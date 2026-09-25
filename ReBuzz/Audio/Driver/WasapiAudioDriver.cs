using BespokeFusion;
using BuzzGUI.Common.Settings;
using Helios.Concurrency;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using ReBuzz.Audio.Engine;
using ReBuzz.Common;
using ReBuzz.Core;
using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace ReBuzz.Audio
{
    internal sealed class WasapiAudioDriver : IAudioDriver
    {
        private readonly string deviceName;
        private readonly ReBuzzCore buzzCore;
        private readonly EngineSettings engineSettings;
        private readonly IRegistryEx registryEx;

        private WasapiPlayer wasapiPlayer;
        private WasapiRecorder wasapiRecorder;
        private RealTimeResampler audioInResampler;

        private readonly float[] audioInBuffer = new float[512];

        public int SampleRate { get; private set; }
        public int BufferSize { get; private set; }

        public IReBuzzAudioProvider Provider => audioProvider;
        private AudioProvider audioProvider;
        private AudioWaveProvider audioWaveProvider;

        public AudioEngine.AudioOutDevice DeviceInfo { get; private set; }
        public AudioEngine.AudioInDevice InputDeviceInfo { get; private set; }

        public WasapiAudioDriver(
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
            string wasapiDeviceID = registryEx.Read("DeviceID", "", "WASAPI");
            int wasapiDeviceSamplerate = registryEx.Read("SampleRate", 44100, "WASAPI");
            bool wasapiExclusiveMode = registryEx.Read("Mode", 0, "WASAPI") == 1;
            bool wasapiPollMode = registryEx.Read("Poll", 0, "WASAPI") == 1;
            BufferSize = registryEx.Read("BufferSize", 1024, "WASAPI");
            bool rawMode = registryEx.Read("RawMode", 0, "WASAPI") == 1;
            bool lowLatencyMode = registryEx.Read("LowLatencyMode", 0, "WASAPI") == 1;

            int latency = Math.Max(4, 1000 * 2 * BufferSize / wasapiDeviceSamplerate);

            var enumerator = new MMDeviceEnumerator();
            MMDevice mMDevice = enumerator
                .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                .FirstOrDefault(d => d.ID == wasapiDeviceID);

            var builder = new WasapiPlayerBuilder()
                .WithDevice(mMDevice)
                .WithLatency(latency)
                .WithMmcssThreadPriority("Pro Audio")
                .WithCategory(AudioStreamCategory.Media);

            if (wasapiExclusiveMode)
                builder = builder.WithExclusiveMode();

            if (wasapiPollMode)
                builder = builder.WithPollingSync();

            if (rawMode)
                builder = builder.WithRawMode();

            builder = builder.WithLowLatency(lowLatencyMode);

            wasapiPlayer = builder.Build();

            SampleRate = wasapiPlayer.OutputWaveFormat.SampleRate;

            var format = wasapiPlayer.GetSupportedExclusiveFormat(wasapiPlayer.OutputWaveFormat);
            if (format == null)
            {
                MessageBoxWindow.ShowOkWindow(
                    "WASAPI Error",
                    "No supported exclusive format found for this device.",
                    false);
            }

            audioProvider = new AudioProvider(
                buzzCore,
                engineSettings,
                wasapiDeviceSamplerate,
                wasapiPlayer.OutputWaveFormat.Channels,
                BufferSize,
                true,
                registryEx);

            bool success = InitWasapiOut(wasapiPlayer);
            if (!success)
            {
                wasapiPlayer = new WasapiPlayerBuilder()
                    .WithDevice(mMDevice)
                    .WithLowLatency(false)
                    .Build();

                audioProvider = new AudioProvider(
                    buzzCore,
                    engineSettings,
                    wasapiDeviceSamplerate,
                    2,
                    BufferSize,
                    true,
                    registryEx);

                success = InitWasapiOut(wasapiPlayer);
            }

            if (!success)
                return;

            wasapiPlayer.PlaybackStopped += (s, e) =>
            {
                if (e.Exception != null)
                {
                    ReBuzzCore.RecordDriverReset();
                    Reset();
                }
            };

            InitRecorder(wasapiDeviceSamplerate, wasapiExclusiveMode, wasapiPollMode, latency);

            DeviceInfo = new AudioEngine.AudioOutDevice
            {
                Name = deviceName,
                Type = AudioEngine.AudioOutType.Wasapi,
                WavePlayer = wasapiPlayer
            };
        }

        private bool InitWasapiOut(WasapiPlayer player)
        {
            bool success = false;

            if (player != null)
            {
                try
                {
                    player.Init(audioProvider);
                    success = true;
                }
                catch (Exception ex)
                {
                    MessageBoxWindow.ShowOkWindow(
                        "WASAPI Error",
                        "WASAPI initialization failed, changing to defaults:\n\n" + ex.Message,
                        false);

                    player.Dispose();
                    audioProvider.Stop();
                    buzzCore.DCWriteLine("Wasapi error: " + ex);
                }
            }

            return success;
        }

        private void InitRecorder(
            int wasapiDeviceSamplerate,
            bool wasapiExclusiveMode,
            bool wasapiPollMode,
            int latency)
        {
            try
            {
                string wasapiDeviceIDIn = registryEx.Read("DeviceIDIn", "", "WASAPI");
                var enumerator = new MMDeviceEnumerator();
                var mMDevice = enumerator
                    .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                    .FirstOrDefault(d => d.ID == wasapiDeviceIDIn);

                if (mMDevice == null)
                    return;

                var recorderBuilder = new WasapiRecorderBuilder()
                    .WithDevice(mMDevice)
                    .WithLowLatency()
                    .WithMmcssThreadPriority("Pro Audio")
                    .WithBufferLength(latency);

                if (wasapiPollMode)
                    recorderBuilder = recorderBuilder.WithPollingSync();

                wasapiRecorder = recorderBuilder.Build();

                InputDeviceInfo = new AudioEngine.AudioInDevice
                {
                    Name = deviceName,
                    Type = AudioEngine.AudioOutType.Wasapi,
                    WaveFormat = wasapiRecorder.WaveFormat
                };

                if (wasapiDeviceSamplerate != wasapiRecorder.WaveFormat.SampleRate)
                {
                    audioInResampler = new RealTimeResampler();
                    audioInResampler.Reset(
                        wasapiDeviceSamplerate,
                        SampleRate,
                        InputDeviceInfo.WaveFormat.Channels);
                }

                wasapiRecorder.DataAvailable += WasapiRecorder_DataAvailable;
                wasapiRecorder.StartRecording();
            }
            catch (Exception ex)
            {
                MessageBoxWindow.ShowOkWindow(
                    "WASAPI Error",
                    "WASAPI Recorder initialization failed:\n\n" + ex.Message,
                    false);

                buzzCore.DCWriteLine(ex.Message);
                wasapiRecorder = null;
            }
        }

        private void WasapiRecorder_DataAvailable(
            ReadOnlySpan<byte> buffer,
            AudioClientBufferFlags flags,
            long devicePosition,
            long qpcPosition)
        {
            int bytesRemaining = buffer.Length;
            int srcByteOffset = 0;
            int channels = InputDeviceInfo.WaveFormat.Channels;

            Span<byte> audioInBytes = MemoryMarshal.AsBytes(audioInBuffer.AsSpan());

            while (bytesRemaining > 0)
            {
                int copyCountBytes = Math.Min(bytesRemaining, audioInBytes.Length);
                buffer.Slice(srcByteOffset, copyCountBytes).CopyTo(audioInBytes);

                int floatSamples = copyCountBytes / 4;
                int frames = floatSamples / channels;

                if (audioInResampler != null)
                {
                    audioInResampler.FillBuffer(audioInBuffer, frames);

                    int availableFrames = Math.Min(
                        audioInResampler.AvailableFrames(),
                        audioInBuffer.Length / channels);

                    if (availableFrames > 0)
                    {
                        audioInResampler.GetSamples(audioInBuffer, 0, availableFrames);
                        buzzCore.AudioInputAvalable(audioInBuffer, availableFrames, channels);
                    }
                }
                else
                {
                    buzzCore.AudioInputAvalable(audioInBuffer, frames, channels);
                }

                srcByteOffset += copyCountBytes;
                bytesRemaining -= copyCountBytes;
            }
        }

        public void Start()
        {
            wasapiPlayer?.Play();
        }

        public void Stop()
        {
            if (wasapiPlayer?.PlaybackState != PlaybackState.Stopped)
            {
                wasapiPlayer?.Stop();
            }
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
            // WASAPI has no native control panel; handled via AudioEngine windows.
        }

        public void Dispose()
        {
            if (wasapiRecorder != null)
            {
                wasapiRecorder.DataAvailable -= WasapiRecorder_DataAvailable;
                wasapiRecorder.StopRecording();
                wasapiRecorder.Dispose();
                wasapiRecorder = null;
            }

            audioInResampler?.Dispose();
            audioInResampler = null;

            wasapiPlayer?.Dispose();
            wasapiPlayer = null;
        }
    }
}
