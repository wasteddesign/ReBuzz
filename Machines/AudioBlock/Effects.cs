using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using DSPLib.SoundTouch;
using System;
using System.Linq;
using System.Threading;
using WDE.AudioBlock.r8brain;
using WDE.AudioBlock.NoiseRemoval;
using System.Windows.Controls;

namespace WDE.AudioBlock
{
    public static class Effects
    {
        public static void RNNoiseRemoval(AudioBlock ab, int wavetableIndex, string rnn_filename)
        {
            bool playing = Global.Buzz.Playing;
            Global.Buzz.Playing = false;

            IWavetable wt = ab.host.Machine.Graph.Buzz.Song.Wavetable;
            var fromLayer = wt.Waves[wavetableIndex].Layers.Last();

            int inputRate = fromLayer.SampleRate;
            int outputRate = fromLayer.SampleRate;

            int sampleCount = fromLayer.SampleCount;
            string name = wt.Waves[wavetableIndex].Name;
            string path = fromLayer.Path;
            int rootNote = fromLayer.RootNote;
            int loopStart = fromLayer.LoopStart;
            int loopEnd = fromLayer.LoopEnd;

            if (fromLayer.ChannelCount == 1)
            {
                float[] sourceData = new float[sampleCount];
                float[] destData = new float[sampleCount];
                fromLayer.GetDataAsFloat(sourceData, 0, 1, 0, 0, sampleCount); // Mono

                DoRNNoiseRemoval(ref sourceData, ref destData, outputRate, inputRate, rnn_filename);

                wt.AllocateWave(wavetableIndex, path, name, destData.Length, WaveFormat.Float32, false, rootNote, false, false);
                var targetLayer = wt.Waves[wavetableIndex].Layers.Last();

                targetLayer.LoopStart = loopStart;
                targetLayer.LoopEnd = loopEnd;
                targetLayer.SampleRate = (int)outputRate;

                targetLayer.SetDataAsFloat(destData, 0, 1, 0, 0, destData.Length); // Mono                    
                targetLayer.InvalidateData();
            }
            else if (fromLayer.ChannelCount == 2)
            {
                float[] sourceDataLeft = new float[sampleCount];
                float[] sourceDataRight = new float[sampleCount];
                float[] destData = new float[sampleCount];
                fromLayer.GetDataAsFloat(sourceDataLeft, 0, 1, 0, 0, sampleCount); // Left
                fromLayer.GetDataAsFloat(sourceDataRight, 0, 1, 1, 0, sampleCount); // Right

                wt.AllocateWave(wavetableIndex, path, name, destData.Length, WaveFormat.Float32, true, rootNote, false, false);
                var targetLayer = wt.Waves[wavetableIndex].Layers.Last();

                DoRNNoiseRemoval(ref sourceDataLeft, ref destData, outputRate, inputRate, rnn_filename);
                sourceDataLeft = null;
                targetLayer.SetDataAsFloat(destData, 0, 1, 0, 0, destData.Length); // Left 

                DoRNNoiseRemoval(ref sourceDataRight, ref destData, outputRate, inputRate, rnn_filename);
                targetLayer.SetDataAsFloat(destData, 0, 1, 1, 0, destData.Length); // Right 

                targetLayer.LoopStart = loopStart;
                targetLayer.LoopEnd = loopEnd;
                targetLayer.SampleRate = outputRate;

                targetLayer.InvalidateData();
            }

            ab.ResetRealtimeResamplersFlag = true;
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

        /// <summary>
        /// Resample audio in wavetable to Buzz samplerate using libsamplerate. Improves sound quality since AudioBlock does not need to do linear interpolatio from samples.
        /// </summary>
        /// <param name="wavetableIndex"></param>
        public static void Resample(AudioBlock ab, int wavetableIndex)
        {
            bool playing = Global.Buzz.Playing;
            Global.Buzz.Playing = false;

            IWavetable wt = ab.host.Machine.Graph.Buzz.Song.Wavetable;
            var fromLayer = wt.Waves[wavetableIndex].Layers.Last();

            double inputRate = fromLayer.SampleRate;
            double outputRate = Global.Buzz.SelectedAudioDriverSampleRate;

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

                    wt.AllocateWave(wavetableIndex, path, name, destData.Length, WaveFormat.Float32, false, rootNote, false, false);
                    var targetLayer = wt.Waves[wavetableIndex].Layers.Last();

                    targetLayer.LoopStart = loopStart;
                    targetLayer.LoopEnd = loopEnd;
                    targetLayer.SampleRate = (int)outputRate;

                    targetLayer.SetDataAsFloat(destData, 0, 1, 0, 0, destData.Length); // Mono                    
                    targetLayer.InvalidateData();
                }
                else if (fromLayer.ChannelCount == 2)
                {
                    float[] sourceDataLeft = new float[sampleCount];
                    float[] sourceDataRight = new float[sampleCount];
                    float[] destData = new float[(int)(sampleCount * outputRate / inputRate)];
                    fromLayer.GetDataAsFloat(sourceDataLeft, 0, 1, 0, 0, sampleCount); // Left
                    fromLayer.GetDataAsFloat(sourceDataRight, 0, 1, 1, 0, sampleCount); // Right

                    wt.AllocateWave(wavetableIndex, path, name, destData.Length, WaveFormat.Float32, true, rootNote, false, false);
                    var targetLayer = wt.Waves[wavetableIndex].Layers.Last();

                    Resample(ref sourceDataLeft, ref destData, outputRate, inputRate);
                    sourceDataLeft = null;
                    targetLayer.SetDataAsFloat(destData, 0, 1, 0, 0, destData.Length); // Left 

                    Resample(ref sourceDataRight, ref destData, outputRate, inputRate);
                    targetLayer.SetDataAsFloat(destData, 0, 1, 1, 0, destData.Length); // Right 

                    targetLayer.LoopStart = loopStart;
                    targetLayer.LoopEnd = loopEnd;
                    targetLayer.SampleRate = (int)outputRate;

                    targetLayer.InvalidateData();
                }
            }

            ab.ResetRealtimeResamplersFlag = true;
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
            var r8bL = new r8brain.R8brain();
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wavetableIndex"></param>
        /// <param name="semiTones"></param>
        public static void ChangePitchSemitones(AudioBlock ab, int wavetableIndex, float semiTones, int sequenceMs = 0, int seekWindowMs = 0, int overlapMs = 8)
        {
            bool playing = Global.Buzz.Playing;
            Global.Buzz.Playing = false;

            IWavetable wt = ab.host.Machine.Graph.Buzz.Song.Wavetable;
            var fromLayer = wt.Waves[wavetableIndex].Layers.Last();

            int sampleRate = fromLayer.SampleRate;
            
            int sampleCount = fromLayer.SampleCount;
            string name = wt.Waves[wavetableIndex].Name;
            string path = fromLayer.Path;
            int rootNote = fromLayer.RootNote;
            int loopStart = fromLayer.LoopStart;
            int loopEnd = fromLayer.LoopEnd;

            if (fromLayer.ChannelCount == 1)
            {
                float[] sourceData = new float[sampleCount];
                float[] destData = new float[(int)(sampleCount)];
                fromLayer.GetDataAsFloat(sourceData, 0, 1, 0, 0, sampleCount); // Mono

                ChangePitchSemitones(sampleRate, fromLayer.ChannelCount, semiTones, ref sourceData, ref destData);

                wt.AllocateWave(wavetableIndex, path, name, destData.Length, WaveFormat.Float32, false, rootNote, false, true);
                var targetLayer = wt.Waves[wavetableIndex].Layers.Last();

                targetLayer.LoopStart = loopStart;
                targetLayer.LoopEnd = loopEnd;
                targetLayer.SampleRate = (int)sampleRate;

                targetLayer.SetDataAsFloat(destData, 0, 1, 0, 0, destData.Length); // Mono                    
                targetLayer.InvalidateData(); // There is going to be one event when creating wave

            }
            else if (fromLayer.ChannelCount == 2)
            {
                float[] sourceData = new float[sampleCount * 2];
                float[] destData = new float[(int)(sampleCount * 2)];

                fromLayer.GetDataAsFloat(sourceData, 0, 2, 0, 0, sampleCount); // Left
                fromLayer.GetDataAsFloat(sourceData, 1, 2, 1, 0, sampleCount); // Right

                wt.AllocateWave(wavetableIndex, path, name, destData.Length / 2, WaveFormat.Float32, true, rootNote, false, true);
                var targetLayer = wt.Waves[wavetableIndex].Layers.Last();

                ChangePitchSemitones(sampleRate, fromLayer.ChannelCount, semiTones, ref sourceData, ref destData);

                targetLayer.SetDataAsFloat(destData, 0, 2, 0, 0, destData.Length / 2); // Left 
                targetLayer.SetDataAsFloat(destData, 1, 2, 1, 0, destData.Length / 2); // Right 

                targetLayer.LoopStart = loopStart;
                targetLayer.LoopEnd = loopEnd;
                targetLayer.SampleRate = (int)sampleRate;

                targetLayer.InvalidateData();
            }

            ab.ResetRealtimeResamplersFlag = true;
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
            }
            while (sourceData.Length - totalInputSampleCount > 0 || outputBufferFillAmount > 0);

            soundTouch.Dispose();

        }

        public static void ChangeTempo(AudioBlock ab, int wavetableIndex, float changePercent, int sequenceMs = 0, int seekWindowMs = 0, int overlapMs = 8)
        {
            bool playing = Global.Buzz.Playing;
            Global.Buzz.Playing = false;

            IWavetable wt = ab.host.Machine.Graph.Buzz.Song.Wavetable;
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

                wt.AllocateWave(wavetableIndex, path, name, destData.Length, WaveFormat.Float32, false, rootNote, false, true);
                var targetLayer = wt.Waves[wavetableIndex].Layers.Last();

                targetLayer.LoopStart = loopStart;
                targetLayer.LoopEnd = loopEnd;
                targetLayer.SampleRate = (int)sampleRate;

                targetLayer.SetDataAsFloat(destData, 0, 1, 0, 0, destData.Length); // Mono                    
                targetLayer.InvalidateData();
            }
            else if (fromLayer.ChannelCount == 2)
            {
                float[] sourceData = new float[sampleCount * 2];
                float[] destData = new float[(int)(targetBufferSize * 2)];

                fromLayer.GetDataAsFloat(sourceData, 0, 2, 0, 0, sampleCount); // Left
                fromLayer.GetDataAsFloat(sourceData, 1, 2, 1, 0, sampleCount); // Right

                wt.AllocateWave(wavetableIndex, path, name, destData.Length / 2, WaveFormat.Float32, true, rootNote, false, true);
                var targetLayer = wt.Waves[wavetableIndex].Layers.Last();

                ChangeTempo(sampleRate, fromLayer.ChannelCount, changePercent, ref sourceData, ref destData);

                targetLayer.SetDataAsFloat(destData, 0, 2, 0, 0, destData.Length / 2); // Left 
                targetLayer.SetDataAsFloat(destData, 1, 2, 1, 0, destData.Length / 2); // Right 

                targetLayer.LoopStart = loopStart;
                targetLayer.LoopEnd = loopEnd;
                targetLayer.SampleRate = (int)sampleRate;

                targetLayer.InvalidateData();
            }

            ab.ResetRealtimeResamplersFlag = true;
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
            }
            while (sourceData.Length - totalInputSampleCount > 0 || outputBufferFillAmount > 0);

            soundTouch.Dispose();
        }

        public static void ChangeRate(AudioBlock ab, int wavetableIndex, float changePercent, int sequenceMs = 0, int seekWindowMs = 0, int overlapMs = 8)
        {
            bool playing = Global.Buzz.Playing;
            Global.Buzz.Playing = false;

            IWavetable wt = ab.host.Machine.Graph.Buzz.Song.Wavetable;
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

                wt.AllocateWave(wavetableIndex, path, name, destData.Length, WaveFormat.Float32, false, rootNote, false, true);
                var targetLayer = wt.Waves[wavetableIndex].Layers.Last();

                targetLayer.LoopStart = loopStart;
                targetLayer.LoopEnd = loopEnd;
                targetLayer.SampleRate = (int)sampleRate;

                targetLayer.SetDataAsFloat(destData, 0, 1, 0, 0, destData.Length); // Mono                    
                targetLayer.InvalidateData();
            }
            else if (fromLayer.ChannelCount == 2)
            {
                float[] sourceData = new float[sampleCount * 2];
                float[] destData = new float[(int)(targetBufferSize * 2)];

                fromLayer.GetDataAsFloat(sourceData, 0, 2, 0, 0, sampleCount); // Left
                fromLayer.GetDataAsFloat(sourceData, 1, 2, 1, 0, sampleCount); // Right

                wt.AllocateWave(wavetableIndex, path, name, destData.Length / 2, WaveFormat.Float32, true, rootNote, false, true);
                var targetLayer = wt.Waves[wavetableIndex].Layers.Last();

                ChangeRate(sampleRate, fromLayer.ChannelCount, changePercent, ref sourceData, ref destData, sequenceMs, seekWindowMs, overlapMs);

                targetLayer.SetDataAsFloat(destData, 0, 2, 0, 0, destData.Length / 2); // Left 
                targetLayer.SetDataAsFloat(destData, 1, 2, 1, 0, destData.Length / 2); // Right 

                targetLayer.LoopStart = loopStart;
                targetLayer.LoopEnd = loopEnd;
                targetLayer.SampleRate = (int)sampleRate;

                targetLayer.InvalidateData();
            }

            ab.ResetRealtimeResamplersFlag = true;
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
            }
            while (sourceData.Length - totalInputSampleCount > 0 || outputBufferFillAmount > 0);

            soundTouch.Dispose();
        }

        public static float DetectBpm(AudioBlock ab, int wavetableIndex)
        {
            float ret = 0;
            bool playing = Global.Buzz.Playing;
            Global.Buzz.Playing = false;

            IWavetable wt = ab.host.Machine.Graph.Buzz.Song.Wavetable;
            var fromLayer = wt.Waves[wavetableIndex].Layers.Last();
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
        public static float DetectBpm(int sampleRate, ref float[] sourceData )
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
            }
            while (sourceData.Length - totalInputSampleCount > 0);

            ret = bpmDetect.Bpm;

            bpmDetect.Dispose();

            return ret;
        }
    }
}
