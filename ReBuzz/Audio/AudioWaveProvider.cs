using BuzzGUI.Common;
using BuzzGUI.Common.Settings;
using NAudio.Wave;
using ReBuzz.Core;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ReBuzz.Audio
{
    internal class AudioWaveProvider : IReBuzzAudioProvider
    {
        public WaveFormat WaveFormat { get; }

        public CommonAudioProvider AudioSampleProvider { get; }

        public AudioWaveProvider(
            ReBuzzCore buzzCore,
            int sampleRate,
            int channels,
            int bufferSize,
            bool doubleBuffer,
            IRegistryEx registryEx, 
            EngineSettings engineSettings)
        {
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
            AudioSampleProvider = new CommonAudioProvider(buzzCore, engineSettings, sampleRate, channels, bufferSize, doubleBuffer, registryEx);
        }

        public void ClearBuffer()
        {
            AudioSampleProvider.ClearBuffer();
        }

        public int Read(Span<float> floatBuffer)
        {
            int retCount = AudioSampleProvider.Read(floatBuffer);

            return retCount;
        }

        public void Stop()
        {
            AudioSampleProvider.Stop();
        }

        public int ReadOverride(float[] buffer, int offset, int count)
        {
            return AudioSampleProvider.ReadOverride(buffer, offset, count);
        }
    }
}
