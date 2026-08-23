using Buzz.MachineInterface;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using WDE.AudioBlock.r8brain;

namespace WDE.AudioBlock
{
    public class RealTimeResampler : IDisposable
    {
        public static int RT_BUFFER_SIZE = 2 * 1024;   // floats, interleaved stereo
        public static int DEST_BUFFER_SIZE = 2 * 2048; // frames, destData = DEST_BUFFER_SIZE * 2 floats

        private R8brain r8bL = new R8brain();
        private R8brain r8bR = new R8brain();

        private readonly float[] sourceData = new float[RT_BUFFER_SIZE];
        private readonly double[] sourceDataDoubleL = new double[RT_BUFFER_SIZE / 2];
        private readonly double[] sourceDataDoubleR = new double[RT_BUFFER_SIZE / 2];
        private int sourceDataFillLevel = 0;

        private bool inputReady;

        private readonly float[] destData = new float[DEST_BUFFER_SIZE * 2]; // interleaved stereo
        private int destDataOffset = 0; // floats used

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

            InputRate = inputRate;
            OutputRate = outputRate;

            inputReady = false;

            r8bL = new R8brain();
            r8bL.Create(inputRate, outputRate, RT_BUFFER_SIZE / 2, 2.0, ER8BResamplerRes.r8brr24);

            r8bR = new R8brain();
            r8bR.Create(inputRate, outputRate, RT_BUFFER_SIZE / 2, 2.0, ER8BResamplerRes.r8brr24);

            Array.Clear(sourceData, 0, sourceData.Length);
            sourceDataFillLevel = 0;

            Array.Clear(destData, 0, destData.Length);
            destDataOffset = 0;
        }

        public void FillBuffer(ref float[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
                return;

            // Must be interleaved stereo
            if ((buffer.Length & 1) != 0)
                return;

            // Do not accept more than RT_BUFFER_SIZE floats at once
            if (buffer.Length > RT_BUFFER_SIZE)
                return;

            // Ensure we don't overflow sourceData
            if (sourceDataFillLevel + buffer.Length > RT_BUFFER_SIZE)
            {
                sourceDataFillLevel = 0;
                Array.Clear(sourceData, 0, sourceData.Length);
            }

            Array.Copy(buffer, 0, sourceData, sourceDataFillLevel, buffer.Length);
            sourceDataFillLevel += buffer.Length;

            int inputBufferFillLevel = Math.Min(RT_BUFFER_SIZE, sourceDataFillLevel);
            if (inputBufferFillLevel < 2)
                return;

            int frames = inputBufferFillLevel / 2;
            if (frames <= 0)
                return;

            // Deinterleave to double buffers
            for (int i = 0; i < frames; i++)
            {
                int idx = i * 2;
                sourceDataDoubleL[i] = sourceData[idx];
                sourceDataDoubleR[i] = sourceData[idx + 1];
            }

            int outputLengthGeneratedL = r8bL.Process(sourceDataDoubleL, frames, out double[] outputDataDoubleL);
            int outputLengthGeneratedR = r8bR.Process(sourceDataDoubleR, frames, out double[] outputDataDoubleR);

            if (outputLengthGeneratedL <= 0 || outputLengthGeneratedR <= 0)
            {
                sourceDataFillLevel -= inputBufferFillLevel;
                if (sourceDataFillLevel < 0) sourceDataFillLevel = 0;
                return;
            }

            int outLen = Math.Min(outputLengthGeneratedL, outputLengthGeneratedR);
            int requiredFloats = outLen * 2;

            // Wrap destDataOffset safely if needed
            if (destDataOffset + requiredFloats > destData.Length)
            {
                destDataOffset = 0;
                Array.Clear(destData, 0, destData.Length);
            }

            for (int i = 0; i < outLen; i++)
            {
                int idx = destDataOffset + i * 2;
                destData[idx] = (float)outputDataDoubleL[i];
                destData[idx + 1] = (float)outputDataDoubleR[i];
            }

            destDataOffset += requiredFloats;

            sourceDataFillLevel -= inputBufferFillLevel;
            if (sourceDataFillLevel < 0) sourceDataFillLevel = 0;
        }

        internal void FillBuffer(ref Sample[] sampleDataTmp)
        {
            if (sampleDataTmp == null || sampleDataTmp.Length == 0)
                return;

            int frames = sampleDataTmp.Length;
            int floatCount = frames * 2;

            if (floatCount > RT_BUFFER_SIZE)
                return;

            var buf = new float[floatCount];
            for (int i = 0; i < frames; i++)
            {
                int idx = i * 2;
                buf[idx] = sampleDataTmp[i].L;
                buf[idx + 1] = sampleDataTmp[i].R;
            }

            FillBuffer(ref buf);
        }

        public void GetSamples(ref Sample[] outbuffer, int num, float gainL, float gainR)
        {
            if (outbuffer == null || num <= 0 || outbuffer.Length < num)
                return;

            int availableFrames = destDataOffset / 2;
            bool enoughData = availableFrames >= num;

            if (!enoughData)
            {
                inputReady = false;
                for (int i = 0; i < num; i++)
                {
                    outbuffer[i].L = 0;
                    outbuffer[i].R = 0;
                }
                return;
            }

            inputReady = true;

            int requiredFloats = num * 2;
            for (int i = 0; i < num; i++)
            {
                int idx = i * 2;
                outbuffer[i].L += destData[idx] * gainL;
                outbuffer[i].R += destData[idx + 1] * gainR;
            }

            int shift = requiredFloats;
            if (shift > destDataOffset)
                shift = destDataOffset;

            int remaining = destDataOffset - shift;
            if (remaining > 0)
                Array.Copy(destData, shift, destData, 0, remaining);

            Array.Clear(destData, remaining, destData.Length - remaining);

            destDataOffset = remaining;
        }

        public void Dispose()
        {
            if (r8bL != null)
            {
                r8bL.Dispose();
                r8bL = null;
            }

            if (r8bR != null)
            {
                r8bR.Dispose();
                r8bR = null;
            }
        }

        internal void FillSilenceInSamples(int numSamples)
        {
            if (numSamples <= 0)
                return;

            int floatCount = numSamples * 2;
            if (floatCount > RT_BUFFER_SIZE)
                floatCount = RT_BUFFER_SIZE;

            var buffer = new float[floatCount];
            FillBuffer(ref buffer);
        }

        public void Clear()
        {
            sourceDataFillLevel = 0;
            destDataOffset = 0;
            inputReady = false;

            Array.Clear(sourceData, 0, sourceData.Length);
            Array.Clear(destData, 0, destData.Length);

            r8bL.Clear();
            r8bR.Clear();
        }

        internal bool IsDirty()
        {
            return sourceDataFillLevel > 0 || destDataOffset > 0;
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
        private readonly Dictionary<ISequence, RTResamplerData> realTimeResamplerTable;
        private readonly RealTimeResampler playingPatternEditorPatternRealTimeResampler;
        private static readonly object syncLock = new object();

        public RealTimeResamplerManager()
        {
            realTimeResamplerTable = new Dictionary<ISequence, RTResamplerData>();
            playingPatternEditorPatternRealTimeResampler = new RealTimeResampler();
            playingPatternEditorPatternRealTimeResampler.Reset(44100, 44100);
        }

        public void ResetRealTimeResamplers()
        {
            lock (syncLock)
            {
                foreach (var rtr in realTimeResamplerTable.Values)
                {
                    rtr?.RealTimeResampler?.Clear();
                }

                playingPatternEditorPatternRealTimeResampler.Clear();
            }
        }

        internal void Check(ISequence seq, IPattern pat)
        {
            if (seq == null)
                return;

            lock (syncLock)
            {
                if (!realTimeResamplerTable.TryGetValue(seq, out var data))
                {
                    realTimeResamplerTable[seq] = new RTResamplerData(pat);
                }
                else if (data.Pattern != pat)
                {
                    data.RealTimeResampler.Dispose();
                    realTimeResamplerTable[seq] = new RTResamplerData(pat);
                }
            }
        }

        internal void Clear(ISequence seq)
        {
            if (seq == null)
                return;

            lock (syncLock)
            {
                if (realTimeResamplerTable.TryGetValue(seq, out var data))
                {
                    if (data.RealTimeResampler.IsDirty())
                        data.RealTimeResampler.Clear();
                }
            }
        }

        internal RealTimeResampler GetResampler(ISequence seq)
        {
            lock (syncLock)
            {
                if (seq != null && realTimeResamplerTable.TryGetValue(seq, out var data))
                    return data.RealTimeResampler;

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
