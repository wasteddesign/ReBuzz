using BuzzGUI.Common.Settings;
using ReBuzz.Audio.Engine;
using ReBuzz.Core;
using System;

namespace ReBuzz.Audio
{
    internal static class AudioDriverFactory
    {
        public static IAudioDriver Create(
            AudioEngine.AudioOutType type,
            string deviceName,
            ReBuzzCore buzzCore,
            EngineSettings settings,
            IRegistryEx registryEx)
        {
            return type switch
            {
                AudioEngine.AudioOutType.ASIO =>
                    new AsioAudioDriver(deviceName, buzzCore, settings, registryEx),

                AudioEngine.AudioOutType.Wasapi =>
                    new WasapiAudioDriver(deviceName, buzzCore, settings, registryEx),

                _ => throw new NotSupportedException($"Unsupported audio driver type: {type}")
            };
        }
    }
}