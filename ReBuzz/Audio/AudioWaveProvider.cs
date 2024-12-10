using NAudio.Wave;
using ReBuzz.Core;
using System.Runtime.CompilerServices;
using BuzzGUI.Common;

namespace ReBuzz.Audio
{
    internal class AudioWaveProvider : IWaveProvider, IReBuzzAudioProvider
    {
        public WaveFormat WaveFormat { get; }

        public CommonAudioProvider AudioSampleProvider { get; }

        public AudioWaveProvider(ReBuzzCore buzzCore, int sampleRate, int channels, int bufferSize, bool doubleBuffer, IRegistryEx registryEx)
        {
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
            AudioSampleProvider = new CommonAudioProvider(buzzCore, Global.EngineSettings, sampleRate, channels, bufferSize, doubleBuffer, registryEx);
        }

        public void ClearBuffer()
        {
            AudioSampleProvider.ClearBuffer();
        }

        public int Read(byte[] byteBuffer, int byteOffset, int byteCount)
        {
            int offset = byteOffset >> 2;
            int count = byteCount >> 2;
            float[] buffer = Unsafe.As<byte[], float[]>(ref byteBuffer);

            int retCount = AudioSampleProvider.Read(buffer, offset, count);

            // Return byte count
            return retCount << 2;
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
