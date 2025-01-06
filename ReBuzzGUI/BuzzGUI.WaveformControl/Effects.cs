using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using BuzzGUI.WaveformControl.NoiseRemoval;
using BuzzGUI.WaveformControl.r8brain;
using DSPLib.SoundTouch;
using System;
using System.Linq;
using System.Threading;
using System.Windows;

namespace BuzzGUI.WavetableView
{
    internal class Effects
    {
        public static TemporaryWave TemporaryWaveFromStereo(float[] destData, WaveFormat waveFormat, int sampleRate, int rootNote, string path, string name)
        {
            int sampleCount = destData.Length / 2;
            float[] destDataL = new float[sampleCount];
            float[] destDataR = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                destDataL[i] = destData[2 * i];
                destDataR[i] = destData[2 * i + 1];
            }
            return new TemporaryWave(destDataL, destDataR, WaveFormat.Float32, sampleRate, rootNote, path, name);
        }

        public static void RNNoiseRemoval(IWavetable wt, int wavetableIndex, int layerIndex, int startSample, int endSample, string rnn_filename)
        {
            bool playing = Global.Buzz.Playing;
            Global.Buzz.Playing = false;


            var fromLayer = wt.Waves[wavetableIndex].Layers[layerIndex];

            int inputRate = fromLayer.SampleRate;
            int outputRate = fromLayer.SampleRate;

            int sampleCount = endSample - startSample;
            string name = wt.Waves[wavetableIndex].Name;
            string path = fromLayer.Path;
            int rootNote = fromLayer.RootNote;
            int loopStart = fromLayer.LoopStart;
            int loopEnd = fromLayer.LoopEnd;

            if (fromLayer.ChannelCount == 1)
            {
                float[] sourceData = new float[sampleCount];
                float[] destData = new float[sampleCount];
                fromLayer.GetDataAsFloat(sourceData, 0, 1, 0, startSample, sampleCount); // Mono

                DoRNNoiseRemoval(ref sourceData, ref destData, outputRate, inputRate, rnn_filename);
                fromLayer.SetDataAsFloat(destData, 0, 1, 0, startSample, sampleCount);
                var targetLayer = fromLayer;
                targetLayer.LoopStart = loopStart;
                targetLayer.LoopEnd = loopEnd;
                targetLayer.InvalidateData();
            }
            else if (fromLayer.ChannelCount == 2)
            {
                float[] sourceDataLeft = new float[sampleCount];
                float[] sourceDataRight = new float[sampleCount];
                float[] destDataL = new float[sampleCount];
                float[] destDataR = new float[sampleCount];
                fromLayer.GetDataAsFloat(sourceDataLeft, 0, 1, 0, startSample, sampleCount); // Left
                fromLayer.GetDataAsFloat(sourceDataRight, 0, 1, 1, startSample, sampleCount); // Right
                DoRNNoiseRemoval(ref sourceDataLeft, ref destDataL, outputRate, inputRate, rnn_filename);
                DoRNNoiseRemoval(ref sourceDataRight, ref destDataR, outputRate, inputRate, rnn_filename);
                fromLayer.SetDataAsFloat(destDataL, 0, 1, 0, startSample, sampleCount);
                fromLayer.SetDataAsFloat(destDataR, 0, 1, 1, startSample, sampleCount);
                var targetLayer = fromLayer;
                targetLayer.LoopStart = loopStart;
                targetLayer.LoopEnd = loopEnd;

                targetLayer.InvalidateData();
            }

            Global.Buzz.Playing = playing;
        }

        public static void DoRNNoiseRemoval(ref float[] sourceData, ref float[] destData, double outputRate, double inputRate, string rnn_filename)
        {
            int RNNOISE_FRAME_SIZE = 480; // Don't change

            bool first = true;

            RNNoise rnn = new RNNoise();
            rnn.Create(rnn_filename);
            int nOutOffset = 0;
            float[] inputBuffer = new float[RNNOISE_FRAME_SIZE];
            float[] outputBuffer = new float[RNNOISE_FRAME_SIZE];

            for (int n = 0; n < sourceData.Length;)
            {
                int numRead = n + RNNOISE_FRAME_SIZE < sourceData.Length ? RNNOISE_FRAME_SIZE : RNNOISE_FRAME_SIZE - (n + RNNOISE_FRAME_SIZE - sourceData.Length);
                for (int i = 0; i < numRead; i++)
                    inputBuffer[i] = sourceData[i + n] * 32768.0f; // float to PCM

                rnn.ProcessFrame(inputBuffer, outputBuffer);

                // First frame is silence
                if (!first)
                {
                    for (int i = 0; i < numRead; i++)
                    {
                        destData[nOutOffset + i] = outputBuffer[i] / 32768.0f;
                    }
                    nOutOffset += numRead;
                }

                first = false;
                n += numRead;
            }

            rnn.Destroy();
        }

        public static void Resample(int wavetableIndex, IWaveLayer fromLayer, double outputRate)
        {
            bool playing = Global.Buzz.Playing;
            Global.Buzz.Playing = false;

            IWavetable wt = Global.Buzz.Song.Wavetable;

            double inputRate = fromLayer.SampleRate;


            if (inputRate != outputRate)
            {
                int sampleCount = fromLayer.SampleCount;
                string name = wt.Waves[wavetableIndex].Name;
                string path = fromLayer.Path;
                int rootNote = fromLayer.RootNote;
                int loopStart = (int)Math.Round(fromLayer.LoopStart * outputRate / inputRate);
                int loopEnd = (int)Math.Round(fromLayer.LoopEnd * outputRate / inputRate);

                if (fromLayer.ChannelCount == 1)
                {
                    float[] sourceData = new float[sampleCount];
                    float[] destData = new float[(int)(sampleCount * outputRate / inputRate)];
                    fromLayer.GetDataAsFloat(sourceData, 0, 1, 0, 0, sampleCount); // Mono

                    Resample(ref sourceData, ref destData, outputRate, inputRate);

                    var tw = new TemporaryWave(destData, WaveFormat.Float32, (int)outputRate, rootNote, path, name);
                    var targetLayer = fromLayer;

                    WaveCommandHelpers.ReplaceLayer(wt, wavetableIndex, WaveCommandHelpers.GetLayerIndex(targetLayer), tw);

                    targetLayer.LoopStart = loopStart;
                    targetLayer.LoopEnd = loopEnd;

                    targetLayer.InvalidateData();
                }
                else if (fromLayer.ChannelCount == 2)
                {
                    float[] sourceDataLeft = new float[sampleCount];
                    float[] sourceDataRight = new float[sampleCount];
                    float[] destDataL = new float[(int)(sampleCount * outputRate / inputRate)];
                    float[] destDataR = new float[(int)(sampleCount * outputRate / inputRate)];
                    fromLayer.GetDataAsFloat(sourceDataLeft, 0, 1, 0, 0, sampleCount); // Left
                    fromLayer.GetDataAsFloat(sourceDataRight, 0, 1, 1, 0, sampleCount); // Right
                    Resample(ref sourceDataLeft, ref destDataL, outputRate, inputRate);
                    Resample(ref sourceDataRight, ref destDataR, outputRate, inputRate);

                    var tw = new TemporaryWave(destDataL, destDataR, WaveFormat.Float32, (int)outputRate, rootNote, path, name);
                    var targetLayer = fromLayer;

                    WaveCommandHelpers.ReplaceLayer(wt, wavetableIndex, WaveCommandHelpers.GetLayerIndex(targetLayer), tw);

                    targetLayer.LoopStart = loopStart;
                    targetLayer.LoopEnd = loopEnd;

                    targetLayer.InvalidateData();
                }
            }
            Global.Buzz.Playing = playing;
        }

        /// <summary>
        /// Resample audio samples using libsamplerate.
        /// </summary>
        /// <param name="sourceData"></param>
        /// <param name="destData"></param>
        /// <param name="outputRate"></param>
        /// <param name="inputRate"></param>
        public static void Resample(ref float[] sourceData, ref float[] destData, double outputRate, double inputRate)
        {
            int BUFFER_LENGTH = 1000;
            var r8bL = new R8brain();
            r8bL.Create(inputRate, outputRate, BUFFER_LENGTH, 2.0, ER8BResamplerRes.r8brr24);

            int inputSampleCount;
            int outputSampleCount;
            int totalInputSampleCount = 0;
            int totalOutputSampleCount = 0;
            double[] inputBuffer = new double[BUFFER_LENGTH];
            int inputBufferFillLevel = 0;
            int inputBufferReadOffset = 0;
            double[] outputBuffer;

            do
            {
                if (inputBufferFillLevel == 0)
                {
                    // Refill input buffer
                    inputBufferFillLevel = Math.Min(BUFFER_LENGTH, sourceData.Length - totalInputSampleCount);
                    inputBufferReadOffset = 0;

                    for (int i = 0; i < inputBufferFillLevel; i++)
                    {
                        inputBuffer[i] = sourceData[totalInputSampleCount + i];
                    }
                }

                double[] inputBufferL = new double[inputBufferFillLevel];
                for (int i = 0; i < inputBufferFillLevel; i++)
                {
                    inputBufferL[i] = sourceData[totalInputSampleCount + i];
                }

                outputSampleCount = r8bL.Process(inputBufferL, inputBufferFillLevel, out outputBuffer);

                for (int i = 0; i < outputSampleCount; i++)
                {
                    destData[totalOutputSampleCount + i] = (float)outputBuffer[i];
                }

                inputSampleCount = inputBufferFillLevel;

                inputBufferReadOffset += inputSampleCount;
                inputBufferFillLevel -= inputSampleCount;

                totalInputSampleCount += inputSampleCount;

                totalOutputSampleCount += outputSampleCount;
            }
            while (inputSampleCount > 0 || outputSampleCount > 0);
            r8bL.Dispose();
        }

        public static void ChangePitchSemitones(IWavetable wt, int wavetableIndex, int layerIndex, int startSample, int endSample, float semiTones, int sequenceMs = 0, int seekWindowMs = 0, int overlapMs = 8)
        {
            bool playing = Global.Buzz.Playing;
            Global.Buzz.Playing = false;

            var fromLayer = wt.Waves[wavetableIndex].Layers[layerIndex];
            int sampleRate = fromLayer.SampleRate;

            int sampleCount = endSample - startSample;
            string name = wt.Waves[wavetableIndex].Name;
            string path = fromLayer.Path;
            int rootNote = fromLayer.RootNote;
            int loopStart = fromLayer.LoopStart;
            int loopEnd = fromLayer.LoopEnd;

            if (fromLayer.ChannelCount == 1)
            {
                float[] sourceData = new float[sampleCount];
                float[] destData = new float[sampleCount];
                fromLayer.GetDataAsFloat(sourceData, 0, 1, 0, startSample, sampleCount); // Mono

                ChangePitchSemitones(sampleRate, fromLayer.ChannelCount, semiTones, ref sourceData, ref destData);
                fromLayer.SetDataAsFloat(destData, 0, 1, 0, startSample, sampleCount);
                var targetLayer = fromLayer;
                targetLayer.LoopStart = loopStart;
                targetLayer.LoopEnd = loopEnd;
                targetLayer.InvalidateData();
            }
            else if (fromLayer.ChannelCount == 2)
            {
                float[] sourceData = new float[sampleCount * 2]; // Stereo buffer
                float[] destData = new float[sampleCount * 2]; // Stereo buffer

                fromLayer.GetDataAsFloat(sourceData, 0, 2, 0, startSample, sampleCount); // Left
                fromLayer.GetDataAsFloat(sourceData, 1, 2, 1, startSample, sampleCount); // Right
                ChangePitchSemitones(sampleRate, fromLayer.ChannelCount, semiTones, ref sourceData, ref destData);
                fromLayer.SetDataAsFloat(destData, 0, 2, 0, startSample, sampleCount); // Left
                fromLayer.SetDataAsFloat(destData, 1, 2, 1, startSample, sampleCount); // Right

                var targetLayer = fromLayer;
                targetLayer.LoopStart = loopStart;
                targetLayer.LoopEnd = loopEnd;
                targetLayer.InvalidateData();
            }

            Global.Buzz.Playing = playing;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="soundTouch"></param>
        /// <param name="sourceDataLeft"></param>
        /// <param name="destData"></param>
        public static void ChangePitchSemitones(int sampleRate, int channelCount, float semiTones, ref float[] sourceData, ref float[] destData, int sequenceMs = 0, int seekWindowMs = 0, int overlapMs = 8)
        {
            int BUFFER_SIZE = 1000;

            SoundTouch soundTouch = new SoundTouch();

            Global.Buzz.DCWriteLine(String.Format("SoundTouch Version {0}", soundTouch.VersionString));
            Global.Buzz.DCWriteLine(String.Format("Use QuickSeek: {0}", soundTouch.GetUseQuickSeek()));
            Global.Buzz.DCWriteLine(String.Format("Use AntiAliasing: {0}", soundTouch.GetUseAntiAliasing()));
            Global.Buzz.DCWriteLine(String.Format("AaFilterLength (ms): {0}", soundTouch.AaFilterLength()));
            Global.Buzz.DCWriteLine(String.Format("Overlap (ms): {0}", soundTouch.GetOverlapMs()));
            Global.Buzz.DCWriteLine(String.Format("Seek Window (ms): {0}", soundTouch.GetSeekWindowMs()));
            Global.Buzz.DCWriteLine(String.Format("Sequence (ms): {0}", soundTouch.GetSequenceMs()));

            soundTouch.SetSampleRate(sampleRate);
            soundTouch.SetChannels(channelCount);
            soundTouch.SetPitchSemitones(semiTones);

            soundTouch.SetSequenceMs(sequenceMs);
            soundTouch.SetSeekWindowMs(seekWindowMs);
            soundTouch.SetOverlapMs(overlapMs);

            int totalInputSampleCount = 0;
            int totalOutputSampleCount = 0;
            float[] inputBuffer = new float[BUFFER_SIZE];
            int inputBufferFillLevel = 0;
            float[] outputBuffer = new float[BUFFER_SIZE];
            int outputBufferFillAmount;

            do
            {
                if (inputBufferFillLevel == 0)
                {
                    // Refill input buffer
                    inputBufferFillLevel = Math.Min(BUFFER_SIZE, sourceData.Length - totalInputSampleCount);
                    Array.Copy(sourceData, totalInputSampleCount, inputBuffer, 0, inputBufferFillLevel);
                }

                soundTouch.PutSamples(inputBuffer, inputBufferFillLevel / channelCount);
                totalInputSampleCount += inputBufferFillLevel;

                outputBufferFillAmount = Math.Min(BUFFER_SIZE / channelCount, (destData.Length - totalOutputSampleCount) / channelCount);
                outputBufferFillAmount = Math.Min(outputBufferFillAmount, soundTouch.NumberOfSamplesAvailable);
                outputBufferFillAmount = soundTouch.ReceiveSamples(outputBuffer, outputBufferFillAmount);

                Array.Copy(outputBuffer, 0, destData, totalOutputSampleCount, outputBufferFillAmount * channelCount);

                totalOutputSampleCount += outputBufferFillAmount * channelCount;

                inputBufferFillLevel = 0;
                Thread.Sleep(0); // Needed?
            }
            while (sourceData.Length - totalInputSampleCount > 0 || outputBufferFillAmount > 0);

            soundTouch.Dispose();

        }

        public static void ChangeTempo(IWavetable wt, int wavetableIndex, float changePercent, int sequenceMs = 0, int seekWindowMs = 0, int overlapMs = 8)
        {
            bool playing = Global.Buzz.Playing;
            Global.Buzz.Playing = false;

            var fromLayer = wt.Waves[wavetableIndex].Layers.Last();
            int sampleRate = fromLayer.SampleRate;

            int sampleCount = fromLayer.SampleCount;
            string name = wt.Waves[wavetableIndex].Name;
            string path = fromLayer.Path;
            int rootNote = fromLayer.RootNote;
            int loopStart = (int)(fromLayer.LoopStart * ((100.0 - changePercent) / 100.0));
            int loopEnd = (int)(fromLayer.LoopEnd * ((100.0 - changePercent) / 100.0));

            double targetBufferSize = sampleCount * ((100.0 - changePercent) / 100.0);

            if (fromLayer.ChannelCount == 1)
            {
                float[] sourceData = new float[sampleCount];
                float[] destData = new float[(int)(targetBufferSize)];
                fromLayer.GetDataAsFloat(sourceData, 0, 1, 0, 0, sampleCount); // Mono

                ChangeTempo(sampleRate, fromLayer.ChannelCount, changePercent, ref sourceData, ref destData);
                var tw = new TemporaryWave(destData, WaveFormat.Float32, sampleRate, rootNote, path, name);

                var targetLayer = fromLayer;
                WaveCommandHelpers.ReplaceLayer(wt, wavetableIndex, WaveCommandHelpers.GetLayerIndex(targetLayer), tw);
                targetLayer.LoopStart = loopStart;
                targetLayer.LoopEnd = loopEnd;

                targetLayer.InvalidateData();
            }
            else if (fromLayer.ChannelCount == 2)
            {
                float[] sourceData = new float[sampleCount * 2];
                float[] destData = new float[(int)(targetBufferSize * 2)];

                fromLayer.GetDataAsFloat(sourceData, 0, 2, 0, 0, sampleCount); // Left
                fromLayer.GetDataAsFloat(sourceData, 1, 2, 1, 0, sampleCount); // Right
                ChangeTempo(sampleRate, fromLayer.ChannelCount, changePercent, ref sourceData, ref destData);
                var tw = TemporaryWaveFromStereo(destData, WaveFormat.Float32, sampleRate, rootNote, path, name);

                var targetLayer = fromLayer;
                WaveCommandHelpers.ReplaceLayer(wt, wavetableIndex, WaveCommandHelpers.GetLayerIndex(targetLayer), tw);
                targetLayer.LoopStart = loopStart;
                targetLayer.LoopEnd = loopEnd;

                targetLayer.InvalidateData();
            }

            Global.Buzz.Playing = playing;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="soundTouch"></param>
        /// <param name="sourceDataLeft"></param>
        /// <param name="destData"></param>
        public static void ChangeTempo(int sampleRate, int channelCount, float changePercent, ref float[] sourceData, ref float[] destData, int sequenceMs = 0, int seekWindowMs = 0, int overlapMs = 8)
        {
            int BUFFER_SIZE = 1000;

            SoundTouch soundTouch = new SoundTouch();

            Global.Buzz.DCWriteLine(String.Format("SoundTouch Version {0}", soundTouch.VersionString));
            Global.Buzz.DCWriteLine(String.Format("Use QuickSeek: {0}", soundTouch.GetUseQuickSeek()));
            Global.Buzz.DCWriteLine(String.Format("Use AntiAliasing: {0}", soundTouch.GetUseAntiAliasing()));

            soundTouch.SetSampleRate(sampleRate);
            soundTouch.SetChannels(channelCount);
            soundTouch.SetTempoChange(changePercent);

            soundTouch.SetSequenceMs(sequenceMs);
            soundTouch.SetSeekWindowMs(seekWindowMs);
            soundTouch.SetOverlapMs(overlapMs);

            int totalInputSampleCount = 0;
            int totalOutputSampleCount = 0;
            float[] inputBuffer = new float[BUFFER_SIZE];
            int inputBufferFillLevel = 0;
            float[] outputBuffer = new float[BUFFER_SIZE];
            int outputBufferFillAmount;

            do
            {
                if (inputBufferFillLevel == 0)
                {
                    // Refill input buffer
                    inputBufferFillLevel = Math.Min(BUFFER_SIZE, sourceData.Length - totalInputSampleCount);
                    Array.Copy(sourceData, totalInputSampleCount, inputBuffer, 0, inputBufferFillLevel);
                }

                soundTouch.PutSamples(inputBuffer, inputBufferFillLevel / channelCount);
                totalInputSampleCount += inputBufferFillLevel;

                outputBufferFillAmount = Math.Min(BUFFER_SIZE / channelCount, (destData.Length - totalOutputSampleCount) / channelCount);
                outputBufferFillAmount = Math.Min(outputBufferFillAmount, soundTouch.NumberOfSamplesAvailable);
                outputBufferFillAmount = soundTouch.ReceiveSamples(outputBuffer, outputBufferFillAmount);

                Array.Copy(outputBuffer, 0, destData, totalOutputSampleCount, outputBufferFillAmount * channelCount);

                totalOutputSampleCount += outputBufferFillAmount * channelCount;

                inputBufferFillLevel = 0;
                Thread.Sleep(0); // Needed?
            }
            while (sourceData.Length - totalInputSampleCount > 0 || outputBufferFillAmount > 0);

            soundTouch.Dispose();
        }

        public static void ChangeRate(IWavetable wt, int wavetableIndex, float changePercent, int sequenceMs = 0, int seekWindowMs = 0, int overlapMs = 8)
        {
            bool playing = Global.Buzz.Playing;
            Global.Buzz.Playing = false;

            var fromLayer = wt.Waves[wavetableIndex].Layers.Last();
            int sampleRate = fromLayer.SampleRate;

            int sampleCount = fromLayer.SampleCount;
            string name = wt.Waves[wavetableIndex].Name;
            string path = fromLayer.Path;
            int rootNote = fromLayer.RootNote;
            int loopStart = (int)(fromLayer.LoopStart * ((100.0 - changePercent) / 100.0));
            int loopEnd = (int)(fromLayer.LoopEnd * ((100.0 - changePercent) / 100.0));

            double targetBufferSize = sampleCount * ((100.0 - changePercent) / 100.0);

            if (fromLayer.ChannelCount == 1)
            {
                float[] sourceData = new float[sampleCount];
                float[] destData = new float[(int)(targetBufferSize)];
                fromLayer.GetDataAsFloat(sourceData, 0, 1, 0, 0, sampleCount); // Mono

                ChangeRate(sampleRate, fromLayer.ChannelCount, changePercent, ref sourceData, ref destData, sequenceMs, seekWindowMs, overlapMs);
                var tw = new TemporaryWave(destData, WaveFormat.Float32, sampleRate, rootNote, path, name);
                var targetLayer = fromLayer;
                WaveCommandHelpers.ReplaceLayer(wt, wavetableIndex, WaveCommandHelpers.GetLayerIndex(targetLayer), tw);

                targetLayer.LoopStart = loopStart;
                targetLayer.LoopEnd = loopEnd;
                targetLayer.InvalidateData();
            }
            else if (fromLayer.ChannelCount == 2)
            {
                float[] sourceData = new float[sampleCount * 2];
                float[] destData = new float[(int)(targetBufferSize * 2)];

                fromLayer.GetDataAsFloat(sourceData, 0, 2, 0, 0, sampleCount); // Left
                fromLayer.GetDataAsFloat(sourceData, 1, 2, 1, 0, sampleCount); // Right
                ChangeRate(sampleRate, fromLayer.ChannelCount, changePercent, ref sourceData, ref destData, sequenceMs, seekWindowMs, overlapMs);
                var tw = TemporaryWaveFromStereo(destData, WaveFormat.Float32, sampleRate, rootNote, path, name);

                var targetLayer = fromLayer;
                WaveCommandHelpers.ReplaceLayer(wt, wavetableIndex, WaveCommandHelpers.GetLayerIndex(targetLayer), tw);
                targetLayer.LoopStart = loopStart;
                targetLayer.LoopEnd = loopEnd;
                targetLayer.InvalidateData();
            }

            Global.Buzz.Playing = playing;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="soundTouch"></param>
        /// <param name="sourceDataLeft"></param>
        /// <param name="destData"></param>
        public static void ChangeRate(int sampleRate, int channelCount, float changePercent, ref float[] sourceData, ref float[] destData, int sequenceMs = 0, int seekWindowMs = 0, int overlapMs = 8)
        {
            int BUFFER_SIZE = 1000;
            SoundTouch soundTouch = new SoundTouch();

            Global.Buzz.DCWriteLine(String.Format("SoundTouch Version {0}", soundTouch.VersionString));
            Global.Buzz.DCWriteLine(String.Format("Use QuickSeek: {0}", soundTouch.GetUseQuickSeek()));
            Global.Buzz.DCWriteLine(String.Format("Use AntiAliasing: {0}", soundTouch.GetUseAntiAliasing()));

            soundTouch.SetSampleRate(sampleRate);
            soundTouch.SetChannels(channelCount);
            soundTouch.SetRateChange(changePercent);

            soundTouch.SetSequenceMs(sequenceMs);
            soundTouch.SetSeekWindowMs(seekWindowMs);
            soundTouch.SetOverlapMs(overlapMs);

            int totalInputSampleCount = 0;
            int totalOutputSampleCount = 0;
            float[] inputBuffer = new float[BUFFER_SIZE];
            int inputBufferFillLevel = 0;
            float[] outputBuffer = new float[BUFFER_SIZE];
            int outputBufferFillAmount;

            do
            {
                if (inputBufferFillLevel == 0)
                {
                    // Refill input buffer
                    inputBufferFillLevel = Math.Min(BUFFER_SIZE, sourceData.Length - totalInputSampleCount);
                    Array.Copy(sourceData, totalInputSampleCount, inputBuffer, 0, inputBufferFillLevel);
                }

                soundTouch.PutSamples(inputBuffer, inputBufferFillLevel / channelCount);
                totalInputSampleCount += inputBufferFillLevel;

                outputBufferFillAmount = Math.Min(BUFFER_SIZE / channelCount, (destData.Length - totalOutputSampleCount) / channelCount);
                outputBufferFillAmount = Math.Min(outputBufferFillAmount, soundTouch.NumberOfSamplesAvailable);
                outputBufferFillAmount = soundTouch.ReceiveSamples(outputBuffer, outputBufferFillAmount);

                Array.Copy(outputBuffer, 0, destData, totalOutputSampleCount, outputBufferFillAmount * channelCount);

                totalOutputSampleCount += outputBufferFillAmount * channelCount;

                inputBufferFillLevel = 0;
                Thread.Sleep(0); // Needed?
            }
            while (sourceData.Length - totalInputSampleCount > 0 || outputBufferFillAmount > 0);

            soundTouch.Dispose();
        }

        public static float DetectBpm(IWavetable wt, int wavetableIndex, int layer)
        {
            float ret = 0;
            bool playing = Global.Buzz.Playing;
            Global.Buzz.Playing = false;

            var fromLayer = wt.Waves[wavetableIndex].Layers[layer];
            int sampleRate = fromLayer.SampleRate;
            int sampleCount = fromLayer.SampleCount;

            if (fromLayer.ChannelCount == 1)
            {
                float[] sourceData = new float[sampleCount];
                fromLayer.GetDataAsFloat(sourceData, 0, 1, 0, 0, sampleCount); // Mono

                ret = DetectBpm(sampleRate, ref sourceData);

            }
            else if (fromLayer.ChannelCount == 2)
            {
                float[] sourceDataLeft = new float[sampleCount];
                float[] sourceDataRight = new float[sampleCount];
                fromLayer.GetDataAsFloat(sourceDataLeft, 0, 1, 0, 0, sampleCount); // Left
                fromLayer.GetDataAsFloat(sourceDataRight, 0, 1, 1, 0, sampleCount); // Right

                // Convert to mono
                for (int i = 0; i < sampleCount; i++)
                    sourceDataLeft[i] = (sourceDataLeft[i] + sourceDataRight[i]) / 2.0f;

                ret = DetectBpm(sampleRate, ref sourceDataLeft);
            }
            Global.Buzz.Playing = playing;

            return ret;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="soundTouch"></param>
        /// <param name="sourceDataLeft"></param>
        /// <param name="destData"></param>
        public static float DetectBpm(int sampleRate, ref float[] sourceData)
        {
            float ret = 0;
            BPMDetect bpmDetect = new BPMDetect(1, sampleRate);

            int totalInputSampleCount = 0;

            float[] inputBuffer = new float[1000];
            int inputBufferFillLevel = 0;

            do
            {
                if (inputBufferFillLevel == 0)
                {
                    // Refill input buffer
                    inputBufferFillLevel = Math.Min(1000, sourceData.Length - totalInputSampleCount);
                    Array.Copy(sourceData, totalInputSampleCount, inputBuffer, 0, inputBufferFillLevel);
                }

                bpmDetect.PutSamples(inputBuffer, (uint)inputBufferFillLevel);
                totalInputSampleCount += inputBufferFillLevel;

                inputBufferFillLevel = 0;
                Thread.Sleep(0); // Needed?
            }
            while (sourceData.Length - totalInputSampleCount > 0);

            ret = bpmDetect.Bpm;

            bpmDetect.Dispose();

            return ret;
        }

        internal static ResourceDictionary GetBuzzThemeResources()
        {
            ResourceDictionary skin = new ResourceDictionary();

            try
            {
                string selectedTheme = Global.Buzz.SelectedTheme == "<default>" ? "Default" : Global.Buzz.SelectedTheme;
                string skinPath = Global.BuzzPath + "\\Themes\\" + selectedTheme + "\\MachineView\\MVResources.xaml";

                skin.Source = new Uri(skinPath, UriKind.Absolute);
            }
            catch (Exception)
            {
                try
                {
                    string skinPath = Global.BuzzPath + "\\Themes\\Default\\MachineView\\MVResources.xaml";
                    skin.Source = new Uri(skinPath, UriKind.Absolute);
                }
                catch { }
            }

            return skin;
        }
    }
}
