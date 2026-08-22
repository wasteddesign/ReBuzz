using BespokeFusion;
using BuzzDotNet.Audio;
using BuzzGUI.Common;
using BuzzGUI.Common.Settings;
using Helios.Concurrency;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using ReBuzz.Common;
using ReBuzz.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;


namespace ReBuzz.Audio
{
    public enum EAudioThreadType
    {
        TaskScheduler = 0,
        Thread,
        None
    }

    public class AudioEngine
    {
        public enum AudioOutType
        {
            ASIO,
            Wasapi,
            DirectSound
        }

        public class AudioOutDevice
        {
            public string Name;
            public AudioOutType Type;
            public object WavePlayer;
        }

        public class AudioInDevice
        {
            public string Name;
            public AudioOutType Type;

            public WaveFormat WaveFormat { get; internal set; }
        }

        public AudioOutDevice SelectedOutDevice { get; private set; }
        public int SampleRateIn { get; private set; }
        public AudioInDevice SelectedInDevice { get; private set; }

        private AudioProvider AudioProvider { get; set; }
        private AudioWaveProvider AudioWaveProvider { get; set; }
        private readonly ReBuzzCore buzzCore;

        private WasapiRecorder wasapiRecorder;

        RealTimeResampler audioInResampler;

        public AudioEngine(
          ReBuzzCore buzzCore,
          EngineSettings settings,
          string buzzPath,
          IUiDispatcher dispatcher,
          IRegistryEx registryEx)
        {
            this.registryEx = registryEx;
            this.buzzPath = buzzPath;
            this.buzzCore = buzzCore;
            engineSettings = settings;
            this.dispatcher = dispatcher;
            CreateScheduler();
        }

        internal static DedicatedThreadPoolTaskScheduler TaskSchedulerAudio { get; private set; }
        internal static TaskFactory TaskFactoryAudio { get; private set; }
        public static int ThreadCount { get; private set; }

        internal void CreateScheduler()
        {
            ThreadCount = registryEx.Read("AudioThreads", 4, "Settings");

            // Using dedicated scheduler for all time critical events is a good approach
            DedicatedThreadPool dedicatedPool = new DedicatedThreadPool(new DedicatedThreadPoolSettings(ThreadCount));
            TaskSchedulerAudio = new DedicatedThreadPoolTaskScheduler(dedicatedPool);
            TaskFactoryAudio = new TaskFactory(TaskSchedulerAudio);
        }

        public void CreateASIOOut(string deviceName)
        {
            var device = AsioDevice.Open(deviceName);
            SelectedOutDevice = new AudioOutDevice() { Name = deviceName, Type = AudioOutType.ASIO, WavePlayer = device };

            int bufferSize = registryEx.Read("BufferSize", 2048, "ASIO");
            int sampleRate = registryEx.Read("SampleRate", 44100, "ASIO");
            AudioWaveProvider = new AudioWaveProvider(buzzCore, sampleRate, device.Capabilities.AllOutputChannels.Length, bufferSize, true, registryEx, engineSettings);

            asioBufferIn = new float[bufferSize * device.Capabilities.AllInputChannels.Length];
            asioBufferOut = new float[bufferSize * device.Capabilities.AllOutputChannels.Length];

            device.InitDuplex(new AsioDuplexOptions
                {
                    InputChannels = device.Capabilities.AllInputChannels,
                    OutputChannels = device.Capabilities.AllOutputChannels,
                    SampleRate = sampleRate,
                    BufferSize = bufferSize,
                    Processor = (in AsioProcessBuffers b) =>
                    {
                        AsioDuplexAudioAvailable(b);
                        asioBufferOut.AsSpan().Clear();
                        AsioDuplexOutput(b);
                    }
                });

            device.DriverResetRequest += AsioOut_DriverResetRequest;

            SelectedInDevice = new AudioInDevice() { Name = deviceName, Type = AudioOutType.ASIO, WaveFormat = AudioWaveProvider.WaveFormat };
            SampleRateIn = sampleRate;
        }

        float[] asioBufferOut = new float[1024 * 16 * 32]; // supports up to 32 channels
        float[] asioBufferIn = new float[1024 * 16 * 32]; // supports up to 32 channels
        private void AsioDuplexAudioAvailable(AsioProcessBuffers b)
        {
            int channels = b.InputChannelCount;
            int frames = b.Frames;

            if (channels <= 0 || frames <= 0)
                return;

            // Interleave all input channels into asioBuffer
            int j = 0;

            for (int i = 0; i < frames; i++)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    asioBufferIn[j++] = b.GetInput(ch)[i];
                }
            }

            // Pass interleaved multichannel buffer to Buzz
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

            AudioWaveProvider.Read(interleaved);

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
            // True device-reported dropout (PP2 Option 2 v3): the ASIO driver
            // could not be served in time and requested a reset.
            ReBuzzCore.RecordDriverReset();
            // Seems to work better if we reset the audio device after call.

            CreateAudioOut(SelectedOutDevice.Name);
            Play();
        }

        public void CreateWasapiOut(string deviceName)
        {
            string wasapiDeviceID = registryEx.Read("DeviceID", "", "WASAPI");
            int wasapiDeviceSamplerate = registryEx.Read("SampleRate", 44100, "WASAPI");
            bool wasapiExclusiveMode = registryEx.Read("Mode", 0, "WASAPI") == 1;                          // default: shared mode
            bool wasapiPollMode = registryEx.Read("Poll", 0, "WASAPI") == 1;                          // default: event sync (vs WithPollingSync)
            int bufferSize = registryEx.Read("BufferSize", 1024, "WASAPI");
            int latency = Math.Max(4, 1000 * 2 * bufferSize / wasapiDeviceSamplerate);
            bool rawMode = registryEx.Read("RawMode", 0, "WASAPI") == 1;                    // IAudioClient3 shared-mode low latency
            bool lowLatencyMode = registryEx.Read("LowLatencyMode", 0, "WASAPI") == 1;      // bypass system audio enhancements

            var enumerator = new MMDeviceEnumerator();
            MMDevice mMDevice = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).FirstOrDefault(d => d.ID == wasapiDeviceID);

            var builder = new WasapiPlayerBuilder()
                .WithDevice(mMDevice)        // default: system default render device
                .WithLatency(latency)                 // default: 200ms
                .WithMmcssThreadPriority("Pro Audio")
                .WithCategory(AudioStreamCategory.Media);

            if (wasapiExclusiveMode)
            {
                builder = builder.WithExclusiveMode();
            }

            if (wasapiPollMode)
            {
                builder = builder.WithPollingSync();
            }

            if (rawMode)
            {
                builder = builder.WithRawMode();
            }

            builder = builder.WithLowLatency(lowLatencyMode);

            var wasapiPlayer = builder.Build();
            
            SampleRateIn = wasapiPlayer.OutputWaveFormat.SampleRate;

            var format = wasapiPlayer.GetSupportedExclusiveFormat(wasapiPlayer.OutputWaveFormat);
            if (format == null)
            {
                // no supported exclusive format found for this device
                MessageBoxWindow.ShowOkWindow("WASAPI Error", "No supported exclusive format found for this device.", false);
            }

            AudioProvider = new AudioProvider(buzzCore, engineSettings, wasapiDeviceSamplerate,
              wasapiPlayer.OutputWaveFormat.Channels, bufferSize, true, registryEx);

            bool success = InitWasapiOut(wasapiPlayer);
            if (!success)
            {
                wasapiPlayer = new WasapiPlayerBuilder()
                    .WithDevice(mMDevice)
                    .WithLowLatency(false)
                    .Build();

                AudioProvider = new AudioProvider(buzzCore, engineSettings, wasapiDeviceSamplerate, 2, bufferSize, true, registryEx);
                success = InitWasapiOut(wasapiPlayer);
            }
            if (!success)
                return;

            wasapiPlayer.PlaybackStopped += (s, e) =>
            {
                if (e.Exception != null)
                {
                    // True device-reported dropout (PP2 Option 2 v3): WASAPI
                    // stopped with an exception = the device faulted / underran.
                    ReBuzzCore.RecordDriverReset();

                    // Seems to work better if we reset the audio device after call.
                    CreateAudioOut(SelectedOutDevice.Name);
                    Play();
                }
            };

            try
            {
                string wasapiDeviceIDIn = registryEx.Read("DeviceIDIn", "", "WASAPI");
                enumerator = new MMDeviceEnumerator();
                mMDevice = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).FirstOrDefault(d => d.ID == wasapiDeviceIDIn);
                if (mMDevice != null)
                {
                    var recorderBuilder = new WasapiRecorderBuilder()
                        .WithDevice(mMDevice)                   // default: system default render device
                        .WithLowLatency()                       // try IAudioClient3 shared-mode low latency
                        .WithMmcssThreadPriority("Pro Audio")
                        .WithBufferLength(latency);            // default: 100ms

                    if (wasapiExclusiveMode)
                    {
                        //recorderBuilder = recorderBuilder.WithExclusiveMode();
                    }

                    if (wasapiPollMode)
                    {
                        recorderBuilder = recorderBuilder.WithPollingSync();
                    }

                    wasapiRecorder = recorderBuilder.Build();

                    SelectedInDevice = new AudioInDevice() { Name = deviceName, Type = AudioOutType.Wasapi, WaveFormat = wasapiRecorder.WaveFormat };

                    // NAudio WASAPI input resampling if different from output
                    if (wasapiDeviceSamplerate != wasapiRecorder.WaveFormat.SampleRate)
                    {
                        audioInResampler = new RealTimeResampler();
                        audioInResampler.Reset(wasapiDeviceSamplerate, SampleRateIn, SelectedInDevice.WaveFormat.Channels);
                    }

                    wasapiRecorder.DataAvailable += WasapiRecorder_DataAvailable;
                    wasapiRecorder.StartRecording();
                }
            }
            catch (Exception ex)
            {
                MessageBoxWindow.ShowOkWindow("WASAPI Error", "WASAPI Recorder initialization failed:\n\n" + ex.Message, false);
                buzzCore.DCWriteLine(ex.Message);
                wasapiRecorder = null;
            }

            SelectedOutDevice = new AudioOutDevice() { Name = deviceName, Type = AudioOutType.Wasapi, WavePlayer = wasapiPlayer };
        }

        readonly float[] audioInBuffer = new float[512];
        private void WasapiRecorder_DataAvailable(ReadOnlySpan<byte> buffer, AudioClientBufferFlags flags, long devicePosition, long qpcPosition)
        {
            int bytesRemaining = buffer.Length;
            int srcByteOffset = 0;

            int channels = SelectedInDevice.WaveFormat.Channels;

            // Get a byte-span view over the float buffer
            Span<byte> audioInBytes = MemoryMarshal.AsBytes(audioInBuffer.AsSpan());

            while (bytesRemaining > 0)
            {
                int copyCountBytes = Math.Min(bytesRemaining, audioInBytes.Length);
                // Copy from ReadOnlySpan<byte> → Span<byte>
                buffer.Slice(srcByteOffset, copyCountBytes).CopyTo(audioInBytes);

                int floatSamples = copyCountBytes / 4;      // total float samples
                int frames = floatSamples / channels;       // audio frames

                if (audioInResampler != null)
                {
                    audioInResampler.FillBuffer(audioInBuffer, frames);

                    int availableFrames = Math.Min(audioInResampler.AvailableFrames(),
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

        bool InitWasapiOut(WasapiPlayer wasapiOut)
        {
            bool success = false;
            if (wasapiOut != null)
            {
                try
                {
                    wasapiOut.Init(AudioProvider);
                    success = true;
                }
                catch (Exception ex)
                {
                    MessageBoxWindow.ShowOkWindow("WASAPI Error", "WASAPI initialization failed, changing to defaults:\n\n" + ex.Message, false);
                    wasapiOut.Dispose();
                    AudioProvider.Stop();
                    buzzCore.DCWriteLine("Wasap error: " + ex);
                }
            }
            return success;
        }

        public void Play()
        {
            if (SelectedOutDevice == null)
                return;

            try
            {
                var wp = (SelectedOutDevice.WavePlayer as IWavePlayer);
                wp?.Play();

                var ap = (SelectedOutDevice.WavePlayer as AsioDevice);
                ap?.Start();
            }
            catch (Exception e)
            {
                buzzCore.DCWriteLine("WavePlayer error: " + e.Message);
            }
        }

        readonly Lock audioEngineLock = new();

        public void FinalStop()
        {
            lock (audioEngineLock)
            {
                AudioProvider?.Stop();
                AudioWaveProvider?.Stop();
                
                if (SelectedOutDevice == null)
                    return;

                StopPlayback();
                AudioWaveProvider = null;
                AudioProvider = null;
            }
        }

        void StopPlayback()
        {
            try
            {
                var wp = (SelectedOutDevice.WavePlayer as IWavePlayer);
                if (wp?.PlaybackState != PlaybackState.Stopped)
                {
                    wp?.Stop();
                }

                var ap = (SelectedOutDevice.WavePlayer as AsioDevice);
                if (ap?.State != AsioDeviceState.Stopped)
                {
                    ap?.Stop();
                }
            }
            catch (Exception e)
            {
                buzzCore.DCWriteLine(e.Message);
            }
        }

        public void Stop()
        {
            try
            {
                StopPlayback();
                ClearAudioBuffer();
            }
            catch (Exception e)
            {
                buzzCore.DCWriteLine(e.Message);
            }
        }

        public void ReleaseAudioDriver()
        {   
            if (wasapiRecorder != null)
            {
                wasapiRecorder.DataAvailable -= WasapiRecorder_DataAvailable;
                wasapiRecorder.StopRecording();
                wasapiRecorder.Dispose();
                wasapiRecorder = null;
                SelectedInDevice = null;
            }

            if (SelectedOutDevice != null)
            {
                if (SelectedOutDevice.WavePlayer is AsioDevice)
                {
                    (SelectedOutDevice.WavePlayer as AsioDevice).DriverResetRequest -= AsioOut_DriverResetRequest;
                }

                var wp = (SelectedOutDevice.WavePlayer as IWavePlayer);
                wp?.Dispose();

                var ap = (SelectedOutDevice.WavePlayer as AsioDevice);
                ap?.Dispose();
            }

            SelectedOutDevice = null;
            audioInResampler?.Dispose();
            audioInResampler = null;
        }

        public List<AudioOutDevice> AudioDevices()
        {
            List<AudioOutDevice> devices = new List<AudioOutDevice>();
            try
            {
                if (AsioOut.isSupported())
                {
                    foreach (var device in AsioOut.GetDriverNames())
                    {
                        devices.Add(new AudioOutDevice() { Name = device, Type = AudioOutType.ASIO });
                    }
                }
                devices.Add(new AudioOutDevice() { Name = "WASAPI", Type = AudioOutType.Wasapi });
                //devices.Add(new AudioOutDevice() { Name = "DirectSound", Type = AudioOutType.DirectSound });
            }
            catch (Exception e)
            {
                MessageBox.Show("AudioDevices Error: " + e.Message, "Audio Devices Error");
            }
            return devices;
        }

        internal void CreateAudioOut(string audioDriver)
        {
            dispatcher.Invoke(FinalStop);
            ReleaseAudioDriver();

            var device = AudioDevices().FirstOrDefault(x => x.Name == audioDriver);
            if (device == null) { device = AudioDevices().First(); }

            try
            {
                if (device != null)
                {
                    switch (device.Type)
                    {
                        case AudioOutType.ASIO:
                            CreateASIOOut(device.Name);
                            break;
                        case AudioOutType.Wasapi:
                            CreateWasapiOut(device.Name);
                            break;
                        case AudioOutType.DirectSound:
                            // Switching to DX audio might hang on complex project, ignore...
                            // CreateDirectSoundOut(device.Name);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                buzzCore.DCWriteLine("Audio Driver Error: " + e.Message);
            }
        }

        WasapiConfigWindow wasapiConfigWindow;
        AsioConfigWindow asioConfigWindow;
        private readonly EngineSettings engineSettings;
        private readonly string buzzPath;
        private readonly IUiDispatcher dispatcher;
        private readonly IRegistryEx registryEx;

        internal void ShowControlPanel()
        {
            if (SelectedOutDevice != null)
            {
                switch (SelectedOutDevice.Type)
                {
                    case AudioOutType.ASIO:
                        if (asioConfigWindow == null)
                        {
                            var asio = (SelectedOutDevice.WavePlayer as AsioDevice);
                            asioConfigWindow = new AsioConfigWindow(asio.DriverName, registryEx);

                            asioConfigWindow.OpenAsioControlPanel += () =>
                            {
                                asio.ShowControlPanel();
                            };

                            var rd = Utils.GetUserControlXAML<ResourceDictionary>("MachineView\\MVResources.xaml", buzzPath);
                            asioConfigWindow.Resources.MergedDictionaries.Add(rd);
                            if (asioConfigWindow.ShowDialog() == true)
                            {
                                asioConfigWindow.SaveSelection();
                                CreateAudioOut(SelectedOutDevice.Name);
                                Play();
                            }
                            asioConfigWindow = null;
                        }

                        break;
                    case AudioOutType.Wasapi:
                        if (wasapiConfigWindow == null)
                        {
                            wasapiConfigWindow = new WasapiConfigWindow(registryEx);
                            var rd = Utils.GetUserControlXAML<ResourceDictionary>("MachineView\\MVResources.xaml", buzzPath);
                            wasapiConfigWindow.Resources.MergedDictionaries.Add(rd);
                            if (wasapiConfigWindow.ShowDialog() == true)
                            {
                                wasapiConfigWindow.SaveSelection();
                                CreateAudioOut(SelectedOutDevice.Name);
                                Play();
                            }
                            wasapiConfigWindow = null;
                        }
                        break;
                    case AudioOutType.DirectSound:

                        break;

                }
            }
        }

        internal void ClearAudioBuffer()
        {
            if (AudioProvider != null)
            {
                (AudioProvider as IReBuzzAudioProvider).ClearBuffer();
            }
        }

        internal void Reset()
        {
            CreateAudioOut(SelectedOutDevice.Name);
            Play();
        }

        internal IReBuzzAudioProvider GetAudioProvider()
        {
            if (AudioProvider != null)
                return AudioProvider;
            else if (AudioWaveProvider != null)
                return AudioWaveProvider;
            else return null;
        }

        internal void ClearChannels()
        {
            if (AudioProvider != null)
            {
                (AudioProvider as IReBuzzAudioProvider).ClearChannels();
            }
        }
    }
}
