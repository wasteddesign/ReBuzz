using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Common.Settings;
using BuzzGUI.Interfaces;
using ReBuzz.Audio.BurstProtection;
using ReBuzz.Common;
using ReBuzz.Core;
using System;
using System.Threading;

namespace ReBuzz.Audio
{
    internal class CommonAudioProvider
    {
        readonly WorkManager workManager;
        private readonly ReBuzzCore buzz;

        WorkThreadEngine workEngine;
        
        bool stopped;
        private readonly float[] threadBuffer;
        private readonly float[][] threadBufferChannel;
        private readonly float[] fillBuffer;
        internal readonly float[][] fillBufferChannel;
        int threadBufferWriteOffset = 0;
        int threadBufferFillLevel = 0;
        int threadBufferReadOffset = 0;
        private int fillBufferNeed;
        private readonly int outputChannels;
        private readonly int threadBufferSize;
        readonly EAudioThreadType threadType;

        // Deadline-miss instrument (read by PP2 via ReBuzzCore accessors).
        readonly System.Diagnostics.Stopwatch deadlineSw = System.Diagnostics.Stopwatch.StartNew();
        readonly double deadlineTicksToMs = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        readonly int deadlineSampleRate;
        const double DeadlineWarmupMs = 1000.0;   // ignore startup priming misses

        BurstProtectionEngine audioBurstProtection;
        readonly int maxStereoChannelCount = 32;
        public int MaxStereoChannelCount => maxStereoChannelCount;

        public CommonAudioProvider(
          ReBuzzCore buzzCore,
          EngineSettings engineSettings,
          int sampleRate,
          int channels,
          int bufferSize,
          bool doubleBuffer, IRegistryEx registryEx)
        {
            this.buzz = buzzCore;
            buzzCore.SelectedAudioDriverSampleRate = sampleRate;
            this.outputChannels = channels;
            deadlineSampleRate = sampleRate;

            threadBufferSize = bufferSize < 16 ? 16 : bufferSize * 2; // Stereo
            int size = doubleBuffer ? threadBufferSize * 2 : threadBufferSize; // Double buffer

            threadBuffer = new float[size];
            fillBuffer = new float[size];

            fillBufferChannel = new float[maxStereoChannelCount][];
            for (int i = 0; i < maxStereoChannelCount; i++)
                fillBufferChannel[i] = new float[size];

            threadBufferChannel = new float[maxStereoChannelCount][];
            for (int i = 0; i < maxStereoChannelCount; i++)
                threadBufferChannel[i] = new float[size];

            long processorAffinityMask = registryEx.Read("ProcessorAffinity", 0xFFFFFFFF, "Settings");

            int processorCount = Environment.ProcessorCount;
            int numAudioThreads = 0;
            for (int i = 0; i < processorCount; i++)
            {
                if ((processorAffinityMask & (1L << i)) != 0)
                {
                    numAudioThreads++;
                }
            }

            int algorithm = registryEx.Read("WorkAlgorithm", 1, "Settings");
            int threadCount = registryEx.Read("AudioThreads", 4, "Settings");

            if (algorithm == 0 || algorithm == 1)
            {
            }
            else
            {
                workEngine = new WorkThreadEngine(threadCount);
                workEngine.Start();
            }

            audioBurstProtection = new BurstProtectionEngine(buzz, true);

            workManager = new WorkManager(buzzCore, workEngine, algorithm, engineSettings);

            threadType = (EAudioThreadType)registryEx.Read("AudioThreadType", 0, "Settings");

            multimediaTimer = new MultimediaTimer() { Interval = 1 };
            multimediaTimer.Elapsed += (o, e) =>
            {
                lock (bufferLock)
                {
                    int readSize = threadBuffer.Length - threadBufferFillLevel;
                    if (readSize > 0)
                    {
                        FillTheBuffer(readSize);
                    }
                }
            };

            if (Global.EngineSettings.AudioBufferFillThread)
            {
                multimediaTimer.Start();
            }
        }

        MultimediaTimer multimediaTimer;

        public void ClearBuffer()
        {
            Array.Clear(threadBuffer, 0, threadBuffer.Length);
            fillBufferNeed = 0;
            threadBufferReadOffset = 0;
            threadBufferFillLevel = 0;
            threadBufferWriteOffset = 0;
        }

        internal void ClearChannels()
        {
            for (int i = 1; i < outputChannels / 2; i++)
            {
                Array.Clear(fillBufferChannel[i], 0, fillBufferChannel[i].Length);
            }
        }

        private readonly Lock bufferLock = new();

        public int OutputChannels => outputChannels;

        private int FillTheBuffer(int readSize)
        {
            lock (bufferLock)
            {
                // Override audio driver and call workManager.ThreadRead outside of Read
                if (buzz.OverrideAudioDriver || stopped)
                {
                    Array.Clear(fillBuffer, 0, readSize);
                    return readSize;
                }

                int totalWritten = 0;
                int numRead = workManager.ThreadReadSpeedAdjust(fillBuffer, 0, readSize);

                int offset = 0;
                while (numRead > 0)
                {
                    int count = numRead;

                    // Stay within threadBuffer bounds
                    int spaceLeft = threadBuffer.Length - threadBufferWriteOffset;
                    if (count > spaceLeft)
                        count = spaceLeft;

                    // Copy main stereo buffer
                    Buffer.BlockCopy(fillBuffer, offset << 2, threadBuffer, threadBufferWriteOffset << 2, count << 2);

                    // Copy other output channels
                    int stereoOutChannels = OutputChannels / 2;
                    for (int j = 1; j < stereoOutChannels; j++)
                    {
                        var fromBuffer = fillBufferChannel[j];
                        var toBuffer = threadBufferChannel[j];

                        Buffer.BlockCopy(fromBuffer, offset << 2, toBuffer, threadBufferWriteOffset << 2, count << 2);
                        Array.Clear(fromBuffer, offset, count);
                    }

                    offset += count;
                    threadBufferWriteOffset += count;
                    if (threadBufferWriteOffset == threadBuffer.Length)
                        threadBufferWriteOffset = 0;

                    threadBufferFillLevel += count;
                    totalWritten += count;
                    numRead -= count;
                }

                return totalWritten;
            }
        }

        public unsafe int Read(Span<float> buffer)
        {
            int count = buffer.Length;
            int offset = 0;

            // Override audio driver and call workManager.ThreadRead outside of Read
            if (buzz.OverrideAudioDriver || stopped)
            {
                buffer.Clear();
                ClearBuffer();
                return count;
            }

            // count is in floats; convert to stereo frames (L,R) per output channel pair
            int stereoChannels = OutputChannels / 2;
            int framesRequested = count / OutputChannels; // frames per all channels
            int samplesRequested = framesRequested * 2; // L,R per frame in threadBuffer

            int countRemaining = samplesRequested;

            long deadlineStartTicks = deadlineSw.ElapsedTicks;

            while (countRemaining > 0 && !stopped)
            {
                lock (bufferLock)
                {
                    int readCount = Math.Min(countRemaining, threadBufferFillLevel);
                    float audioOutMul = 1 / 32768.0f;

                    if (threadBufferReadOffset + readCount > threadBuffer.Length)
                        readCount = threadBuffer.Length - threadBufferReadOffset;

                    if (readCount > 0)
                    {
                        audioBurstProtection.Process(threadBuffer, threadBufferReadOffset, readCount, true, false);

                        for (int j = 1; j < stereoChannels; j++)
                        {
                            audioBurstProtection.Process(threadBufferChannel[j], threadBufferReadOffset, readCount, true, false);
                        }

                        int framesToCopy = readCount / 2;

                        for (int f = 0; f < framesToCopy; f++)
                        {
                            // main stereo pair
                            buffer[offset++] = threadBuffer[threadBufferReadOffset];
                            buffer[offset++] = threadBuffer[threadBufferReadOffset + 1];

                            // other output channels
                            for (int j = 1; j < stereoChannels; j++)
                            {
                                var chBuf = threadBufferChannel[j];
                                buffer[offset++] = chBuf[threadBufferReadOffset] * audioOutMul;         // Scale other channels to match required output
                                buffer[offset++] = chBuf[threadBufferReadOffset + 1] * audioOutMul;
                            }

                            threadBufferReadOffset += 2;
                            if (threadBufferReadOffset == threadBuffer.Length)
                                threadBufferReadOffset = 0;
                        }

                        threadBufferFillLevel -= readCount;
                        countRemaining -= readCount;
                    }
                    else
                    {
                        int readSize = Math.Min(countRemaining, threadBuffer.Length);
                        int filled = FillTheBuffer(readSize);

                        // If nothing was filled, avoid tight spin
                        if (filled == 0)
                            break;
                    }

                    fillBufferNeed = threadBuffer.Length - threadBufferFillLevel;
                }
            }

            // Did this callback overrun its OWN block period? Deadline is derived
            // per-call from frames/sampleRate (the driver hands blocks far larger
            // than the nominal buffer size, so the nominal size is the wrong number).
            if (deadlineSampleRate > 0 && OutputChannels > 0)
            {
                long nowTicks = deadlineSw.ElapsedTicks;
                double elapsedMs = (nowTicks - deadlineStartTicks) * deadlineTicksToMs;
                double deadlineMs = (count / OutputChannels) / (double)deadlineSampleRate * 1000.0;
                if (nowTicks * deadlineTicksToMs > DeadlineWarmupMs && elapsedMs > deadlineMs)
                    ReBuzzCore.RecordDeadlineMiss((long)((elapsedMs - deadlineMs) * 1000.0));
            }

            return count - (countRemaining * stereoChannels);
        }

        public void Stop()
        {
            stopped = true;             // Stop audio thread

            workManager.Stop();
            if (workEngine != null)
            {
                workEngine.Stop();
                workEngine.AllDoneEvent().WaitOne();
                workEngine = null;
            }

            if (multimediaTimer != null)
            {
                if (multimediaTimer.IsRunning)
                {
                    multimediaTimer.Stop();
                }
                multimediaTimer.Dispose();
                multimediaTimer = null;
            }

            audioBurstProtection.Release();
        }

        public int FillBufferForMultiChannelMasterTap(
            float[] buffer,
            float[] samples,
            int offset,
            int count)
        {
            int stereoChannels = buzz.MasterTapChannelCount / 2;    // number of stereo pairs
            int frames = count / 2;                                 // number of frames to write

            int writeOffset = 0;
            int frameOffset = offset / 2;                           // convert float offset → frame offset

            for (int f = 0; f < frames; f++)
            {
                int sampleIndex = (frameOffset + f) * 2;            // L,R index in samples[]

                // main stereo pair
                buffer[writeOffset++] = samples[sampleIndex];
                buffer[writeOffset++] = samples[sampleIndex + 1];

                // additional stereo pairs
                for (int j = 1; j < stereoChannels; j++)
                {
                    var chBuf = fillBufferChannel[j];
                    buffer[writeOffset++] = chBuf[sampleIndex];
                    buffer[writeOffset++] = chBuf[sampleIndex + 1];
                }
            }

            return count; // number of floats written
        }

        internal int ReadOverride(float[] buffer, int offset, int count, bool multiChannel = false)
        {
            int readCount;

            if (multiChannel)
            {
                readCount = count;
                // count is in floats; convert to stereo frames (L,R) per output channel pair
                int framesRequested = count / buzz.MasterTapChannelCount; // frames per all channels
                int mainStereoSamplesRequested = framesRequested * 2; // L,R per frame in 

                workManager.MainAudioFillBuffer(buffer, offset, mainStereoSamplesRequested);
            }
            else
            {
                readCount = workManager.MainAudioFillBuffer(buffer, offset, count);
            }

            return readCount;
        }

        internal void FillChannel(int channel, Sample[] samples, int n)
        {
            // First channel (0) is the main stereo out
            if (channel < 1 || channel >= maxStereoChannelCount)
                return;

            var fillChannel = fillBufferChannel[channel];
            var workBufferOffset = workManager.workBufferOffset;    // Ugly, but we need to know where to write
            int j = 0;
            
            if (buzz.Speed == 0)
            {
                for (int i = 0; i < n; i++)
                {
                    fillChannel[workBufferOffset + j] += samples[i].L;
                    fillChannel[workBufferOffset + j + 1] += samples[i].R;

                    j += 2;
                }
            }
            else
            {
                double mul = (Math.Abs(buzz.Speed) / 20.0 + 1.0);
                int targetCount = (int)(n * mul * 2); // Stereo

                float[] toBuffer = new float[targetCount];
                float[] fromBuffer = new float[n * 2];

                // Copy incoming samples to fromBuffer
                j = 0;
                for (int i = 0; i < fromBuffer.Length; i += 2)
                {
                    fromBuffer[i] = samples[j].L;
                    fromBuffer[i + 1] = samples[j].R;
                    j++;
                }

                WorkManager.SpeedDown(fromBuffer, 0, fromBuffer.Length, toBuffer, targetCount, false);

                for (int i = 0; i < targetCount; i++)
                {
                    fillChannel[i + workBufferOffset] += toBuffer[i];
                }
            }
        }
    }
}
