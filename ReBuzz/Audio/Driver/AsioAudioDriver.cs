using BuzzDotNet.Audio;
using BuzzGUI.Common.Settings;
using NAudio.Wave;
using ReBuzz.Audio.Engine;
using ReBuzz.Common;
using ReBuzz.Core;
using System;

namespace ReBuzz.Audio
{
    internal sealed class AsioAudioDriver : IAudioDriver
    {
        private readonly string deviceName;
        private readonly ReBuzzCore buzzCore;
        private readonly EngineSettings engineSettings;
        private readonly IRegistryEx registryEx;

        private AsioDevice asioDevice;
        private AudioWaveProvider audioWaveProvider;

        private float[] asioBufferOut = new float[1024 * 16 * 32];
        private float[] asioBufferIn = new float[1024 * 16 * 32];

        public int SampleRate { get; private set; }
        public int BufferSize { get; private set; }

        public IReBuzzAudioProvider Provider => audioWaveProvider;
        public AudioEngine.AudioOutDevice DeviceInfo { get; private set; }
        public AudioEngine.AudioInDevice InputDeviceInfo { get; private set; }

        public AsioAudioDriver(
            string deviceName,
            ReBuzzCore buzzCore,
            EngineSettings settings,
            IRegistryEx registryEx)
        {
            this.deviceName = deviceName;
            this.buzzCore = buzzCore;
            this.engineSettings = settings;
            this.registryEx = registryEx;
        }

        public void Initialize()
        {
            asioDevice = AsioDevice.Open(deviceName);
            DeviceInfo = new AudioEngine.AudioOutDevice
            {
                Name = deviceName,
                Type = AudioEngine.AudioOutType.ASIO,
                WavePlayer = asioDevice
            };

            BufferSize = registryEx.Read("BufferSize", 1024, "ASIO");
            SampleRate = registryEx.Read("SampleRate", 44100, "ASIO");

            audioWaveProvider = new AudioWaveProvider(
                buzzCore,
                SampleRate,
                asioDevice.Capabilities.AllOutputChannels.Length,
                BufferSize,
                true,
                registryEx,
                engineSettings);

            asioBufferIn = new float[BufferSize * asioDevice.Capabilities.AllInputChannels.Length];
            asioBufferOut = new float[BufferSize * asioDevice.Capabilities.AllOutputChannels.Length];

            InitDuplexWithFallback();

            asioDevice.DriverResetRequest += AsioOut_DriverResetRequest;

            InputDeviceInfo = new AudioEngine.AudioInDevice
            {
                Name = deviceName,
                Type = AudioEngine.AudioOutType.ASIO,
                WaveFormat = audioWaveProvider.WaveFormat
            };
        }

        private void InitDuplexWithFallback()
        {
            try
            {
                asioDevice.InitDuplex(new AsioDuplexOptions
                {
                    InputChannels = asioDevice.Capabilities.AllInputChannels,
                    OutputChannels = asioDevice.Capabilities.AllOutputChannels,
                    SampleRate = SampleRate,
                    BufferSize = BufferSize,
                    Processor = ProcessBuffers
                });
            }
            catch (Exception ex)
            {
                buzzCore.DCWriteLine(
                    "Error initializing ASIO. Using audio interface preferred buffer size (" +
                    asioDevice.Capabilities.BufferPreferredSize + ").\n\n" + ex.Message,
                    BuzzGUI.Interfaces.DCLogLevel.Error);
            }

            if (asioDevice.State == AsioDeviceState.Unconfigured)
            {
                try
                {
                    asioDevice.InitDuplex(new AsioDuplexOptions
                    {
                        InputChannels = asioDevice.Capabilities.AllInputChannels,
                        OutputChannels = asioDevice.Capabilities.AllOutputChannels,
                        SampleRate = SampleRate,
                        BufferSize = null,
                        Processor = ProcessBuffers
                    });
                }
                catch (Exception ex)
                {
                    buzzCore.DCWriteLine(
                        "Can't initialize ASIO using preferred buffer size (" +
                        asioDevice.Capabilities.BufferPreferredSize + ").\n\n" + ex.Message,
                        BuzzGUI.Interfaces.DCLogLevel.Error);

                    BespokeFusion.MessageBoxWindow.ShowOkWindow(
                        "ASIO Error",
                        "Can't initialize ASIO using preferred buffer size (" +
                        asioDevice.Capabilities.BufferPreferredSize + ").\n\n" + ex.Message,
                        false);
                }
            }
        }

        private void ProcessBuffers(in AsioProcessBuffers b)
        {
            AsioDuplexAudioAvailable(b);
            AsioDuplexOutput(b);
        }

        private void AsioDuplexAudioAvailable(AsioProcessBuffers b)
        {
            int channels = b.InputChannelCount;
            int frames = b.Frames;

            if (channels <= 0 || frames <= 0)
                return;

            int j = 0;
            for (int i = 0; i < frames; i++)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    asioBufferIn[j++] = b.GetInput(ch)[i];
                }
            }

            buzzCore.AudioInputAvalable(asioBufferIn, frames, channels);
        }

        private void AsioDuplexOutput(AsioProcessBuffers b)
        {
            int frames = b.Frames;
            int channels = b.OutputChannelCount;

            if (asioBufferOut.Length < frames * channels)
            {
                asioBufferOut = new float[frames * channels];
            }

            Span<float> interleaved = asioBufferOut.AsSpan(0, frames * channels);
            audioWaveProvider.Read(interleaved);

            for (int c = 0; c < channels; c++)
            {
                var dest = b.GetOutput(c);
                int src = c;

                for (int i = 0; i < frames; i++)
                {
                    dest[i] = interleaved[src];
                    src += channels;
                }
            }
        }

        private void AsioOut_DriverResetRequest(object sender, EventArgs e)
        {
            ReBuzzCore.RecordDriverReset();
            Reset();
        }

        public void Start()
        {
            asioDevice?.Start();
        }

        public void Stop()
        {
            asioDevice?.Stop();
        }

        public void Reset()
        {
            Stop();
            Dispose();
            Initialize();
            Start();
        }

        public void ShowControlPanel()
        {
            asioDevice?.ShowControlPanel();
        }

        public void Dispose()
        {
            if (asioDevice != null)
            {
                asioDevice.DriverResetRequest -= AsioOut_DriverResetRequest;
                asioDevice.Dispose();
                asioDevice = null;
            }
        }
    }
}
