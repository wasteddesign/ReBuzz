using Buzz.MachineInterface;
using System;
using BuzzGUI.WaveformControl.r8brain;

namespace ReBuzz.Audio
{
    public class RealTimeResampler
    {
        public static int RT_BUFFER_SIZE = 1024;
        public static int DEST_BUFFER_SIZE = 2048;

        R8brain r8bL = new R8brain();
        R8brain r8bR = new R8brain();
        float[] sourceData = new float[RT_BUFFER_SIZE];
        double[] sourceDataDoubleL = new double[RT_BUFFER_SIZE];
        double[] sourceDataDoubleR = new double[RT_BUFFER_SIZE];
        int sourceDataFillLevel = 0;

        bool inputReady;

        float[] destData = new float[DEST_BUFFER_SIZE * 2];
        int destDataWritePos = 0;
        int destDataReadPos = 0;
        int destDataFilleLevel = 0;

        public int InputRate { get; set; }
        public int OutputRate { get; set; }
        public double BufferReminder { get; internal set; }
        public int ReadEndPos { get; internal set; }

        public RealTimeResampler()
        {
            InputRate = -1;
            OutputRate = -1;
        }

        public void Reset(int outputRate, int inputRate)
        {
            Dispose();

            InputRate = (int)inputRate;
            OutputRate = (int)outputRate;

            inputReady = false;

            r8bL = new R8brain();
            r8bL.Create(inputRate, outputRate, RT_BUFFER_SIZE, 2.0, ER8BResamplerRes.r8brr24);

            r8bR = new R8brain();
            r8bR.Create(inputRate, outputRate, RT_BUFFER_SIZE, 2.0, ER8BResamplerRes.r8brr24);

            Array.Clear(sourceData, 0, sourceData.Length);
            sourceDataFillLevel = 0;

            Array.Clear(destData, 0, destData.Length);
            destDataWritePos = 0;
            destDataReadPos = 0;
            destDataFilleLevel = 0;
        }

        // count == stereo samples
        public void FillBuffer(float[] buffer, int count)
        {
            if (buffer == null || buffer.Length > RT_BUFFER_SIZE || buffer.Length == 0) // Nonsense
                return;

            Array.Copy(buffer, 0, sourceData, sourceDataFillLevel, count * 2);
            sourceDataFillLevel += count * 2;

            int inputBufferFillLevel = Math.Min(RT_BUFFER_SIZE, sourceDataFillLevel);

            // Handle both channels
            // First left
            int outputLengthGeneratedL;
            double[] outputDataDoubleL;

            for (int i = 0; i < inputBufferFillLevel / 2; i++)
            {
                sourceDataDoubleL[i] = sourceData[2 * i];
            }

            // Right
            int outputLengthGeneratedR;
            double[] outputDataDoubleR;

            for (int i = 0; i < inputBufferFillLevel / 2; i++)
            {
                sourceDataDoubleR[i] = sourceData[2 * i + 1];
            }

            outputLengthGeneratedL = r8bL.Process(sourceDataDoubleL, inputBufferFillLevel / 2, out outputDataDoubleL);
            outputLengthGeneratedR = r8bR.Process(sourceDataDoubleR, inputBufferFillLevel / 2, out outputDataDoubleR);

            int dataPosL = destDataWritePos;
            for (int i = 0; i < outputLengthGeneratedL; i++)
            {
                destData[dataPosL] = (float)outputDataDoubleL[i];
                dataPosL += 2;
                if (dataPosL >= destData.Length)
                    dataPosL = 0;
            }

            int dataPosR = destDataWritePos + 1;
            for (int i = 0; i < outputLengthGeneratedR; i++)
            {
                destData[dataPosR] = (float)outputDataDoubleR[i];
                dataPosR += 2;
                if (dataPosR >= destData.Length)
                    dataPosR = 1;
            }

            sourceDataFillLevel -= inputBufferFillLevel;

            destDataWritePos = (destDataWritePos + outputLengthGeneratedL * 2) % destData.Length;
            destDataFilleLevel += outputLengthGeneratedL * 2;
        }

        internal void FillBuffer(Sample[] sampleDataTmp, int count)
        {
            float[] buf = new float[sampleDataTmp.Length * 2];
            for (int i = 0; i < sampleDataTmp.Length; i++)
            {
                buf[i * 2] = sampleDataTmp[i].L;
                buf[i * 2 + 1] = sampleDataTmp[i].R;
            }

            FillBuffer(buf, count);
        }


        public void GetSamples(Sample[] outbuffer, int num, float gainL, float gainR)
        {
            if (destDataWritePos / 2 >= num)
            {
                inputReady = true;
            }
            else
            {
                for (int i = 0; i < num; i++)
                {
                    outbuffer[i].L = 0;
                    outbuffer[i].R = 0;
                }
            }

            if (inputReady && num <= destDataWritePos / 2)
            {
                for (int i = 0; i < num; i++)
                {
                    outbuffer[i].L += destData[i * 2] * gainL;
                    outbuffer[i].R += destData[i * 2 + 1] * gainR;
                }
                Array.Copy(destData, num * 2, destData, 0, destData.Length - num * 2);
                destDataWritePos -= num * 2;
            }
        }

        public void GetSamples(float[] outbuffer, int offset, int num)
        {
            int bufferCount = num * 2;

            // Ensure there is data available
            if (destDataFilleLevel >= bufferCount)
            {
                inputReady = true;
            }
            else
            {
                for (int i = 0; i < bufferCount; i++)
                {
                    outbuffer[i] = 0;
                }
            }

            if (inputReady)
            {
                for (int i = 0; i < bufferCount; i++)
                {
                    outbuffer[i] = destData[destDataReadPos];
                    destDataReadPos++;
                    if (destDataReadPos >= destData.Length)
                        destDataReadPos = 0;
                }
                destDataFilleLevel -= bufferCount;
            }
        }

        public void Dispose()
        {
            if (r8bL != null)
                r8bL.Dispose();
            if (r8bR != null)
                r8bR.Dispose();
        }

        internal void FillSilenceInSamples(int numSamples)
        {
            float[] buffer = new float[2 * numSamples];
            FillBuffer(buffer, numSamples);
        }

        public void Clear()
        {
            sourceDataFillLevel = 0;
            destDataWritePos = 0;
            r8bL.Clear();
            r8bR.Clear();
        }

        internal bool IsDirty()
        {
            return (sourceDataFillLevel > 0 || destDataWritePos > 0);
        }

        internal int AvailableSamples()
        {
            return destDataFilleLevel >> 1;
        }
    }
}
