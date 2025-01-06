using BuzzGUI.Common;
using BuzzGUI.Common.DSP;
using BuzzGUI.Interfaces;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Window = System.Windows.Window;

namespace BuzzGUI.MachineView.SignalAnalysis
{
    /// <summary>
    /// Interaction logic for SignalAnalysisWindow.xaml
    /// </summary>
    /// 


    public partial class SignalAnalysisWindow : Window, INotifyPropertyChanged
    {
        public double VUMeterLevelL { get; set; }
        public double VUMeterLevelR { get; set; }
        public double VUMeterRMSLevelL { get; set; }
        public double VUMeterRMSLevelR { get; set; }

        const int amp12 = 0xFFFE;
        const int amp0 = 0x4000;
        const double MinAmp = 66;
        const double VUMeterRange = 80.0;

        private readonly int FFT_SIZE = 2048;

        float maxSampleL;
        float maxSampleR;

        public SolidColorBrush AudioLBrush { get; }
        private float[] AudioBuffer { get; set; }
        private int audioBufferFillPosition = 0;
        private readonly int AUDIO_BUFFER_SIZE = 20 * 2048;

        public double WindowSize { get; set; }
        public double WindowPosition { get; set; }

        double waveZoom;
        public double WaveZoom { get => waveZoom; set { waveZoom = value; PropertyChanged.Raise(this, "WaveZoom"); } }

        DispatcherTimer timer;

        private IMachineConnection selectedConnection;
        private bool masterTap;

        public IMachine Machine { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        readonly string[] fftWindows = new string[] { "Hanning", "Hamming", "Blackman", "BlackmanExact", "BlackmanHarris", "FlatTop", "Bartlett", "Cosine" };
        public string[] FFTWindows { get => fftWindows; }

        string selectedFFTWindow;
        public string SelectedFFTWindow { get => selectedFFTWindow; set { selectedFFTWindow = value; PropertyChanged.Raise(this, "SelectedFFTWindow"); } }

        public IMachineConnection SelectedConnection
        {
            get { return selectedConnection; }
            set
            {
                if (selectedConnection != null)
                {
                    selectedConnection.Tap -= SelectedConnection_Tap;
                    timer.Stop();
                }
                else if (masterTap)
                {
                    Global.Buzz.MasterTap -= SelectedConnection_Tap;
                    timer.Stop();
                    selectedConnection = value;
                    masterTap = false;
                    return;
                }

                selectedConnection = value;
                if (selectedConnection != null)
                {
                    selectedConnection.Tap += SelectedConnection_Tap;

                    timer.Start();
                }
                else if (Machine.Name == "Master")
                {
                    masterTap = true;
                    Global.Buzz.MasterTap += SelectedConnection_Tap;
                    timer.Start();
                }
            }
        }

        public SolidColorBrush AudioRBrush { get; }

        private void SelectedConnection_Tap(float[] samples, bool stereo, SongTime songTime)
        {
            // Fill audio buffer
            FillAudioBuffer(samples);

            // VU bars
            if (!stereo) // Mono
            {
                maxSampleL = Math.Max(maxSampleL, DSP.AbsMax(samples) * (1.0f / 32768.0f));
                maxSampleR = maxSampleL;
            }
            else
            {
                float[] L = new float[samples.Length / 2];
                float[] R = new float[samples.Length / 2];
                for (int i = 0; i < samples.Length / 2; i++)
                {
                    L[i] = samples[i * 2];
                    R[i] = samples[i * 2 + 1];
                }

                maxSampleL = Math.Max(maxSampleL, DSP.AbsMax(L) * (1.0f / 32768.0f));
                maxSampleR = Math.Max(maxSampleR, DSP.AbsMax(R) * (1.0f / 32768.0f));
            }
        }


        public SignalAnalysisWindow(IMachine mac)
        {
            this.Title = "Signal Analysis: " + mac.Name;
            this.Machine = mac;
            Machine.Graph.ConnectionAdded += (obj) =>
            {
                PropertyChanged.Raise(this, "Machine");
            };

            Machine.Graph.ConnectionRemoved += (obj) =>
            {
                if (!masterTap)
                {
                    if (Machine.Outputs.FirstOrDefault(x => x == SelectedConnection) == null)
                        SelectedConnection = Machine.Outputs.FirstOrDefault();
                    PropertyChanged.Raise(this, "Machine");
                }
            };

            Global.Buzz.Song.MachineRemoved += (sender) =>
            {
                if (sender == Machine)
                    this.Close();
            };

            AudioLBrush = TryFindResource("AudioLBrush") as SolidColorBrush;
            if (AudioLBrush == null)
                AudioLBrush = Brushes.Red;

            AudioRBrush = TryFindResource("AudioLBrush") as SolidColorBrush;
            if (AudioRBrush == null)
                AudioRBrush = Brushes.SpringGreen;

            AudioBuffer = new float[AUDIO_BUFFER_SIZE];


            InitializeComponent();
            this.Resources.MergedDictionaries.Add(GetThemeResources());

            SelectedFFTWindow = "Hanning";

            DataContext = this;
            SetTimer();

            WindowSize = 1;
            WindowPosition = 1;
            WaveZoom = 0;

            SelectedConnection = Machine.Outputs.FirstOrDefault();
            PropertyChanged.Raise(this, "Machine");

            this.Loaded += (sender, e) =>
            {
                DrawDbText();
            };

            this.SizeChanged += (sender, e) =>
            {
                DrawDbText();
            };

            this.Closed += (sender, e) =>
            {
                SelectedConnection = null;
            };

        }

        void SetTimer()
        {
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(1000 / 30);
            timer.Tick += (sender, e) =>
            {
                if (maxSampleL >= 0)
                {
                    var db = Math.Min(Math.Max(Decibel.FromAmplitude(maxSampleL), -VUMeterRange), 0.0);
                    VUMeterLevelL = (db + VUMeterRange) / VUMeterRange;
                    VUMeterRMSLevelL = VUMeterLevelL * 0.70710;
                    PropertyChanged.Raise(this, "VUMeterLevelL");
                    PropertyChanged.Raise(this, "VUMeterRMSLevelL");
                    maxSampleL = -1;
                }
                if (maxSampleR >= 0)
                {
                    var db = Math.Min(Math.Max(Decibel.FromAmplitude(maxSampleR), -VUMeterRange), 0.0);
                    VUMeterLevelR = (db + VUMeterRange) / VUMeterRange;
                    VUMeterRMSLevelR = VUMeterLevelR * 0.70710;
                    PropertyChanged.Raise(this, "VUMeterLevelR");
                    PropertyChanged.Raise(this, "VUMeterRMSLevelR");
                    maxSampleR = -1;
                }

                double[] audioBufL = GetBuffer(FFT_SIZE * 2, 0);
                double[] audioBufR = GetBuffer(FFT_SIZE * 2, 1);

                double[] window = null;

                switch (SelectedFFTWindow)
                {
                    case "Hanning":
                        window = FftSharp.Window.Hanning(audioBufL.Length);
                        break;
                    case "Hamming":
                        window = FftSharp.Window.Hamming(audioBufL.Length);
                        break;
                    case "Blackman":
                        window = FftSharp.Window.Blackman(audioBufL.Length);
                        break;
                    case "BlackmanExact":
                        window = FftSharp.Window.BlackmanExact(audioBufL.Length);
                        break;
                    case "BlackmanHarris":
                        window = FftSharp.Window.BlackmanHarris(audioBufL.Length);
                        break;
                    case "FlatTop":
                        window = FftSharp.Window.FlatTop(audioBufL.Length);
                        break;
                    case "Bartlett":
                        window = FftSharp.Window.Bartlett(audioBufL.Length);
                        break;
                    case "Cosine":
                        window = FftSharp.Window.Cosine(audioBufL.Length);
                        break;
                }

                FftSharp.Window.ApplyInPlace(window, audioBufL);
                FftSharp.Window.ApplyInPlace(window, audioBufR);

                double[] fftPowerL = FftSharp.Transform.FFTpower(audioBufL);
                double[] fftPowerR = FftSharp.Transform.FFTpower(audioBufR);

                cSpecL.Children.Clear();
                cSpecR.Children.Clear();
                Polyline polylineL = new Polyline();
                polylineL.Stroke = AudioLBrush;

                double canvasStepX = (WindowSize + 9) / 10.0;

                double fftStep = fftPowerL.Length / cSpecL.ActualWidth / canvasStepX;

                double fftIndexStart = (WindowPosition / sliderPos.Maximum) * fftPowerL.Length;
                double fftIndex = fftIndexStart;
                double clamp = 80;
                double move = 10;

                int prevInd = -1;

                for (double x = 0; x < cSpecL.ActualWidth; x++)
                {
                    int ind = Math.Min((int)fftIndex, fftPowerL.Length - 1);
                    if (ind != prevInd)
                    {
                        double y = cSpecL.ActualHeight - (cSpecL.ActualHeight * fftPowerL[ind] / clamp) - move;
                        polylineL.Points.Add(new Point(x, y));
                        prevInd = ind;
                    }
                    fftIndex += fftStep;
                }
                cSpecL.Children.Add(polylineL);

                Polyline polylineR = new Polyline();
                polylineR.Stroke = AudioRBrush;

                fftIndex = fftIndexStart;
                prevInd = -1;
                for (double x = 0; x < cSpecR.ActualWidth; x++)
                {
                    int ind = Math.Min((int)fftIndex, fftPowerR.Length - 1);
                    if (ind != prevInd)
                    {
                        double y = cSpecR.ActualHeight - (cSpecR.ActualHeight * fftPowerR[ind] / clamp) - move;
                        polylineR.Points.Add(new Point(x, y));
                        prevInd = ind;
                    }
                    fftIndex += fftStep;
                }
                cSpecR.Children.Add(polylineR);

                // Wave
                double zoom = swzoom.Maximum - WaveZoom + 1;
                int bufSize = (int)(cWaveL.ActualWidth * zoom);
                audioBufL = GetBuffer(bufSize, 0);

                double audiomul = (1.0f / 32768.0f) * cWaveL.ActualHeight;

                polylineL = new Polyline();
                polylineL.Stroke = AudioLBrush;
                double waveIndex = 0;

                for (int x = 0; x < cWaveL.ActualWidth; x++)
                {
                    int index = (int)waveIndex % audioBufL.Length;
                    double y = cWaveL.ActualHeight / 2 - audioBufL[index] * audiomul;
                    polylineL.Points.Add((new Point(x, y)));
                    waveIndex += zoom;
                }

                cWaveL.Children.Clear();
                cWaveL.Children.Add(polylineL);

                bufSize = (int)(cWaveR.ActualWidth * zoom);
                audioBufR = GetBuffer(bufSize, 1);

                polylineR = new Polyline();
                polylineR.Stroke = AudioRBrush;
                waveIndex = 0;

                for (int x = 0; x < cWaveR.ActualWidth; x++)
                {
                    int index = (int)waveIndex % audioBufR.Length;
                    double y = cWaveR.ActualHeight / 2 - audioBufR[index] * audiomul;
                    polylineR.Points.Add((new Point(x, y)));
                    waveIndex += zoom;
                }
                cWaveR.Children.Clear();
                cWaveR.Children.Add(polylineR);

            };
        }

        private void DrawDbText()
        {
            volTextCanvas.Children.Clear();

            double y = 0;
            while (y < volTextCanvas.ActualHeight)
            {
                Brush b = TryFindResource("GridLinesBrush") as SolidColorBrush;
                Line l = new Line() { X1 = volTextCanvas.ActualWidth - 10, X2 = volTextCanvas.ActualWidth, Y1 = y, Y2 = y, SnapsToDevicePixels = true, Stroke = b, StrokeThickness = 1, ClipToBounds = false };
                volTextCanvas.Children.Add(l);

                if (y + 2 >= volTextCanvas.ActualHeight)
                    break;

                TextBlock tb = new TextBlock();
                Canvas.SetRight(tb, 3);
                Canvas.SetTop(tb, y);

                double v = (volTextCanvas.ActualHeight - y) / volTextCanvas.ActualHeight;

                int newamp = v == 0 ? 0 : (int)Math.Round(Decibel.ToAmplitude(v * (MinAmp + Decibel.FromAmplitude((double)0xfffe / 0x4000)) - MinAmp) * 0x4000);

                string dbTxt = newamp > 0 ? string.Format("{0:F0}dB", Decibel.FromAmplitude(newamp * (1.0 / 0x4000))) : "-inf.dB";

                if (y + 18 + 2 >= volTextCanvas.ActualHeight)
                    dbTxt = "-inf.dB";

                tb.Text = dbTxt;
                tb.FontSize = 11;
                tb.Foreground = TryFindResource("TextForeground") as SolidColorBrush;
                volTextCanvas.Children.Add((tb));

                y += 18;
            }
        }

        private void FillAudioBuffer(float[] samples)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                AudioBuffer[audioBufferFillPosition] = samples[i];
                audioBufferFillPosition++;
                audioBufferFillPosition %= AUDIO_BUFFER_SIZE;
            }
        }

        private double[] GetBuffer(int buf_size, int channel)
        {
            //int buf_size = FFT_SIZE;
            double[] ret = new double[buf_size];

            int pos = (((audioBufferFillPosition - buf_size * 2) % AUDIO_BUFFER_SIZE + AUDIO_BUFFER_SIZE)) % AUDIO_BUFFER_SIZE;

            for (int i = 0; i < buf_size; i++)
            {
                if (channel < 2)
                {
                    ret[i] = AudioBuffer[(pos + channel) % AUDIO_BUFFER_SIZE];
                }
                else
                {
                    ret[i] = (AudioBuffer[pos % AUDIO_BUFFER_SIZE] + AudioBuffer[(pos + 1) % AUDIO_BUFFER_SIZE]) / 2.0;
                }

                pos += 2;
            }

            return ret;
        }

        internal ResourceDictionary GetThemeResources()
        {
            ResourceDictionary skin = new ResourceDictionary();

            try
            {
                string selectedTheme = Global.Buzz.SelectedTheme == "<default>" ? "Default" : Global.Buzz.SelectedTheme;
                string skinPath = Global.BuzzPath + "\\Themes\\" + selectedTheme + "\\MachineView\\SignalAnalysisWindow.xaml";
                skin = (ResourceDictionary)XamlReaderEx.LoadHack(skinPath);
            }
            catch (Exception)
            {
                string skinPath = Global.BuzzPath + "\\Themes\\Default\\MachineView\\SignalAnalysisWindow.xaml";
                skin.Source = new Uri(skinPath, UriKind.Absolute);
            }

            return skin;
        }
    }
}
