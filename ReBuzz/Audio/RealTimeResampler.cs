using System;
using BuzzGUI.WaveformControl.r8brain;

namespace ReBuzz.Audio
{
    public class RealTimeResampler : IDisposable
    {
        public static int RT_BUFFER_SIZE = 1024;
        public static int DEST_BUFFER_SIZE = 2048;

        private R8brain[] resamplers;
        private float[] sourceData;
        private double[][] sourceDataDouble;

        private int sourceDataFillLevel = 0;

        private float[] destData;
        private int destDataWritePos = 0;
        private int destDataReadPos = 0;
        private int destDataFillLevel = 0;

        public int InputRate { get; private set; }
        public int OutputRate { get; private set; }
        public int ChannelCount { get; private set; }

        public double BufferReminder { get; internal set; }
        public int ReadEndPos { get; internal set; }

        public RealTimeResampler()
        {
            InputRate = -1;
            OutputRate = -1;
            ChannelCount = 2;
        }

        public void Reset(int outputRate, int inputRate, int channelCount)
        {
            Dispose();

            InputRate = inputRate;
            OutputRate = outputRate;
            ChannelCount = channelCount;

            // Allocate per-channel resamplers
            resamplers = new R8brain[channelCount];
            sourceDataDouble = new double[channelCount][];

            for (int ch = 0; ch < channelCount; ch++)
            {
                resamplers[ch] = new R8brain();
                resamplers[ch].Create(inputRate, outputRate, RT_BUFFER_SIZE, 2.0, ER8BResamplerRes.r8brr24);

                sourceDataDouble[ch] = new double[RT_BUFFER_SIZE];
            }

            // Interleaved input buffer
            sourceData = new float[RT_BUFFER_SIZE * channelCount];
            sourceDataFillLevel = 0;

            // Interleaved output ring buffer
            destData = new float[DEST_BUFFER_SIZE * channelCount];
            destDataWritePos = 0;
            destDataReadPos = 0;
            destDataFillLevel = 0;
        }

        /// <summary>
        /// Fill input buffer with interleaved samples.
        /// count = number of frames
        /// buffer.Length = count * ChannelCount
        /// </summary>
        public void FillBuffer(float[] buffer, int frames)
        {
            int samples = frames * ChannelCount;

            if (buffer == null || buffer.Length < samples)
                return;

            Array.Copy(buffer, 0, sourceData, sourceDataFillLevel, samples);
            sourceDataFillLevel += samples;

            int inputSamples = Math.Min(sourceDataFillLevel, RT_BUFFER_SIZE * ChannelCount);
            int inputFrames = inputSamples / ChannelCount;

            // De-interleave into per-channel double buffers
            for (int ch = 0; ch < ChannelCount; ch++)
            {
                for (int f = 0; f < inputFrames; f++)
                {
                    sourceDataDouble[ch][f] = sourceData[f * ChannelCount + ch];
                }
            }

            // Process each channel
            int outputFrames = 0;
            double[][] outputDouble = new double[ChannelCount][];

            for (int ch = 0; ch < ChannelCount; ch++)
            {
                outputFrames = resamplers[ch].Process(
                    sourceDataDouble[ch],
                    inputFrames,
                    out outputDouble[ch]
                );
            }

            // Interleave into destData ring buffer
            int writePos = destDataWritePos;

            for (int f = 0; f < outputFrames; f++)
            {
                for (int ch = 0; ch < ChannelCount; ch++)
                {
                    destData[writePos] = (float)outputDouble[ch][f];
                    writePos++;

                    if (writePos >= destData.Length)
                        writePos = 0;
                }
            }

            destDataWritePos = writePos;
            destDataFillLevel += outputFrames * ChannelCount;

            sourceDataFillLevel -= inputSamples;
        }

        public void GetSamples(float[] outbuffer, int offset, int frames)
        {
            int samplesNeeded = frames * ChannelCount;

            if (destDataFillLevel < samplesNeeded)
            {
                Array.Clear(outbuffer, offset, samplesNeeded);
                return;
            }

            for (int i = 0; i < samplesNeeded; i++)
            {
                outbuffer[offset + i] = destData[destDataReadPos];
                destDataReadPos++;

                if (destDataReadPos >= destData.Length)
                    destDataReadPos = 0;
            }

            destDataFillLevel -= samplesNeeded;
        }

        public void FillSilenceInSamples(int numSamples)
        {
            float[] buffer = new float[numSamples * ChannelCount];
            FillBuffer(buffer, numSamples);
        }

        public void Clear()
        {
            sourceDataFillLevel = 0;
            destDataWritePos = 0;
            destDataReadPos = 0;

            foreach (var r in resamplers)
                r?.Clear();
        }

        public bool IsDirty()
        {
            return (sourceDataFillLevel > 0 || destDataWritePos > 0);
        }

        public int AvailableFrames()
        {
            return destDataFillLevel / ChannelCount;
        }

        public void Dispose()
        {
            if (resamplers != null)
            {
                foreach (var r in resamplers)
                    r.Dispose();
            }
        }
    }
}
