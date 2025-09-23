using Buzz.MachineInterface;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using WDE.AudioBlock.r8brain;

namespace WDE.AudioBlock
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
        int destDataOffset = 0;

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

            // sourceData = new float[RT_BUFFER_SIZE];
            Array.Clear(sourceData, 0, sourceData.Length);
            sourceDataFillLevel = 0;

            //destData = new float[RT_BUFFER_SIZE * 2];
            Array.Clear(destData, 0, destData.Length);
            destDataOffset = 0;
        }

        public void FillBuffer(ref float[] buffer)
        {
            if (buffer == null || buffer.Length > RT_BUFFER_SIZE || buffer.Length == 0) // Nonsense
                return;

            if (buffer.Length + destDataOffset > DEST_BUFFER_SIZE)
                destDataOffset = 0;

            Array.Copy(buffer, 0, sourceData, sourceDataFillLevel, buffer.Length);
            sourceDataFillLevel += buffer.Length;

            int inputBufferFillLevel = Math.Min(1000, sourceDataFillLevel);

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

            if (2 * outputLengthGeneratedL + destDataOffset > DEST_BUFFER_SIZE)
                destDataOffset = 0;
            if (2 * outputLengthGeneratedR + destDataOffset > DEST_BUFFER_SIZE)
                destDataOffset = 0;

            for (int i = 0; i < Math.Min(outputLengthGeneratedL, DEST_BUFFER_SIZE); i++)
            {
                destData[destDataOffset + 2 * i] = (float)outputDataDoubleL[i];
            }

            for (int i = 0; i < Math.Min(outputLengthGeneratedR, DEST_BUFFER_SIZE); i++)
            {
                destData[destDataOffset + 2 * i + 1] = (float)outputDataDoubleR[i];
            }

            // Global.Buzz.DCWriteLine("outputLengthGeneratedL: " + outputLengthGeneratedL + ", outputLengthGeneratedR: " + outputLengthGeneratedR);
            sourceDataFillLevel -= inputBufferFillLevel;

            // Global.Buzz.DCWriteLine("sourceDataFillLevel: " + sourceDataFillLevel + ", destDataOffset: " + destDataOffset);

            // outputLengthGeneratedL == outputLengthGeneratedR so we can do this:
            destDataOffset += outputLengthGeneratedL * 2;
        }

        internal void FillBuffer(ref Sample[] sampleDataTmp)
        {
            float[] buf = new float[sampleDataTmp.Length * 2];
            for (int i = 0; i < sampleDataTmp.Length; i++)
            {
                buf[i * 2] = sampleDataTmp[i].L;
                buf[i * 2 + 1] = sampleDataTmp[i].R;
            }

            FillBuffer(ref buf);
        }


        public void GetSamples(ref Sample[] outbuffer, int num, float gainL, float gainR)
        {
            if (destDataOffset / 2 >= num)
            {
                inputReady = true;
            }
            else
            {
                // Global.Buzz.DCWriteLine("Buffer not ready yet. Requested size: " + num);
                for (int i = 0; i < num; i++)
                {
                    outbuffer[i].L = 0;
                    outbuffer[i].R = 0;
                } 
            }

            if (inputReady && num <= destDataOffset / 2)
            {
                for (int i = 0; i < num; i++)
                {
                    outbuffer[i].L += destData[i * 2] * gainL;
                    outbuffer[i].R += destData[i * 2 + 1] * gainR;
                }
                Array.Copy(destData, num * 2, destData, 0, destData.Length - num * 2);
                destDataOffset -= num * 2;
            }
            // Global.Buzz.DCWriteLine("RT Resample sourceDataFillLevel: " + sourceDataFillLevel + " | destDataOffset: " + destDataOffset);
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
            FillBuffer(ref buffer);
        }

        public void Clear()
        {
            sourceDataFillLevel = 0;
            destDataOffset = 0;
            r8bL.Clear();
            r8bR.Clear();
        }

        internal bool IsDirty()
        {
            return (sourceDataFillLevel > 0 || destDataOffset > 0);
        }
    }

    class RTResamplerData
    {
        public IPattern Pattern { get; set; }
        public RealTimeResampler RealTimeResampler { get; set; }
        public RTResamplerData(IPattern pat)
        {
            Pattern = pat;
            RealTimeResampler = new RealTimeResampler();
        }
    }
    

    public class RealTimeResamplerManager
    {
        private Dictionary<ISequence, RTResamplerData> realTimeResamplerTable;
        private RealTimeResampler playingPatternEditorPatternRealTimeResampler;
        private static readonly object syncLock = new object();

        public RealTimeResamplerManager()
        {
            realTimeResamplerTable = new Dictionary<ISequence, RTResamplerData>();
            playingPatternEditorPatternRealTimeResampler = new RealTimeResampler();
            playingPatternEditorPatternRealTimeResampler.Reset(44100, 44100); // Do this to avoid clicks/pops when activated first time
        }

        public void ResetRealTimeResamplers()
        {
            foreach (var rtr in realTimeResamplerTable.Values)
            {
                if (rtr != null)
                    rtr.RealTimeResampler.Clear();
            }
        }

        internal void Check(ISequence seq, IPattern pat)
        {
            if (!realTimeResamplerTable.ContainsKey(seq))
            {
                realTimeResamplerTable.Add(seq, new RTResamplerData(pat));
            }
            else if (realTimeResamplerTable[seq].Pattern != pat)
            {
                realTimeResamplerTable[seq].RealTimeResampler.Dispose();
                realTimeResamplerTable[seq] = new RTResamplerData(pat);
            }
        }

        internal void Clear(ISequence seq)
        {
            if (realTimeResamplerTable[seq].RealTimeResampler.IsDirty())
                realTimeResamplerTable[seq].RealTimeResampler.Clear();
        }

        internal RealTimeResampler GetResampler(ISequence seq)
        {
            lock (syncLock)
            {
                if (seq != null)
                    return realTimeResamplerTable[seq].RealTimeResampler;
                else
                    return playingPatternEditorPatternRealTimeResampler;
            }
        }

        internal void FillSilenceInSamples(ISequence seq, int outputOffset)
        {
            lock (syncLock)
            {
                GetResampler(seq).FillSilenceInSamples(outputOffset);
            }
        }

        internal void FillBuffer(ISequence seq, ref Sample[] sampleDataTmp)
        {
            lock (syncLock)
            {
                GetResampler(seq).FillBuffer(ref sampleDataTmp);
            }
        }

        internal void GetSamples(ISequence seq, ref Sample[] output, int numsamples, float gainL, float gainR)
        {
            lock (syncLock)
            {
                GetResampler(seq).GetSamples(ref output, numsamples, gainL, gainR);
            }
        }
    }
}
