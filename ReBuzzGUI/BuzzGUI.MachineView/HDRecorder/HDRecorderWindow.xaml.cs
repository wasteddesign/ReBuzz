using BuzzGUI.Common;
using BuzzGUI.Common.DSP;
using BuzzGUI.Interfaces;
using libsndfile;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
//using PropertyChanged;

namespace BuzzGUI.MachineView.HDRecorder
{
    /// <summary>
    /// Interaction logic for HDRecorderWindow.xaml
    /// </summary>
    /// 

    public partial class HDRecorderWindow : Window, INotifyPropertyChanged
    {
        public enum States { Stopped, WaitingForStart, RecordingLoop, Recording };

        IBuzz buzz;
        IMachineGraph machineGraph;

        public IMachineGraph MachineGraph
        {
            get { return machineGraph; }
            set
            {
                if (machineGraph != null)
                {
                    Global.GeneralSettings.PropertyChanged -= new PropertyChangedEventHandler(GeneralSettings_PropertyChanged);
                    buzz.PropertyChanged -= buzz_PropertyChanged;
                }

                machineGraph = value;

                if (machineGraph != null)
                {
                    Global.GeneralSettings.PropertyChanged += new PropertyChangedEventHandler(GeneralSettings_PropertyChanged);
                    buzz = machineGraph.Buzz;
                    buzz.PropertyChanged += buzz_PropertyChanged;
                }

            }
        }

        void GeneralSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "WPFIdealFontMetrics":
                    PropertyChanged.Raise(this, "TextFormattingMode");
                    break;
            }

        }

        void buzz_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Playing")
            {
                if (!buzz.Playing && IsRecording && driveTask != null)
                    Stop();
            }

        }

        public SimpleCommand SaveAsCommand { get; private set; }
        public SimpleCommand RecordCommand { get; private set; }
        public SimpleCommand RecordLoopCommand { get; private set; }
        public SimpleCommand RenderLoopCommand { get; private set; }
        public SimpleCommand RenderLoop2Command { get; private set; }
        public SimpleCommand StopCommand { get; private set; }
        public SimpleCommand OpenFileLocationCommand { get; private set; }
        public SimpleCommand OpenInEditorCommand { get; private set; }

        bool isRecording;
        public bool IsRecording
        {
            get { return isRecording; }
            set
            {
                isRecording = value;
                PropertyChanged.Raise(this, "IsRecording");
                PropertyChanged.Raise(this, "IsNotRecording");
            }
        }

        public bool IsNotRecording { get { return !IsRecording; } }

        public string OutputPath { get; set; }
        public int BitDepthIndex { get; set; }
        libsndfile.Format Format;
        States state;
        CancellationTokenSource cts;
        BlockingCollection<float[]> bufferQueue;
        Task saveTask;
        Task driveTask;
        int prepareCount;

        string baseName;
        int fileCount = 0;

        string NextFilename
        {
            get
            {
                string path;

                do
                {
                    fileCount++;
                    path = Path.Combine(Path.GetDirectoryName(baseName), Path.GetFileNameWithoutExtension(baseName));
                    path += "-" + fileCount.ToString("D4") + Path.GetExtension(baseName);

                } while (File.Exists(path));

                return path;
            }
        }

        public HDRecorderWindow()
        {
            string fileName = Global.Buzz.Song.SongName != null ? Path.GetFileNameWithoutExtension(Global.Buzz.Song.SongName) + ".wav" : "ReBuzz.wav";
            baseName = System.IO.Path.Combine(
                RegistryEx.Read<string>("HDRecorderPath", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
                fileName);

            OutputPath = NextFilename;
            Format = libsndfile.Format.SF_FORMAT_WAV;
            BitDepthIndex = 0;

            SaveAsCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    var dlg = new SaveFileDialog();
                    dlg.InitialDirectory = System.IO.Path.GetDirectoryName(OutputPath);
                    dlg.FileName = System.IO.Path.GetFileName(OutputPath);
                    dlg.Filter = "Microsoft PCM wave|*.wav|Apple/SGI AIFF|*.aif|Sun/NeXT AU format|*.au|RAW PCM|*.raw|FLAC lossless|*.flac|Ogg Vorbis|*.ogg|mp3|*.mp3";
                    dlg.DefaultExt = ".wav";

                    if ((bool)dlg.ShowDialog())
                    {
                        baseName = OutputPath = dlg.FileName;
                        TrimBaseName();
                        fileCount = 0;
                        PropertyChanged.Raise(this, "OutputPath");
                        RegistryEx.Write<string>("HDRecorderPath", System.IO.Path.GetDirectoryName(baseName));

                        switch (dlg.FilterIndex)
                        {
                            case 2: Format = libsndfile.Format.SF_FORMAT_AIFF; break;
                            case 3: Format = libsndfile.Format.SF_FORMAT_AU; break;
                            case 4: Format = libsndfile.Format.SF_FORMAT_RAW; break;
                            case 5: Format = libsndfile.Format.SF_FORMAT_FLAC; break;
                            case 6: Format = libsndfile.Format.SF_FORMAT_VORBIS | libsndfile.Format.SF_FORMAT_OGG; break;
                            case 7: Format = libsndfile.Format.SF_FORMAT_MPEG | libsndfile.Format.SF_FORMAT_MPEG_LAYER_III; break;
                            default: Format = libsndfile.Format.SF_FORMAT_WAV; break;
                        }
                    }
                }
            };

            RecordCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x => Record()
            };

            RecordLoopCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x => RecordLoop(false, 0)
            };

            RenderLoopCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x => RecordLoop(true, 0)
            };

            RenderLoop2Command = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x => RecordLoop(true, 1)
            };

            StopCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x => Stop()
            };

            OpenFileLocationCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => OutputPath.Trim() != "",
                ExecuteDelegate = x =>
                {
                    string path = Path.GetDirectoryName(OutputPath);
                    Process.Start("explorer.exe", path);
                }
            };

            OpenInEditorCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => OutputPath.Trim() != "",
                ExecuteDelegate = x =>
                {
                    string path = Path.GetDirectoryName(OutputPath);
                    Process.Start("explorer.exe", path);
                }
            };

            DataContext = this;
            InitializeComponent();

            this.Closing += (sender, e) =>
            {
                Stop();
            };

        }

        // Remove the runnning digit
        private void TrimBaseName()
        {
            string name = Path.GetFileNameWithoutExtension(baseName);
            string extension = Path.GetExtension(baseName);
            if (name.Length < 6)
                return;

            if (name[name.Length - 5] != '-')
                return;

            bool allNumbers = true;
            for (int i = 0; i < 4; i++)
            {
                if (!char.IsDigit(name[name.Length - 1 - i]))
                {
                    allNumbers = false;
                    break;
                }
            }

            if (allNumbers)
            {
                baseName = Path.Combine(Path.GetDirectoryName(baseName), name.Substring(0, name.Length - 5) + extension);
            }
        }

        void CreateFile()
        {
            var bitdepths = new[] { Format.SF_FORMAT_PCM_16, Format.SF_FORMAT_PCM_24, Format.SF_FORMAT_PCM_32, Format.SF_FORMAT_FLOAT };

            var soundFile = SoundFile.Create(OutputPath, buzz.SelectedAudioDriverSampleRate, 2, Format | bitdepths[BitDepthIndex]);
            soundFile.Clipping = true;

            bufferQueue = new BlockingCollection<float[]>();

            saveTask = Task.Factory.StartNew(() =>
            {
                while (!bufferQueue.IsCompleted)
                {
                    try
                    {
                        var samples = bufferQueue.Take();
                        DSP.Scale(samples, 1.0f / 32768.0f);
                        soundFile.WriteFloat(samples, 0, samples.Length / 2);
                    }
                    catch (InvalidOperationException) { }
                }

                soundFile.Close();

            });

        }

        void Record()
        {
            try
            {
                CreateFile();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
                return;
            }

            progress.IsEnabled = true;
            progress.IsIndeterminate = true;
            prepareCount = 0;
            state = States.Recording;

            buzz.MasterTap += Buzz_MasterTap;
            IsRecording = true;
        }

        void RecordLoop(bool driveaudio, int prepcount)
        {
            try
            {
                CreateFile();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
                return;
            }

            buzz.Playing = false;
            if (driveaudio) buzz.OverrideAudioDriver = true;
            buzz.Song.PlayPosition = buzz.Song.LoopStart;
            progress.Minimum = buzz.Song.LoopStart;
            progress.Maximum = buzz.Song.LoopEnd;
            progress.IsEnabled = true;
            prepareCount = prepcount;
            state = States.WaitingForStart;

            buzz.MasterTap += Buzz_MasterTap;

            buzz.Playing = true;
            IsRecording = true;

            if (driveaudio)
            {
                cts = new CancellationTokenSource();

                driveTask = Task.Factory.StartNew(() =>
                {

                    var buffer = new float[2 * 256];
                    while (!cts.Token.IsCancellationRequested)
                    {
                        System.Threading.Thread.Sleep(0);

                        for (int i = 0; i < 10; i++)
                            buzz.RenderAudio(buffer, 256, buzz.SelectedAudioDriverSampleRate);
                    }

                    buzz.OverrideAudioDriver = false;
                });
            }


        }

        void Buzz_MasterTap(float[] samples, bool stereo, SongTime songtime)
        {
            bool juststarted = false;

            if (state == States.WaitingForStart)
            {
                if (songtime.PosInTick == 0 && songtime.CurrentTick == buzz.Song.LoopStart)
                {
                    if (prepareCount > 0)
                    {
                        prepareCount--;
                        return;
                    }
                    else
                    {
                        state = States.RecordingLoop;
                        juststarted = true;
                    }
                }
            }

            if (state == States.RecordingLoop)
            {
                if (!juststarted && songtime.PosInTick == 0 && (songtime.CurrentTick == buzz.Song.LoopStart || songtime.CurrentTick == buzz.Song.LoopEnd))
                {
                    state = States.Stopped;
                    Stop();
                    return;
                }

                bufferQueue.Add(samples);
                progress.Value = songtime.CurrentTick + (songtime.SubTicksPerTick > 0 ? (double)songtime.CurrentSubTick / songtime.SubTicksPerTick : 0);
            }
            else if (state == States.Recording)
            {
                bufferQueue.Add(samples);
            }

        }

        void Stop()
        {
            if (!IsRecording) return;

            IsRecording = false;
            buzz.MasterTap -= Buzz_MasterTap;
            if (buzz.Playing) buzz.Playing = false;
            progress.Value = 0;
            progress.IsEnabled = false;
            progress.IsIndeterminate = false;
            if (cts != null) cts.Cancel();
            bufferQueue.CompleteAdding();

            OutputPath = NextFilename;
            PropertyChanged.Raise(this, "OutputPath");

            if (driveTask != null)
                Task.WaitAll(driveTask, saveTask);
            else
                Task.WaitAll(saveTask);

            driveTask = saveTask = null;

        }

        public TextFormattingMode TextFormattingMode { get { return Global.GeneralSettings.WPFIdealFontMetrics ? TextFormattingMode.Ideal : TextFormattingMode.Display; } }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
