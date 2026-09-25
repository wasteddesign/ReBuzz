using BuzzDotNet.Audio;
using ReBuzz.Audio.Engine;
using ReBuzz.Common;
using ReBuzz.Core;

namespace ReBuzz.Audio
{
    internal interface IAudioDriver
    {
        void Initialize();
        void Start();
        void Stop();
        void Reset();
        void Dispose();
        void ShowControlPanel();

        int SampleRate { get; }
        int BufferSize { get; }
        IReBuzzAudioProvider Provider { get; }
        AudioEngine.AudioOutDevice DeviceInfo { get; }
        AudioEngine.AudioInDevice InputDeviceInfo { get; }
    }
}