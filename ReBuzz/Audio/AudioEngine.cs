using BuzzDotNet.Audio;
using BuzzGUI.Common;
using Helios.Concurrency;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using ReBuzz.Common;
using ReBuzz.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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
            public IWavePlayer WavePlayer;
        }

        public AudioOutDevice SelectedOutDevice { get; private set; }

        private AudioProvider AudioProvider { get; set; }
        private AudioWaveProvider AudioWaveProvider { get; set; }
        private readonly ReBuzzCore buzzCore;
        WasapiCapture wasapiCapture;

        public AudioEngine(ReBuzzCore buzzCore)
        {
            this.buzzCore = buzzCore;
            CreateScheduler();
        }

        internal static DedicatedThreadPoolTaskScheduler TaskSchedulerAudio { get; private set; }
        internal static TaskFactory TaskFactoryAudio { get; private set; }
        public static int ThreadCount { get; private set; }

        internal void CreateScheduler()
        {
            ThreadCount = RegistryEx.Read("AudioThreads", 4, "Settings");

            // Using dedicated scheduler for all time critical events is a good approach
            DedicatedThreadPool dedicatedPool = new DedicatedThreadPool(new DedicatedThreadPoolSettings(ThreadCount));
            TaskSchedulerAudio = new DedicatedThreadPoolTaskScheduler(dedicatedPool);
            TaskFactoryAudio = new TaskFactory(TaskSchedulerAudio);
        }

        public void CreateASIOOut(string deviceName)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var asioOut = new AsioOut(deviceName); // This needs to be called from UI thread

                SelectedOutDevice = new AudioOutDevice() { Name = deviceName, Type = AudioOutType.ASIO, WavePlayer = asioOut };

                int bufferSize = RegistryEx.Read("BufferSize", 2048, "ASIO");
                int sampleRate = RegistryEx.Read("SampleRate", 44100, "ASIO");
                //AudioProvider = new AudioProvider(buzzCore, machineManager, sampleRate, 2, bufferSize, true);
                AudioWaveProvider = new AudioWaveProvider(buzzCore, sampleRate, asioOut.DriverOutputChannelCount, bufferSize, false);

                //asioOut.Init(AudioProvider);
                asioOut.InitRecordAndPlayback(AudioWaveProvider, 2, sampleRate);

                asioOut.DriverResetRequest += AsioOut_DriverResetRequest;
                asioOut.PlaybackStopped += AsioOut_PlaybackStopped;
                asioOut.AudioAvailable += AsioOut_AudioAvailable;
            });
        }

        readonly float[] asioSamples = new float[1024 * 16 * 2];
        private void AsioOut_AudioAvailable(object sender, AsioAudioAvailableEventArgs e)
        {
            int read = e.GetAsInterleavedSamples(asioSamples);
            buzzCore.AudioInputAvalable(asioSamples, read);
        }

        private void AsioOut_PlaybackStopped(object sender, StoppedEventArgs e)
        {
        }

        private void AsioOut_DriverResetRequest(object sender, EventArgs e)
        {
            // Seems to work better if we reset the audio device after call.
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                CreateAudioOut(SelectedOutDevice.Name);
                Play();
            });

        }

        readonly float[] audioInBuffer = new float[512];
        private unsafe void WasapiCapture_DataAvailable(object sender, WaveInEventArgs e)
        {
            int bytesRemaining = e.BytesRecorded;
            int srcByteOffset = 0;
            while (bytesRemaining > 0)
            {
                int copyCount = Math.Min(bytesRemaining, audioInBuffer.Length * 4);
                Buffer.BlockCopy(e.Buffer, srcByteOffset, audioInBuffer, 0, copyCount);
                buzzCore.AudioInputAvalable(audioInBuffer, copyCount >> 2);
                srcByteOffset += copyCount;
                bytesRemaining -= copyCount;
            }
        }

        public void CreateWasapiOut(string deviceName)
        {
            string wasapiDeviceID = RegistryEx.Read("DeviceID", "", "WASAPI");
            int wasapiDeviceSamplerate = RegistryEx.Read("SampleRate", 44100, "WASAPI");
            int wasapiMode = RegistryEx.Read("Mode", 0, "WASAPI");
            int wasapiPoll = RegistryEx.Read("Poll", 0, "WASAPI");
            int bufferSize = RegistryEx.Read("BufferSize", 1024, "WASAPI");

            var enumerator = new MMDeviceEnumerator();
            MMDevice mMDevice = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).FirstOrDefault(d => d.ID == wasapiDeviceID);

            WasapiOut wasapiOut = null;
            if (mMDevice != null)
            {
                int latency = Math.Max(1, 1000 * bufferSize / wasapiDeviceSamplerate);
                wasapiOut = new WasapiOut(mMDevice, wasapiMode == 0 ? AudioClientShareMode.Shared : AudioClientShareMode.Exclusive, wasapiPoll == 1, latency);
            }
            else
            {
                wasapiOut = new WasapiOut();
            }

            AudioProvider = new AudioProvider(buzzCore, wasapiDeviceSamplerate, wasapiOut.OutputWaveFormat.Channels, bufferSize, false);

            try
            {
                wasapiOut.Init(AudioProvider);
            }
            catch (Exception ex)
            {
                wasapiOut.Dispose();
                AudioProvider.Stop();
                Global.Buzz.DCWriteLine("Wasap error: " + ex);
                wasapiOut = new WasapiOut();

                AudioProvider = new AudioProvider(buzzCore, wasapiDeviceSamplerate, 2, bufferSize, false);
                wasapiOut.Init(AudioProvider);
            }

            wasapiOut.PlaybackStopped += (s, e) =>
            {
                if (e.Exception != null)
                {
                    // Seems to work better if we reset the audio device after call.
                    DispatcherTimer dt = new DispatcherTimer();
                    dt.Interval = TimeSpan.FromSeconds(1);
                    dt.Tick += (s2, e2) =>
                    {
                        CreateAudioOut(SelectedOutDevice.Name);
                        Play();
                        dt.Stop();
                    };
                    dt.Start();
                }
            };

            try
            {
                string wasapiDeviceIDIn = RegistryEx.Read("DeviceIDIn", "", "WASAPI");
                enumerator = new MMDeviceEnumerator();
                mMDevice = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).FirstOrDefault(d => d.ID == wasapiDeviceIDIn);
                if (mMDevice != null)
                {
                    int latency = Math.Max(1, 1000 * bufferSize / wasapiDeviceSamplerate / 2);
                    wasapiCapture = new WasapiCapture(mMDevice, wasapiMode == 0, latency);
                    wasapiCapture.DataAvailable += WasapiCapture_DataAvailable;
                    wasapiCapture.StartRecording();
                }
            }
            catch (Exception ex)
            {
                buzzCore.DCWriteLine(ex.Message);
                wasapiCapture = null;
            }

            SelectedOutDevice = new AudioOutDevice() { Name = deviceName, Type = AudioOutType.Wasapi, WavePlayer = wasapiOut };
        }

        public void CreateDirectSoundOut(string deviceName)
        {
            int latency = 200;
            int samplerate = 44100;
            var dxOut = new DirectSoundOut(DirectSoundOut.DSDEVID_DefaultPlayback, 40);
            //var latency = (int)dxOut.GetType().GetField("desiredLatency", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(dxOut);

            int buffer = 2 * latency * samplerate / 1000;
            AudioProvider = new AudioProvider(buzzCore, samplerate, 2, buffer, true);

            dxOut.Init(AudioProvider);
            SelectedOutDevice = new AudioOutDevice() { Name = deviceName, Type = AudioOutType.DirectSound, WavePlayer = dxOut };
        }

        public void Play()
        {
            try
            {
                SelectedOutDevice?.WavePlayer?.Play();
            }
            catch (Exception e)
            {
                Global.Buzz.DCWriteLine("WavePlayer error: " + e.Message);
            }
        }

        public void FinalStop()
        {
            lock (this)
            {
                ReBuzzCore.SkipAudio = true;

                AudioProvider?.Stop();
                AudioProvider = null;

                AudioWaveProvider?.Stop();
                AudioWaveProvider = null;

                try
                {
                    SelectedOutDevice?.WavePlayer?.Stop();
                }
                catch (Exception e)
                {
                    buzzCore.DCWriteLine(e.Message);
                }

                ReBuzzCore.SkipAudio = false;
            }
        }

        public void Stop()
        {
            try
            {
                SelectedOutDevice?.WavePlayer?.Stop();
                ClearAudioBuffer();
            }
            catch (Exception e)
            {
                buzzCore.DCWriteLine(e.Message);
            }
        }

        public void ReleaseAudioDriver()
        {
            if (wasapiCapture != null)
            {
                wasapiCapture.DataAvailable -= WasapiCapture_DataAvailable;
                wasapiCapture.StopRecording();
                wasapiCapture.Dispose();
                wasapiCapture = null;
            }

            if (SelectedOutDevice != null)
            {
                if (SelectedOutDevice.WavePlayer is AsioOut)
                {
                    (SelectedOutDevice.WavePlayer as AsioOut).DriverResetRequest -= AsioOut_DriverResetRequest;
                    (SelectedOutDevice.WavePlayer as AsioOut).PlaybackStopped -= AsioOut_PlaybackStopped;
                    (SelectedOutDevice.WavePlayer as AsioOut).AudioAvailable -= AsioOut_AudioAvailable;
                }

                SelectedOutDevice.WavePlayer.Dispose();
            }
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
            Application.Current.Dispatcher.Invoke(() =>
            {
                FinalStop();
            });
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
                Global.Buzz.DCWriteLine("Audio Driver Error: " + e.Message);
            }
        }

        WasapiConfigWindow wasapiConfigWindow;
        AsioConfigWindow asioConfigWindow;
        internal void ShowControlPanel()
        {
            if (SelectedOutDevice != null)
            {
                switch (SelectedOutDevice.Type)
                {
                    case AudioOutType.ASIO:
                        if (asioConfigWindow == null)
                        {
                            var asio = (SelectedOutDevice.WavePlayer as AsioOut);
                            asioConfigWindow = new AsioConfigWindow(asio.DriverName);

                            asioConfigWindow.OpenAsioControlPanel += () =>
                            {
                                asio.ShowControlPanel();
                            };

                            var rd = Utils.GetUserControlXAML<ResourceDictionary>("MachineView\\MVResources.xaml");
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
                            wasapiConfigWindow = new WasapiConfigWindow();
                            var rd = Utils.GetUserControlXAML<ResourceDictionary>("MachineView\\MVResources.xaml");
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
    }
}
