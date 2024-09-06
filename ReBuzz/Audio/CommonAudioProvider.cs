using Buzz.MachineInterface;
using BuzzGUI.Common;
using ReBuzz.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ReBuzz.Audio
{
    internal class CommonAudioProvider
    {
        readonly WorkManager workManager;
        private readonly ReBuzzCore buzz;
        //public WaveFormat WaveFormat { get; }

        WorkThreadEngine workEngine;
        readonly ManualResetEvent fillBufferEvent = new ManualResetEvent(false);
        //readonly ManualResetEvent fillBufferDoneEvent = new ManualResetEvent(false);

        bool stopped;
        private readonly float[] threadBuffer;
        private readonly float[][] threadBufferChannel;
        private readonly float[] fillBuffer;
        internal readonly float[][] fillBufferChannel;
        int threadBufferWriteOffset = 0;
        int threadBufferFillLevel = 0;
        int threadBufferReadOffset = 0;
        private int fillBufferNeed;
        readonly Thread audioThread;
        Task audioTask;
        private readonly int channels;
        readonly EAudioThreadType threadType;

        public CommonAudioProvider(ReBuzzCore buzzCore, int sampleRate, int channels, int bufferSize, bool doubleBuffer)
        {
            this.buzz = buzzCore;
            buzzCore.SelectedAudioDriverSampleRate = sampleRate;
            this.channels = channels;

            int threadBufferSize = bufferSize < 16 ? 16 : bufferSize * 2; // Stereo
            int size = doubleBuffer ? threadBufferSize * 2 : threadBufferSize; // Double buffer

            threadBuffer = new float[size];
            fillBuffer = new float[size];

            fillBufferChannel = new float[64][];
            for (int i = 0; i < 64; i++)
                fillBufferChannel[i] = new float[size];

            threadBufferChannel = new float[64][];
            for (int i = 0; i < 64; i++)
                threadBufferChannel[i] = new float[size];

            long processorAffinityMask = RegistryEx.Read("ProcessorAffinity", 0xFFFFFFFF, "Settings");

            int processorCount = Environment.ProcessorCount;
            int numAudioThreads = 0;
            for (int i = 0; i < processorCount; i++)
            {
                if ((processorAffinityMask & (1L << i)) != 0)
                {
                    numAudioThreads++;
                }
            }

            int algorithm = RegistryEx.Read("WorkAlgorithm", 1, "Settings");
            int threadCount = RegistryEx.Read("AudioThreads", 4, "Settings");

            if (algorithm == 0 || algorithm == 1)
            {
            }
            else
            {
                workEngine = new WorkThreadEngine(threadCount);
                workEngine.Start();
            }

            workManager = new WorkManager(buzzCore, workEngine, algorithm);

            threadType = (EAudioThreadType)RegistryEx.Read("AudioThreadType", 0, "Settings");

            if (threadType == EAudioThreadType.TaskScheduler)
            {
                // Use default scheduler
                //Task.Factory.StartNew(() => { BufferFillThread(); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                audioTask = AudioEngine.TaskFactoryAudio.StartNew(() => { BufferFillThread(); }, CancellationToken.None, TaskCreationOptions.LongRunning, AudioEngine.TaskSchedulerAudio);
            }
            else if (threadType == EAudioThreadType.Thread)
            {
                audioThread = new Thread(this.BufferFillThread);
                audioThread.Priority = ProcessAndThreadProfile.AudioProviderThread;
                audioThread.IsBackground = true;
                audioThread.Start();
            }
        }

        public void ClearBuffer()
        {
            Array.Clear(threadBuffer, 0, threadBuffer.Length);
            fillBufferNeed = 0;
            threadBufferReadOffset = 0;
            threadBufferFillLevel = 0;
            threadBufferWriteOffset = 0;
        }

        private readonly object bufferLock = new object();

        private void BufferFillThread()
        {
            while (!stopped)
            {
                // Wait until we need to update buffer
                fillBufferEvent.WaitOne();
                if (stopped)
                {
                    return;
                }

                lock (bufferLock)
                {
                    int fillNeed = fillBufferNeed;
                    int fillTarget;
                    while (fillNeed != 0)
                    {
                        fillTarget = Math.Min(fillNeed, threadBuffer.Length);
                        if (stopped)
                        {
                            return;
                        }

                        int readSize = fillTarget - threadBufferFillLevel;

                        // Do we already have enough?
                        if (readSize <= 0)
                        {
                            break;
                        }

                        FillTheBuffer(readSize);
                        fillNeed -= fillTarget;
                    }
                    fillBufferEvent.Reset();
                }
            }
        }

        int FillTheBuffer(int readSize)
        {
            lock (bufferLock)
            {
                int numRead = workManager.ThreadReadSpeedAdjust(fillBuffer, 0, readSize);

                int offset = 0;
                while (numRead > 0)
                {
                    int count = numRead;

                    // We shall stay with in the buffer
                    if (count + threadBufferWriteOffset > threadBuffer.Length)
                        count = threadBuffer.Length - threadBufferWriteOffset;

                    Buffer.BlockCopy(fillBuffer, offset << 2, threadBuffer, threadBufferWriteOffset << 2, count << 2);

                    // Copy other output channels
                    int stereaOutChannels = (channels - 2);
                    for (int j = 1; j < stereaOutChannels; j++)
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

                    numRead -= count;
                }
                return numRead;
            }
        }

        public unsafe int Read(float[] buffer, int offset, int count)
        {
            // Override audio driver and call workManager.ThreadRead outside of Read
            if (buzz.OverrideAudioDriver || stopped || ReBuzzCore.SkipAudio)
            {
                Array.Clear(buffer, offset, count);
                ClearBuffer();
                return count;
            }

            int countRemaining = count / channels * 2;

            while (countRemaining > 0 && !stopped)
            {
                if (ReBuzzCore.SkipAudio)
                {
                    Array.Clear(buffer, offset, count);
                    return count;
                }

                lock (bufferLock)
                {
                    int readCount = countRemaining;
                    readCount = Math.Min(readCount, threadBufferFillLevel);

                    if (threadBufferReadOffset + readCount > threadBuffer.Length)
                        readCount = threadBuffer.Length - threadBufferReadOffset;

                    if (readCount != 0)
                    {
                        //Buffer.BlockCopy(threadBuffer, threadBufferReadOffset << 2, buffer, offset << 2, readCount << 2);
                        for (int i = 0; i < readCount / 2; i++)
                        {
                            buffer[offset++] = threadBuffer[threadBufferReadOffset];
                            buffer[offset++] = threadBuffer[threadBufferReadOffset + 1];

                            // Copy other output channels. Skip the first
                            int stereoChannels = (channels) / 2;
                            for (int j = 1; j < stereoChannels; j++)
                            {
                                buffer[offset++] = threadBufferChannel[j][threadBufferReadOffset];
                                buffer[offset++] = threadBufferChannel[j][threadBufferReadOffset + 1];
                            }
                            threadBufferReadOffset += 2;
                        }

                        //offset += readCount;

                        //threadBufferReadOffset += readCount;
                        if (threadBufferReadOffset == threadBuffer.Length)
                            threadBufferReadOffset = 0;

                        threadBufferFillLevel -= readCount;
                        countRemaining -= readCount;
                    }
                    else
                    {
                        int readSize = Math.Min(countRemaining, threadBuffer.Length);
                        FillTheBuffer(readSize);
                    }
                    fillBufferNeed = threadBuffer.Length - threadBufferFillLevel;
                }
            }
            if (!stopped && threadType != EAudioThreadType.None)
            {
                // Continue filling the buffer in BufferFillThread
                fillBufferEvent.Set();
            }
            return count;
        }

        public void Stop()
        {
            stopped = true;             // Stop audio thread
            lock (bufferLock)
            {
                fillBufferEvent.Set();      // Stop waiting for request to fill buffer
            }

            workManager.Stop();
            if (workEngine != null)
            {
                workEngine.Stop();
                workEngine = null;
            }

            if (audioTask != null)
            {
                try
                {
                    Task.WaitAll(audioTask);
                }
                catch { }
                audioTask = null;
            }

            if (audioThread != null)
            {
                audioThread.Join();
            }
        }

        internal int ReadOverride(float[] buffer, int offset, int count)
        {
            return workManager.ThreadRead(buffer, offset, count);
        }

        internal void FillChannel(int channel, Sample[] samples, int n)
        {
            if (channel < 1 || channel > channels / 2)
                return;

            var fillChannel = fillBufferChannel[channel];
            var workBufferOffset = workManager.workBufferOffset;    // Ugly, but we need to know where to write
            int j = 0;
            float audioOutMul = 1 / 32768.0f;

            if (buzz.Speed == 0)
            {
                for (int i = 0; i < n; i++)
                {
                    fillChannel[j++ + workBufferOffset] += samples[i].L * audioOutMul;
                    fillChannel[j++ + workBufferOffset] += samples[i].R * audioOutMul;
                }
            }
            else
            {

                double mul = (Math.Abs(buzz.Speed) / 20.0 + 1.0);
                int targetCount = (int)(n * mul * 2); // Stereo

                //targetCount = targetCount + workBufferOffset < fillChannel.Length ? targetCount : fillChannel.Length - workBufferOffset;
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
                    fillChannel[i + workBufferOffset] += toBuffer[i] * audioOutMul;
                }
            }
        }
    }
}
