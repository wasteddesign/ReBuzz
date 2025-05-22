using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using Spectrogram;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WDE.AudioBlock
{
    /// <summary>
    /// WaveCanvas handles the actual wave drawing.
    /// </summary>
    public partial class WaveCanvas : Canvas, INotifyPropertyChanged
    {
        AudioBlock audioBlockMachine;
        private int audioBlockIndex;
        private Brush waveBrush;

        private static int MAXIMUN_SAMPLE_READ_SIZE = 1200;
        private static double DRAW_SCALE = 0.6;

        internal double patternLengthInSeconds = 5.0;
        private double drawAccuracy = 1;
        private bool selectedWave;

        private bool updateGraphFlag = false;

        private ViewOrientationMode viewOrientationMode = ViewOrientationMode.Vertical;

        private double slidingWindowOffsetSeconds = 0;

        private double playPositionLinePos = 0;
        private Line playPositionLine = null;

        public ContextMenu CmWaveCanvas { get; }

        private Canvas backgroundWaveCanvas;

        MenuItem menuItemQuality;
        MenuItem menuItemSpectrogramEnabled;
        MenuItem miSpectrogramSettings;
        DispatcherTimer dispatcherTimerUpdatePlayPosition;

        DispatcherTimer dispatcherTimerUpdateWaveGraph;

        Envelopes envelopes;

        public Brush WaveBrush { get => waveBrush; set => waveBrush = value; }

        public double PatternLengthInSeconds
        {
            get => patternLengthInSeconds;

            set
            {
                patternLengthInSeconds = value;
                envelopes.DrawLengthInSeconds = patternLengthInSeconds;
            }
        }

        public bool SelectedWave
        {
            get { return selectedWave; }
            internal set
            {
                selectedWave = value;
                if (SelectedWave)
                {
                    this.Background = new SolidColorBrush(Utils.ChangeColorBrightness(Global.Buzz.ThemeColors["SE Pattern Box"], 0.4f));
                }
                else
                    this.Background = new SolidColorBrush(Global.Buzz.ThemeColors["SE Pattern Box"]);
            }
        }

        public double DrawAccuracy { get => drawAccuracy; set => drawAccuracy = value; }
        public int AudioBlockIndex { get => audioBlockIndex; set => audioBlockIndex = value; }
        public bool UpdateGraphFlag { get => updateGraphFlag; set => updateGraphFlag = value; }
        internal ViewOrientationMode ViewOrientationMode { get => viewOrientationMode; set => viewOrientationMode = value; }
        public double SlidingWindowOffsetSeconds { get => slidingWindowOffsetSeconds; set => slidingWindowOffsetSeconds = value; }
        public MenuItem MenuItemQuality { get => menuItemQuality; set => menuItemQuality = value; }
        public MenuItem MenuItemSpectrogram { get => menuItemSpectrogramEnabled; set => menuItemSpectrogramEnabled = value; }
        public int SpectrogramLowFreq { get; private set; }
        public int SpectrogramHighFreq { get; private set; }
        public int SpectrogramIntensity { get; private set; }
        public Colormap? SpectrogramColorMap { get; private set; }

        public bool EnableResizing { get; set; }

        private bool displayPlayPosition = true;
        public bool DisplayPlayPosition
        {
            get { return displayPlayPosition; }
            set
            {
                displayPlayPosition = value;
                if (displayPlayPosition)
                {
                    dispatcherTimerUpdatePlayPosition.Start();
                }
                else
                {
                    dispatcherTimerUpdatePlayPosition.Stop();
                }
            }
        }

        public bool DrawBackground { get; set; }
        public double TickHeight { get; internal set; }
        public bool PatternEditorWave { get; internal set; }
        public bool DrawPatternText { get; internal set; }
        public bool ReDrawAfterStopped { get; private set; }
        public int Time { get; internal set; }

        private MenuItem miLooping;
        private MenuItem miLoopEnabled;
        private MenuItem menuItemSnapToTick;
        private MenuItem menuItemSnapToBeat;
        private MenuItem menuItemSnapTo;


        public WaveCanvas(AudioBlock audioBlockMachine, int audioBlockIndex)
        {
            InitializeComponent();

            TickHeight = 0;
            PatternEditorWave = false;
            DrawPatternText = true;

            MAXIMUN_SAMPLE_READ_SIZE = AudioBlock.Settings.WaveGraphDetail * 100;

            SelectedWave = false;
            EnableResizing = true;
            DrawBackground = true;

            WaveBrush = Brushes.Red;

            this.audioBlockMachine = audioBlockMachine;
            this.AudioBlockIndex = audioBlockIndex;

            this.SnapsToDevicePixels = true;
            backgroundWaveCanvas = new Canvas() { SnapsToDevicePixels = true };

            this.Background = Utils.CreateLGBackgroundBrush(ViewOrientationMode == ViewOrientationMode.Horizontal);

            CmWaveCanvas = new ContextMenu() { Margin = new Thickness(4, 4, 4, 4) };
            this.ContextMenu = CmWaveCanvas;
            CreateMenu();

            this.Loaded += WaveCanvas_Loaded;

            //AudioBlock.Settings.PropertyChanged += Settings_PropertyChanged;
            //audioBlockMachine.UpdateWaveGraph += AudioBlockMachine_UpdateWaveGraph;
            //Global.Buzz.PropertyChanged += Buzz_PropertyChanged;

            //audioBlockMachine.PropertyChanged += AudioBlockMachine_PropertyChanged;


            dispatcherTimerUpdatePlayPosition = new DispatcherTimer(DispatcherPriority.Background);
            dispatcherTimerUpdatePlayPosition.Interval = new TimeSpan(0, 0, 0, 0, 1000 / 20);
            dispatcherTimerUpdatePlayPosition.Tick += DispatcherTimerPlayPosition_Tick;
            dispatcherTimerUpdatePlayPosition.Start();

            playPositionLine = Utils.CreateLine(this, -1, -1, -1, -1, new SolidColorBrush(Global.Buzz.ThemeColors["SE Song Position"]), 1);

            this.Unloaded += WaveCanvas_Unloaded;

            envelopes = new Envelopes(audioBlockMachine, audioBlockIndex, this) { SnapsToDevicePixels = true };

            SizeChanged += (sender, e) =>
            {
                envelopes.Width = e.NewSize.Width;
                envelopes.Height = e.NewSize.Height;
            };

            // This will create a bitmap copy of backgroundWaveCanvas and that is used as cache to speed up rendering.
            // backgroundWaveCanvas.CacheMode = new BitmapCache() { EnableClearType = false, SnapsToDevicePixels = false };

            

            SpectrogramLowFreq = 0;
            SpectrogramHighFreq = 8000;
            SpectrogramIntensity = 2;
            SpectrogramColorMap = Colormap.viridis;

            dispatcherTimerUpdateWaveGraph = new DispatcherTimer();
            dispatcherTimerUpdateWaveGraph.Interval = new TimeSpan(0, 0, 0, 0, 1000 / 30);
            dispatcherTimerUpdateWaveGraph.Tick += DispatcherTimerUpdateWaveGraph_Tick;
            dispatcherTimerUpdateWaveGraph.Dispatcher.Thread.IsBackground = true;
        }

        private void AudioBlockMachine_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "UpdateMenus")
            {
                WaveCanvas wc = (WaveCanvas)sender;
                if (AudioBlockIndex != wc.AudioBlockIndex)
                    return;

                DisableEvents();
                UpdateMenu();
                this.ContextMenu.IsOpen = false;
                EnableEvents();
            }
        }

        private void Buzz_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (/*PatternEditorWave && */(e.PropertyName == "BPM" || e.PropertyName == "TPB"))
            {
                if (Global.Buzz.Playing || Global.Buzz.Recording)
                    this.ReDrawAfterStopped = true;
                else
                {
                    ReDrawAfterStopped = false;
                    audioBlockMachine.SetPatternLength(AudioBlockIndex);
                    UpdateGraph();
                }
            }
            else if (/*PatternEditorWave && */(e.PropertyName == "Playing") && ReDrawAfterStopped)
            {
                if (!Global.Buzz.Playing && !Global.Buzz.Recording)
                {
                    ReDrawAfterStopped = false;
                    audioBlockMachine.SetPatternLength(AudioBlockIndex);
                    UpdateGraph();
                }
            }
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "WaveGraphDetail")
            {
                MAXIMUN_SAMPLE_READ_SIZE = AudioBlock.Settings.WaveGraphDetail * 100;
                UpdateGraph();
            }
        }

        private void CreateMenu()
        {
            ContextMenu cmWaveCanvas = this.ContextMenu;

            cmWaveCanvas.Items.Remove(miLooping);
            cmWaveCanvas.Items.Remove(menuItemSnapTo);
            cmWaveCanvas.Items.Remove(menuItemSpectrogramEnabled);
            cmWaveCanvas.Items.Remove(miSpectrogramSettings);

            miLooping = new MenuItem();
            miLooping.Header = "Looping";
            cmWaveCanvas.Items.Add(miLooping);
            miLoopEnabled = new MenuItem();
            miLoopEnabled.Header = "Enabled";
            miLoopEnabled.IsCheckable = true;
            miLoopEnabled.IsChecked = audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].LoopEnabled;
            miLoopEnabled.Checked += Mi_Checked_Looping;
            miLoopEnabled.Unchecked += Mi_Unchecked_Looping;
            miLooping.Items.Add(miLoopEnabled);

            // Open loop disabled until I figure out other approach            
            /*
            miOpenLoop = new MenuItem();
            miOpenLoop.Header = "Loop Repeat";
            miLooping.Items.Add(miOpenLoop);

            miOpenLoopEnabled = new MenuItem();
            miOpenLoopEnabled.Header = "Open Loop";
            miOpenLoopEnabled.IsCheckable = true;
            miOpenLoopEnabled.IsEnabled = true;
            miOpenLoopEnabled.IsChecked = audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].OpenLoop;
            miOpenLoopEnabled.Checked += Mi_Checked_OpenLoop;
            miOpenLoopEnabled.Unchecked += Mi_Unchecked_OpenLoop;
            miOpenLoop.Items.Add(miOpenLoopEnabled);

            MenuItem mi = new MenuItem();
            mi.Header = "Loop Length...";            
            miOpenLoop.Items.Add(mi);
            */

            UpdateLoopMenu();

            menuItemSnapTo = new MenuItem();
            menuItemSnapTo.Header = "Snap To...";
            cmWaveCanvas.Items.Add(menuItemSnapTo);

            menuItemSnapToTick = new MenuItem();
            menuItemSnapToTick.Header = "Tick";
            menuItemSnapToTick.IsCheckable = true;
            menuItemSnapToTick.IsChecked = audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].SnapToTick;
            menuItemSnapTo.Items.Add(menuItemSnapToTick);

            menuItemSnapToBeat = new MenuItem();
            menuItemSnapToBeat.Header = "Beat";
            menuItemSnapToBeat.IsCheckable = true;
            menuItemSnapToBeat.IsChecked = audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].SnapToBeat;
            menuItemSnapTo.Items.Add(menuItemSnapToBeat);

            miSpectrogramSettings = new MenuItem();
            miSpectrogramSettings.Header = "Spectrogram Settings...";
            miSpectrogramSettings.Click += MiSpectrogramSettings_Click;
            miSpectrogramSettings.IsEnabled = audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].SpectrogramEnabled;


            menuItemSpectrogramEnabled = new MenuItem();
            menuItemSpectrogramEnabled.Header = "Spectrogram";
            menuItemSpectrogramEnabled.IsCheckable = true;
            menuItemSpectrogramEnabled.IsChecked = audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].SpectrogramEnabled;
            menuItemSpectrogramEnabled.Checked += MenuItemSpectrogram_Checked;
            menuItemSpectrogramEnabled.Unchecked += MenuItemSpectrogram_Unchecked;
            cmWaveCanvas.Items.Add(menuItemSpectrogramEnabled);
            cmWaveCanvas.Items.Add(miSpectrogramSettings);

            cmWaveCanvas.IsOpen = false;
        }

        private void UpdateMenu()
        {
            ContextMenu cmWaveCanvas = this.ContextMenu;
            miLoopEnabled.IsChecked = audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].LoopEnabled;

            UpdateLoopMenu();

            menuItemSnapToTick.IsChecked = audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].SnapToTick;
            menuItemSnapToBeat.IsChecked = audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].SnapToBeat;
            miSpectrogramSettings.IsEnabled = audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].SpectrogramEnabled;

            menuItemSpectrogramEnabled.IsChecked = audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].SpectrogramEnabled;

            cmWaveCanvas.IsOpen = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void MiSpectrogramSettings_Click(object sender, RoutedEventArgs e)
        {
            ResourceDictionary rd = audioBlockMachine.host.Machine.ParameterWindow != null ? audioBlockMachine.host.Machine.ParameterWindow.Resources : null;
            InputDialogSpectrogram inputDialogSpectrogram = new InputDialogSpectrogram(rd, SpectrogramLowFreq, SpectrogramHighFreq, SpectrogramIntensity, (int)SpectrogramColorMap);
            inputDialogSpectrogram.ShowDialog();
            if (inputDialogSpectrogram.DialogResult.HasValue && inputDialogSpectrogram.DialogResult.Value)
            {
                SpectrogramHighFreq = inputDialogSpectrogram.numHighFreq.Value < inputDialogSpectrogram.numLowFreq.Value + 1000 ?
                    (int)inputDialogSpectrogram.numLowFreq.Value + 1000 : (int)inputDialogSpectrogram.numHighFreq.Value;
                SpectrogramLowFreq = (int)inputDialogSpectrogram.numLowFreq.Value;
                SpectrogramIntensity = (int)inputDialogSpectrogram.numIntensity.Value;
                SpectrogramColorMap = (Colormap)inputDialogSpectrogram.cbColorMap.SelectedIndex;
                UpdateGraph();
            }
        }

        private void MenuItemSpectrogram_Unchecked(object sender, RoutedEventArgs e)
        {
            audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].SpectrogramEnabled = false;
            this.audioBlockMachine.RaiseUpdateWaveGraphEvent(audioBlockIndex, WaveUpdateEventType.PatternChanged);
            audioBlockMachine.RaisePropertyUpdateMenu(this);
        }

        private void MenuItemSpectrogram_Checked(object sender, RoutedEventArgs e)
        {
            audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].SpectrogramEnabled = true;
            this.audioBlockMachine.RaiseUpdateWaveGraphEvent(audioBlockIndex, WaveUpdateEventType.PatternChanged);
            audioBlockMachine.RaisePropertyUpdateMenu(this);
        }

        private void WaveCanvas_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (!PatternEditorWave)
            {
                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                {
                    double direction = 1;
                    if (AudioBlock.Settings.InvMouseWheelZoom)
                        direction = -1;

                    Slider sliderZoom = audioBlockMachine.AudioBlockGUI.WaveView.SliderZoom;
                    sliderZoom.Value += direction * sliderZoom.Maximum / e.Delta;
                    e.Handled = true;
                }
                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    Slider sliderWindow = audioBlockMachine.AudioBlockGUI.WaveView.SliderWindow;
                    sliderWindow.Value += sliderWindow.Maximum / e.Delta;
                    e.Handled = true;
                }
            }
        }

        private void UpdateLoopMenu()
        {
            if (miLoopEnabled.IsChecked)
            {
                for (int i = 1; i < miLooping.Items.Count; i++)
                    ((MenuItem)miLooping.Items[i]).IsEnabled = true;
            }
            else
            {
                for (int i = 1; i < miLooping.Items.Count; i++)
                    ((MenuItem)miLooping.Items[i]).IsEnabled = false;
            }
        }

        private void Mi_Unchecked_OpenLoop(object sender, RoutedEventArgs e)
        {
            audioBlockMachine.SetOpenLoopState(audioBlockIndex, false);
            audioBlockMachine.AudioBlockGUI.WaveView.UpdateScale();
            audioBlockMachine.UpdateEnvPoints(audioBlockIndex);
            audioBlockMachine.NotifyBuzzDataChanged();
            this.UpdateGraph();
        }

        private void Mi_Checked_OpenLoop(object sender, RoutedEventArgs e)
        {
            audioBlockMachine.SetOpenLoopState(audioBlockIndex, true);
            audioBlockMachine.AudioBlockGUI.WaveView.UpdateScale();
            audioBlockMachine.UpdateEnvPoints(audioBlockIndex);
            audioBlockMachine.NotifyBuzzDataChanged();
            this.UpdateGraph();
        }

        private void Mi_Unchecked_Looping(object sender, RoutedEventArgs e)
        {
            audioBlockMachine.SetLoopingState(audioBlockIndex, false);
            audioBlockMachine.UpdateEnvPoints(audioBlockIndex);
            audioBlockMachine.SetPatternLength(this.audioBlockIndex);
            audioBlockMachine.NotifyBuzzDataChanged();

            audioBlockMachine.RaisePropertyUpdateMenu(this);
            this.audioBlockMachine.RaiseUpdateWaveGraphEvent(audioBlockIndex);
        }

        private void Mi_Checked_Looping(object sender, RoutedEventArgs e)
        {
            audioBlockMachine.SetLoopingState(audioBlockIndex, true);
            audioBlockMachine.NotifyBuzzDataChanged();
            audioBlockMachine.UpdateEnvPoints(audioBlockIndex);
            audioBlockMachine.RaisePropertyUpdateMenu(this);
            this.audioBlockMachine.RaiseUpdateWaveGraphEvent(audioBlockIndex);
        }



        /// <summary>
        /// Update play position line.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
   
        private void DispatcherTimerPlayPosition_Tick(object sender, EventArgs e)
        {
            if (this.Visibility == Visibility.Visible && (Global.Buzz.Playing || Global.Buzz.Recording))
            {
                playPositionLinePos = this.audioBlockMachine.GetPlayPositionInPatternName(audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].Pattern);
                if (playPositionLinePos >= 0)
                {
                    playPositionLinePos = UpdatePlayPosIfLooped(playPositionLinePos);

                    UpdatePlayLine();
                    playPositionLine.Visibility = Visibility.Visible;
                }
                else
                {
                    playPositionLinePos = UpdatePlayPosIfLooped(playPositionLinePos);

                    UpdatePlayLine();
                    playPositionLine.Visibility = Visibility.Hidden;
                }
            }
        }

        /// <summary>
        /// Update play position if looping AudioBlock item.
        /// </summary>
        /// <param name="playPos"></param>
        /// <returns></returns>
        private double UpdatePlayPosIfLooped(double playPos)
        {
            double ret = playPos;

            if (audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].LoopEnabled &&
                !audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].OpenLoop)
            {
                lock (AudioBlock.syncLock)
                {

                    IWavetable wt = audioBlockMachine.host.Machine.Graph.Buzz.Song.Wavetable;
                    

                    int sampleNum = audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].WavetableIndex;
                    double step = ((double)audioBlockMachine.GetSampleFrequency(sampleNum)) / ((double)Global.Buzz.SelectedAudioDriverSampleRate);

                    var targetLayer = wt.Waves[sampleNum].Layers.Last();

                    string patternName = audioBlockMachine.MachineState.AudioBlockInfoTable[AudioBlockIndex].Pattern;
                    double offset = (audioBlockMachine.GetOffsetForPatternInMs(patternName) / 1000.0) * (double)targetLayer.SampleRate;


                    double loopStart = targetLayer.LoopStart;
                    double loopEnd = targetLayer.LoopEnd;
                    double loopLength = loopEnd - loopStart;

                    if (playPos * step > loopStart - offset)
                    {
                        ret = (((playPos * step) - (loopStart - offset)) % loopLength);
                        ret += loopStart - offset;
                        ret /= step;
                    }
                }
            }

            return ret;
        }

        public void DisableEvents()
        {
            menuItemSnapToTick.Checked -= MenuItemSnapToTick_Checked;
            menuItemSnapToTick.Unchecked -= MenuItemSnapToTick_Unchecked;

            menuItemSnapToBeat.Checked -= MenuItemSnapToBeat_Checked;
            menuItemSnapToBeat.Unchecked -= MenuItemSnapToBeat_Unchecked;

            this.PreviewDragEnter -= WaveCanvas_PreviewDragEnter;
            this.PreviewDragOver -= WaveCanvas_PreviewDragOver;
            this.Drop -= WaveCanvas_Drop;
            this.AllowDrop = false;
        }

        public void EnableEvents()
        {
            menuItemSnapToTick.Checked += MenuItemSnapToTick_Checked;
            menuItemSnapToTick.Unchecked += MenuItemSnapToTick_Unchecked;

            menuItemSnapToBeat.Checked += MenuItemSnapToBeat_Checked;
            menuItemSnapToBeat.Unchecked += MenuItemSnapToBeat_Unchecked;

            this.PreviewDragEnter += WaveCanvas_PreviewDragEnter;
            this.PreviewDragOver += WaveCanvas_PreviewDragOver;
            this.Drop += WaveCanvas_Drop;
            this.AllowDrop = true;
        }

        private void WaveCanvas_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void WaveCanvas_PreviewDragEnter(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void WaveCanvas_Drop(object sender, DragEventArgs e)
        {
            Utils.DragDropHelper(audioBlockMachine, audioBlockIndex, sender, e);
        }

        private void MenuItemSnapToBeat_Unchecked(object sender, RoutedEventArgs e)
        {
            DisableEvents();
            audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].SnapToBeat = false;
            audioBlockMachine.RaisePropertyUpdateMenu(this);
            EnableEvents();
        }

        private void MenuItemSnapToBeat_Checked(object sender, RoutedEventArgs e)
        {
            DisableEvents();
            audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].SnapToBeat = true;
            audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].SnapToTick = false;
            audioBlockMachine.RaisePropertyUpdateMenu(this);
            EnableEvents();
        }

        private void MenuItemSnapToTick_Unchecked(object sender, RoutedEventArgs e)
        {
            DisableEvents();
            audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].SnapToTick = false;
            audioBlockMachine.RaisePropertyUpdateMenu(this);
            EnableEvents();
        }

        private void MenuItemSnapToTick_Checked(object sender, RoutedEventArgs e)
        {
            DisableEvents();
            audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].SnapToTick = true;
            audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].SnapToBeat = false;
            audioBlockMachine.RaisePropertyUpdateMenu(this);
            EnableEvents();
        }


        private double CalculatePlayPosInCanvas()
        {
            double ret;
            if (ViewOrientationMode == ViewOrientationMode.Vertical)
            {
                double sampleLength = ((double)Global.Buzz.SelectedAudioDriverSampleRate) * PatternLengthInSeconds;
                ret = (playPositionLinePos - SlidingWindowOffsetSeconds * (double)Global.Buzz.SelectedAudioDriverSampleRate) * (this.ActualHeight / (double)sampleLength);
            }
            else
            {
                double sampleLength = ((double)Global.Buzz.SelectedAudioDriverSampleRate) * PatternLengthInSeconds;
                ret = (playPositionLinePos - SlidingWindowOffsetSeconds * (double)Global.Buzz.SelectedAudioDriverSampleRate) * (this.ActualWidth / (double)sampleLength);
            }

            return double.IsNaN(ret) ? 0 : ret;
        }

        public void UpdatePlayLine()
        {
            if (ViewOrientationMode == ViewOrientationMode.Vertical)
            {
                double posInCanvas = CalculatePlayPosInCanvas();
                Utils.UpdateLine(playPositionLine, 0, posInCanvas, this.Width, posInCanvas);
            }
            else
            {
                double posInCanvas = CalculatePlayPosInCanvas();
                Utils.UpdateLine(playPositionLine, posInCanvas, 0, posInCanvas, this.Height);
            }
        }

        /// <summary>
        /// Do not update graph here because it is expensive. Rather raise a flag and do it OnRender.
        /// </summary>
        /// <param name="ab"></param>
        /// <param name="e"></param>
        private void AudioBlockMachine_UpdateWaveGraph(AudioBlock ab, EventArgs e)
        {
            EventArgsWaveUpdate eau = (EventArgsWaveUpdate)e;
            if (eau.Type == WaveUpdateEventType.MachineClosing)
            {
                WaveCanvas_Unloaded(null, null);
            }
            if (this.AudioBlockIndex == eau.AudioBlockIndex)
            {
                if (eau.Type == WaveUpdateEventType.ColorChanged)
                {
                    int color = audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].Color;
                    if (color > -1)
                        WaveBrush = new SolidColorBrush(Color.FromRgb((byte)((color & 0xFF0000) >> 16), (byte)((color & 0xFF00) >> 8), (byte)(color & 0xFF)));
                }

                if (this.IsVisible)
                {
                    if (!audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].SpectrogramEnabled)
                    {
                        UpdateGraph();
                    }
                    else if (eau.Type == WaveUpdateEventType.ChangeRate ||
                         eau.Type == WaveUpdateEventType.ChangeTempo ||
                         eau.Type == WaveUpdateEventType.ChangePitch ||
                         eau.Type == WaveUpdateEventType.PatternChanged ||
                         eau.Type == WaveUpdateEventType.Drag)
                    {
                        UpdateGraph();
                    }
                }
                else
                {
                    dispatcherTimerUpdateWaveGraph.Stop();
                    UpdateGraphFlag = true;
                }
            }
        }

        /// <summary>
        /// Don't do heavy redrawing until allowed.
        /// </summary>
        /// <param name="dc"></param>
        protected override void OnRender(DrawingContext dc)
        {
            if (UpdateGraphFlag)
            {
                UpdateGraph();
                UpdateGraphFlag = false;
            }

            base.OnRender(dc);
        }

        /// <summary>
        /// Handles the actual drawing of the whole graph view...
        /// </summary>
        public void UpdateGraph()
        {
            dispatcherTimerUpdateWaveGraph.Stop();

            this.Children.Clear();
            this.ToolTip = null;

            Line playLineTmp = playPositionLine;

            //this.CacheMode = new BitmapCache() { EnableClearType = false, SnapsToDevicePixels = false };

            backgroundWaveCanvas.Children.Clear();
            backgroundWaveCanvas.Width = this.Width;
            backgroundWaveCanvas.Height = this.Height;

            this.Children.Add(backgroundWaveCanvas);

            if (AudioBlock.Settings.ShowEnvelopeLimits)
            {
                if (ViewOrientationMode == ViewOrientationMode.Vertical)
                {
                    DrawEnvelopeLimitsVer(this, Brushes.Yellow);
                }
                else
                {
                    DrawEnvelopeLimitsHor(this, Brushes.Yellow);
                }
            }

            envelopes.ViewOrientationMode = this.ViewOrientationMode;
            this.Children.Add(envelopes);
            
            string patternName = audioBlockMachine.MachineState.AudioBlockInfoTable[AudioBlockIndex].Pattern;
            if (patternName == "")
                return;

            int waveIndex = audioBlockMachine.MachineState.AudioBlockInfoTable[AudioBlockIndex].WavetableIndex;
            if (waveIndex < 0 || Global.Buzz.Song.Wavetable.Waves[waveIndex] == null)
            {
                envelopes.Visibility = Visibility.Collapsed;
                ContextMenu = null;
            }
            else
            {
                envelopes.Visibility = Visibility.Visible;
                ContextMenu = CmWaveCanvas;
                UpdateMenu();
                //patternLengthInSeconds = audioBlockMachine.GetPatternLenghtInSeconds(AudioBlockIndex);
            }

            if (DrawPatternText)
                Utils.DrawText(this, audioBlockMachine.MachineState.AudioBlockInfoTable[AudioBlockIndex].Pattern);

            if (envelopes.Visibility != Visibility.Collapsed)
                envelopes.Draw();

            // Play position line is updated on top of wave
            if (playLineTmp != null && DisplayPlayPosition)
                this.Children.Add(playLineTmp);

            // Thread.Sleep(10); // Drawing is very heavy operation so give time to other threads.
            UpdatePlayLine();

            Thumb thumb = new Thumb();
            thumb.CacheMode = new BitmapCache() { EnableClearType = false, SnapsToDevicePixels = false };

            if (ViewOrientationMode == ViewOrientationMode.Vertical)
            {
                thumb.Width = 14;
                thumb.Height = 40;
                Canvas.SetTop(thumb, this.Height / 2.0 - thumb.Height / 2.0);
                Canvas.SetRight(thumb, 0);
            }
            else
            {
                thumb.Width = 40;
                thumb.Height = 14;
                Canvas.SetBottom(thumb, 0);
                Canvas.SetLeft(thumb, this.Width / 2.0 - thumb.Width / 2.0);
            }

            thumb.DragStarted += Thumb_DragStarted;
            thumb.DragDelta += Thumb_DragDelta;
            thumb.DragCompleted += Thumb_DragCompleted;
            thumb.MouseEnter += Thumb_MouseEnter;
            thumb.MouseLeave += Thumb_MouseLeave;

            DropShadowEffect fx = new DropShadowEffect();
            fx.BlurRadius = 8;
            fx.Direction = 0;
            fx.Opacity = 1;
            fx.ShadowDepth = 0;

            thumb.Effect = fx;

            if (EnableResizing)
                this.Children.Add(thumb);

            if (ViewOrientationMode == ViewOrientationMode.Vertical)
            {
                if (audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].SpectrogramEnabled)
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    DrawVerticalSpectrogram(backgroundWaveCanvas);
                    Mouse.OverrideCursor = null;
                }
                else
                {
                    DrawVerticalWave(backgroundWaveCanvas);
                }
            }
            else
            {
                if (audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].SpectrogramEnabled)
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    DrawHorizontalSpectrogram(backgroundWaveCanvas);
                    Mouse.OverrideCursor = null;
                }
                else
                {
                    DrawHorizontalWave(backgroundWaveCanvas);
                }
            }

            
        }

        private void Thumb_MouseLeave(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = null;
        }

        private void Thumb_MouseEnter(object sender, MouseEventArgs e)
        {
            if (ViewOrientationMode == ViewOrientationMode.Vertical)
            {
                Mouse.OverrideCursor = Cursors.SizeWE;
            }
            else
            {
                Mouse.OverrideCursor = Cursors.SizeNS;
            }
        }

        private void Thumb_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            UpdateGraph();
        }

        private void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (ViewOrientationMode == ViewOrientationMode.Vertical)
            {
                double xadjust = this.Width + e.HorizontalChange;
                if (xadjust > 50)
                    Width = xadjust;
            }
            else
            {
                double yadjust = this.Height + e.VerticalChange;
                if (yadjust > 50)
                    Height = yadjust;
            }
        }

        private void Thumb_DragStarted(object sender, DragStartedEventArgs e)
        {
        }

        private void WaveCanvas_Loaded(object sender, RoutedEventArgs e)
        {
            AudioBlock.Settings.PropertyChanged += Settings_PropertyChanged;
            audioBlockMachine.UpdateWaveGraph += AudioBlockMachine_UpdateWaveGraph;
            Global.Buzz.PropertyChanged += Buzz_PropertyChanged;

            this.MouseWheel += WaveCanvas_MouseWheel;

            audioBlockMachine.PropertyChanged += AudioBlockMachine_PropertyChanged;
            UpdateGraph();
            EnableEvents();
        }

        private void WaveCanvas_Unloaded(object sender, RoutedEventArgs e)
        {
            DisableEvents();

            this.MouseWheel -= WaveCanvas_MouseWheel;

            dispatcherTimerUpdatePlayPosition.Stop();
            dispatcherTimerUpdateWaveGraph.Stop();
            if (audioBlockMachine != null) audioBlockMachine.UpdateWaveGraph -= AudioBlockMachine_UpdateWaveGraph;
            AudioBlock.Settings.PropertyChanged -= Settings_PropertyChanged;
            Global.Buzz.PropertyChanged -= Buzz_PropertyChanged;
            if (audioBlockMachine != null) audioBlockMachine.PropertyChanged -= AudioBlockMachine_PropertyChanged;
            //audioBlockMachine = null;
            Children.Clear();
            ContextMenu = null;
        }

        internal void ChangeOrientation(ViewOrientationMode orientationMode)
        {
            this.ViewOrientationMode = orientationMode;
            double tmpSize = this.Width;
            this.Width = this.Height;
            this.Height = tmpSize;

            envelopes.ViewOrientationMode = this.ViewOrientationMode;
            envelopes.Width = this.Width;
            envelopes.Height = this.Height;
        }

        // Common for background thread functions
        private double drawScale;
        private int readSize;
        private double readPos;
        private double readStep;
        private int numPoints;
        private int numPointsPos;
        private int numPointsRead;
        private bool isStereo;
        private double offset;
        private Point[] pointsL;
        private Point[] pointsR;
        private Sample[] output;
        private Canvas bgCanvasAsync;
        private Polygon LeftWave;
        private Polygon RightWave;

        /// <summary>
        /// All this is a bit hacky. I wanted to reuse some functions in AudioBlock.cs and that forces some weirdish code. At some point add proper functions
        /// that do exactly what is needed...
        /// 
        /// Also Canvas could be separated in to different class to embed multiple waves to window.
        /// </summary>
        /// <param name="canvas"></param>
        public void DrawVerticalWave(Canvas canvas)
        {
            bgCanvasAsync = canvas;

            dispatcherTimerUpdateWaveGraph.Stop();
            StartNewVerticalWaveBackgroundThread();
        }

        public void StartNewVerticalWaveBackgroundThread()
        {
            Canvas canvas = bgCanvasAsync;

            Brush strokeBrush = new SolidColorBrush(Color.FromArgb(0x70, 0, 0, 0));

            IWavetable wt = audioBlockMachine.host.Machine.Graph.Buzz.Song.Wavetable;

            int sampleNum = audioBlockMachine.MachineState.AudioBlockInfoTable[AudioBlockIndex].WavetableIndex;
            if (sampleNum < 0 || wt.Waves[sampleNum] == null || patternLengthInSeconds == 0 || canvas.Height == 0)
                return;

            var targetLayer = wt.Waves[sampleNum].Layers.Last();

            string patternName = audioBlockMachine.MachineState.AudioBlockInfoTable[AudioBlockIndex].Pattern;
            isStereo = audioBlockMachine.IsStereo(sampleNum);
            offset = (audioBlockMachine.GetOffsetForPatternInMs(patternName) / 1000.0 + SlidingWindowOffsetSeconds) * (double)targetLayer.SampleRate;
            double sampleLength;
            sampleLength = ((double)Global.Buzz.SelectedAudioDriverSampleRate) * PatternLengthInSeconds;

            //if (SelectedWave)
            //{
            //    this.Background = new SolidColorBrush(Utils.ChangeColorBrightness(Global.Buzz.ThemeColors["SE Pattern Box"], 0.4f));
            //}

            if (DrawBackground)
            {
                this.Background.Opacity = 1;
                this.Background = Utils.CreateLGBackgroundBrush(ViewOrientationMode == ViewOrientationMode.Horizontal);
            }
            else
                this.Background.Opacity = 0;


            drawScale = DRAW_SCALE * audioBlockMachine.MachineState.AudioBlockInfoTable[AudioBlockIndex].Gain;

            Brush tickBrush = new SolidColorBrush(Utils.ChangeColorBrightness(Global.Buzz.ThemeColors["SE Pattern Box"], -0.3f));
            DrawTickLinesForVerWave(canvas, offset, sampleNum, sampleLength, tickBrush);

            double realHeight = canvas.Height * (patternLengthInSeconds / canvas.Height) * GetPixelsPerSecond();
            // Only draw every x pixel to speed up graph
            readStep = (double)sampleLength / (realHeight / DrawAccuracy);

            readSize = (int)readStep < MAXIMUN_SAMPLE_READ_SIZE ? (int)readStep : MAXIMUN_SAMPLE_READ_SIZE;
            readSize = readSize == 0 ? 1 : readSize;

            // double readPos = 0;

            numPoints = (int)(canvas.Height / DrawAccuracy);
            pointsL = new Point[numPoints];
            pointsR = new Point[numPoints];

            output = new Sample[readSize];

            canvas.Children.Remove(LeftWave);
            canvas.Children.Remove(RightWave);
            LeftWave = new Polygon();
            RightWave = new Polygon();

            LeftWave.Height = canvas.Height;
            LeftWave.Fill = WaveBrush;
            LeftWave.Stroke = strokeBrush;
            LeftWave.StrokeThickness = 1;

            RightWave.Fill = WaveBrush;
            RightWave.Stroke = strokeBrush;
            RightWave.StrokeThickness = 1;

            canvas.Children.Add(LeftWave);
            if (isStereo)
                canvas.Children.Add(RightWave);

            // backgroundWorkerDrawVerticalWave.RunWorkerAsync();

            numPointsRead = 0;
            numPointsPos = 0;
            readPos = 0;
            dispatcherTimerUpdateWaveGraph.Start();

            Utils.DrawBox(backgroundWaveCanvas);

            DrawOffsetForVerWave(canvas, offset, sampleNum, sampleLength, strokeBrush);

            if (audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].LoopEnabled)
                DrawLoopPointsForVerticalWave(canvas, sampleNum, sampleLength, Brushes.Yellow);
        }

        private void GetSamplesInSmallChunks(int audioBlockWave, ref Sample[] output, int readSize, double pos, double readStep)
        {
            int array_size = 400;
            int samples_read = 0;
            int samples_remaining = readSize;

            double readSubStep = readStep / (readSize / (double)array_size);
            double readSubPos = pos;

            do
            {
                int readSubSize = samples_remaining > array_size ? array_size : samples_remaining;
                Sample[] tmbBuffer = new Sample[readSubSize];
                GetSamples(AudioBlockIndex, ref tmbBuffer, readSubSize, readSubPos);

                for (int i = 0; i < readSubSize; i++)
                {
                    output[samples_read + i] = tmbBuffer[i];
                }

                samples_read += readSubSize;
                samples_remaining -= readSubSize;
                readSubPos += readSubStep;

            } while (samples_remaining > 0);
        }


        private void UpdateVerticalWave()
        {
            //Application.Current.Dispatcher.BeginInvoke(
            //new Action(() =>
            //{
            if (ViewOrientationMode != ViewOrientationMode.Vertical)
                return;

            Canvas canvas = bgCanvasAsync;

            IWavetable wt = audioBlockMachine.host.Machine.Graph.Buzz.Song.Wavetable;

            int sampleNum = audioBlockMachine.MachineState.AudioBlockInfoTable[AudioBlockIndex].WavetableIndex;
            if (sampleNum < 0 || wt.Waves[sampleNum] == null)
                return;

            double leftWidth = canvas.Width;
            double leftCenter = canvas.Width / 2.0;
            double rightWidth = canvas.Width / 2.0;
            double rightCenter = canvas.Width * 3.0 / 4.0;

            if (isStereo)
            {
                leftWidth /= 2.0;
                leftCenter /= 2.0;
            }

            LeftWave.Points.Clear();
            RightWave.Points.Clear();

            for (int y = 0; y < numPointsRead; y++)
            {
                Point point = new Point();
                point.X = (pointsL[y].Y * leftWidth * drawScale) + leftCenter;
                point.Y = y * DrawAccuracy;
                LeftWave.Points.Add(point);
            }

            for (int y = numPointsRead - 1; y >= 0; y--)
            {
                Point point = new Point();
                point.X = (pointsL[y].X * leftWidth * drawScale) + leftCenter;
                point.Y = y * DrawAccuracy;
                LeftWave.Points.Add(point);
            }

            if (isStereo)
            {
                for (int y = 0; y < numPointsRead; y++)
                {
                    Point point = new Point();
                    point.X = (pointsR[y].Y * rightWidth * drawScale) + rightCenter;
                    point.Y = y * DrawAccuracy;
                    RightWave.Points.Add(point);
                }

                for (int y = numPointsRead - 1; y >= 0; y--)
                {
                    Point point = new Point();
                    point.X = (pointsR[y].X * rightWidth * drawScale) + rightCenter;
                    point.Y = y * DrawAccuracy;
                    RightWave.Points.Add(point);
                }
            }
            //}));
        }

        private double GetPixelsPerSecond()
        {
            double pixelsPerSecond;
            double drawLenghtInSeconds = PatternLengthInSeconds; // Includes offset
            if (TickHeight > 0)
            {
                pixelsPerSecond = TickHeight * audioBlockMachine.host.MasterInfo.TicksPerSec;
            }
            else
            {
                if (ViewOrientationMode == ViewOrientationMode.Vertical)
                {
                    pixelsPerSecond = Height / drawLenghtInSeconds;
                }
                else
                {
                    pixelsPerSecond = Width / drawLenghtInSeconds;
                }
            }

            return pixelsPerSecond;
        }

        public void DrawHorizontalWave(Canvas canvas)
        {
            bgCanvasAsync = canvas;
            dispatcherTimerUpdateWaveGraph.Stop();
            StartNewHorizontalWaveBackgroundThread();
        }

        public void StartNewHorizontalWaveBackgroundThread()
        {
            Canvas canvas = bgCanvasAsync;

            Brush strokeBrush = new SolidColorBrush(Color.FromArgb(0x70, 0, 0, 0));

            IWavetable wt = audioBlockMachine.host.Machine.Graph.Buzz.Song.Wavetable;

            int sampleNum = audioBlockMachine.MachineState.AudioBlockInfoTable[AudioBlockIndex].WavetableIndex;
            if (sampleNum < 0 || wt.Waves[sampleNum] == null || patternLengthInSeconds == 0 || canvas.Width == 0)
                return;

            var targetLayer = wt.Waves[sampleNum].Layers.Last();

            string patternName = audioBlockMachine.MachineState.AudioBlockInfoTable[AudioBlockIndex].Pattern;
            isStereo = audioBlockMachine.IsStereo(sampleNum);
            offset = (audioBlockMachine.GetOffsetForPatternInMs(patternName) / 1000.0 + SlidingWindowOffsetSeconds) * (double)targetLayer.SampleRate;
            double sampleLength;

            sampleLength = ((double)Global.Buzz.SelectedAudioDriverSampleRate) * PatternLengthInSeconds;

            //if (SelectedWave)
            //{
            //    this.Background = new SolidColorBrush(Utils.ChangeColorBrightness(Global.Buzz.ThemeColors["SE Pattern Box"], 0.4f));
            //}

            if (DrawBackground)
            {
                this.Background.Opacity = 1;
                this.Background = Utils.CreateLGBackgroundBrush(ViewOrientationMode == ViewOrientationMode.Horizontal);
            }
            else
                this.Background.Opacity = 0;

            drawScale = DRAW_SCALE * audioBlockMachine.MachineState.AudioBlockInfoTable[AudioBlockIndex].Gain;

            Brush tickBrush = new SolidColorBrush(Utils.ChangeColorBrightness(Global.Buzz.ThemeColors["SE Pattern Box"], -0.3f));
            DrawTickLinesForHorWave(canvas, offset, sampleNum, sampleLength, tickBrush);

            double realWidth = canvas.Width * (patternLengthInSeconds / canvas.Width) * GetPixelsPerSecond();
            // Only draw every x pixel to speed up graph
            readStep = (double)sampleLength / (realWidth / DrawAccuracy);

            readSize = (int)readStep < MAXIMUN_SAMPLE_READ_SIZE ? (int)readStep : MAXIMUN_SAMPLE_READ_SIZE;
            readSize = readSize == 0 ? 1 : readSize;

            // double readPos = 0;

            numPoints = (int)(canvas.Width / DrawAccuracy);
            pointsL = new Point[numPoints];
            pointsR = new Point[numPoints];

            output = new Sample[readSize];

            canvas.Children.Remove(LeftWave);
            canvas.Children.Remove(RightWave);

            LeftWave = new Polygon();
            RightWave = new Polygon();

            LeftWave.Height = canvas.Height;
            LeftWave.Fill = WaveBrush;
            LeftWave.Stroke = strokeBrush;
            LeftWave.StrokeThickness = 1;


            RightWave.Fill = WaveBrush;
            RightWave.Stroke = strokeBrush;
            RightWave.StrokeThickness = 1;

            canvas.Children.Add(LeftWave);
            if (isStereo)
                canvas.Children.Add(RightWave);

            // backgroundWorkerDrawHorizontalWave.RunWorkerAsync();
            
            numPointsRead = 0;
            numPointsPos = 0;
            readPos = 0;
            dispatcherTimerUpdateWaveGraph.Start();

            Utils.DrawBox(backgroundWaveCanvas);

            if (audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].LoopEnabled)
                DrawLoopPointsForHorizontalWave(canvas, sampleNum, sampleLength, Brushes.Yellow);

            DrawOffsetForHorWave(canvas, offset, sampleNum, sampleLength, strokeBrush);
        }

        private void DispatcherTimerUpdateWaveGraph_Tick(object sender, EventArgs e)
        {
            for (int i = 0; i < 40; i++)
            {
                if (numPointsPos < numPoints)
                {
                    for (int j = 0; j < readSize; j++)
                    {
                        output[j] = new Sample(0.0f, 0.0f);
                    }
                    GetSamplesInSmallChunks(AudioBlockIndex, ref output, readSize, readPos, readStep);

                    readPos += readStep;

                    pointsL[numPointsPos] = GetMinMaxL(ref output, readSize);
                    if (isStereo)
                        pointsR[numPointsPos] = GetMinMaxR(ref output, readSize);

                    numPointsRead++;
                    numPointsPos++;

                    if (numPointsPos % 10 == 0)
                    {
                        if (viewOrientationMode == ViewOrientationMode.Horizontal)
                            UpdateHorizontalWave();
                        else
                            UpdateVerticalWave();
                    }
                }
                else
                {
                    dispatcherTimerUpdateWaveGraph.Stop();
                    if (viewOrientationMode == ViewOrientationMode.Horizontal)
                        UpdateHorizontalWave();
                    else
                        UpdateVerticalWave();

                    break;
                }
            }
        }

        private void UpdateHorizontalWave()
        {
            //Application.Current.Dispatcher.BeginInvoke(
            //new Action(() =>
            //{
            if (ViewOrientationMode != ViewOrientationMode.Horizontal)
                return;

            Canvas canvas = bgCanvasAsync;

            IWavetable wt = audioBlockMachine.host.Machine.Graph.Buzz.Song.Wavetable;

            int sampleNum = audioBlockMachine.MachineState.AudioBlockInfoTable[AudioBlockIndex].WavetableIndex;
            if (sampleNum < 0 || wt.Waves[sampleNum] == null)
                return;

            LeftWave.Points.Clear();
            RightWave.Points.Clear();

            double leftHeight = canvas.Height;
            double leftCenter = canvas.Height / 2.0;
            double rightHeight = canvas.Height / 2.0;
            double rightCenter = canvas.Height * 3.0 / 4.0;

            if (isStereo)
            {
                leftHeight /= 2.0;
                leftCenter /= 2.0;
            }

            for (int x = 0; x < numPointsRead; x++)
            {
                Point point = new Point();
                point.Y = (pointsL[x].Y * leftHeight * drawScale) + leftCenter;
                point.X = x * DrawAccuracy;
                LeftWave.Points.Add(point);
            }

            for (int x = numPointsRead - 1; x >= 0; x--)
            {
                Point point = new Point();
                point.Y = (pointsL[x].X * leftHeight * drawScale) + leftCenter;
                point.X = x * DrawAccuracy;
                LeftWave.Points.Add(point);
            }

            if (isStereo)
            {
                for (int x = 0; x < numPointsRead; x++)
                {
                    Point point = new Point();
                    point.Y = (pointsR[x].Y * rightHeight * drawScale) + rightCenter;
                    point.X = x * DrawAccuracy;
                    RightWave.Points.Add(point);
                }

                for (int x = numPointsRead - 1; x >= 0; x--)
                {
                    Point point = new Point();
                    point.Y = (pointsR[x].X * rightHeight * drawScale) + rightCenter;
                    point.X = x * DrawAccuracy;
                    RightWave.Points.Add(point);
                }
            }
//            }));
        }

        private void DrawOffsetForVerWave(Canvas canvas, double offset, int sampleNum, double sampleLength, Brush strokeBrush)
        {
            // Draw Offset Line
            if (offset < 0)
            {
                double step = ((double)audioBlockMachine.GetSampleFrequency(sampleNum)) / ((double)Global.Buzz.SelectedAudioDriverSampleRate);

                double offsetY = canvas.Height * (-offset / (sampleLength));
                offsetY /= step;

                Line myLine = new Line();

                myLine.Stroke = strokeBrush;
                myLine.SnapsToDevicePixels = true;
                myLine.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
                myLine.StrokeThickness = 1;

                myLine.StrokeDashArray = new DoubleCollection() { 4, 2 };

                myLine.X1 = 0;
                myLine.Y1 = offsetY;
                myLine.X2 = canvas.Width - 1;
                myLine.Y2 = offsetY;

                canvas.Children.Add(myLine);
            }
        }

        private void DrawLoopPointsForVerticalWave(Canvas canvas, int sampleNum, double sampleLength, Brush strokeBrush)
        {
            IWavetable wt = audioBlockMachine.host.Machine.Graph.Buzz.Song.Wavetable;
            var targetLayer = wt.Waves[sampleNum].Layers.Last();
            double step = ((double)audioBlockMachine.GetSampleFrequency(sampleNum)) / ((double)Global.Buzz.SelectedAudioDriverSampleRate);

            string patternName = audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].Pattern;
            double offset;
            double loopStart;
            double loopEnd;

            lock (AudioBlock.syncLock)
            {
                offset = (audioBlockMachine.GetOffsetForPatternInMs(patternName) / 1000.0 + SlidingWindowOffsetSeconds) * (double)targetLayer.SampleRate;

                loopStart = targetLayer.LoopStart;
                loopEnd = targetLayer.LoopEnd;
            }

            double startY = canvas.Height * ((loopStart - offset) / (sampleLength));
            startY /= step;

            double endY = 0;


            if (audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].LoopEnabled &&
                audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].OpenLoop)
            {
                endY = audioBlockMachine.GetPatternLenghtInSeconds(audioBlockIndex) *
                    (double)audioBlockMachine.GetSampleFrequency(sampleNum);
                endY = canvas.Height * endY / sampleLength;
            }
            else
            {
                endY = canvas.Height * ((loopEnd - offset) / (sampleLength));
            }

            endY /= step;

            Line myLine = new Line();

            myLine.Stroke = strokeBrush;
            myLine.SnapsToDevicePixels = true;
            myLine.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
            myLine.StrokeThickness = 1.3;

            myLine.StrokeDashArray = new DoubleCollection() { 3, 2 };

            myLine.X1 = 0;
            myLine.Y1 = startY;
            myLine.X2 = canvas.Width - 1;
            myLine.Y2 = startY;

            canvas.Children.Add(myLine);

            // End
            myLine = new Line();

            myLine.Stroke = strokeBrush;
            myLine.SnapsToDevicePixels = true;
            myLine.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
            myLine.StrokeThickness = 1.3;

            myLine.StrokeDashArray = new DoubleCollection() { 3, 2 };

            myLine.X1 = 0;
            myLine.Y1 = endY;
            myLine.X2 = canvas.Width - 1;
            myLine.Y2 = endY;

            canvas.Children.Add(myLine);
        }

        private void DrawLoopPointsForHorizontalWave(Canvas canvas, int sampleNum, double sampleLength, Brush strokeBrush)
        {
            IWavetable wt = audioBlockMachine.host.Machine.Graph.Buzz.Song.Wavetable;
            var targetLayer = wt.Waves[sampleNum].Layers.Last();
            double step = ((double)audioBlockMachine.GetSampleFrequency(sampleNum)) / ((double)Global.Buzz.SelectedAudioDriverSampleRate);

            string patternName = audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].Pattern;
            double offset;
            double loopEnd;
            double loopStart;

            lock (AudioBlock.syncLock)
            {
                offset = (audioBlockMachine.GetOffsetForPatternInMs(patternName) / 1000.0 + SlidingWindowOffsetSeconds) * (double)targetLayer.SampleRate;

                loopStart = targetLayer.LoopStart;
                loopEnd = targetLayer.LoopEnd;
            }
            double startX = canvas.Width * ((loopStart - offset) / (sampleLength));
            startX /= step;

            double endX = 0;

            if (audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].LoopEnabled &&
                            audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].OpenLoop)
            {
                endX = audioBlockMachine.GetPatternLenghtInSeconds(audioBlockIndex) *
                    (double)audioBlockMachine.GetSampleFrequency(sampleNum);
                endX = canvas.Width * endX / sampleLength;
            }
            else
            {
                endX = canvas.Width * ((loopEnd - offset) / (sampleLength));
            }

            endX /= step;

            Line myLine = new Line();

            myLine.Stroke = strokeBrush;
            myLine.SnapsToDevicePixels = true;
            myLine.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
            myLine.StrokeThickness = 1.3;

            myLine.StrokeDashArray = new DoubleCollection() { 3, 2 };

            myLine.X1 = startX;
            myLine.Y1 = 0;
            myLine.X2 = startX;
            myLine.Y2 = canvas.Height - 1;

            canvas.Children.Add(myLine);

            // End
            myLine = new Line();

            myLine.Stroke = strokeBrush;
            myLine.SnapsToDevicePixels = true;
            myLine.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
            myLine.StrokeThickness = 1.3;

            myLine.StrokeDashArray = new DoubleCollection() { 3, 2 };

            myLine.X1 = endX;
            myLine.Y1 = 0;
            myLine.X2 = endX;
            myLine.Y2 = canvas.Height - 1;

            canvas.Children.Add(myLine);
        }

        private void DrawTickLinesForVerWave(Canvas canvas, double offset, int sampleNum, double sampleLength, Brush strokeBrush)
        {
            double numberOfTicks = patternLengthInSeconds * audioBlockMachine.host.MasterInfo.TicksPerSec;
            double pixelsPerTick = TickHeight > 0 ? TickHeight : canvas.Height / numberOfTicks;

            int tick = (Global.Buzz.TPB - (Time % Global.Buzz.TPB));
            if (tick == Global.Buzz.TPB)
                tick = 0;

            double patPosTPBPixels = tick * pixelsPerTick;
            double start = patPosTPBPixels + 1;

            int ticksPerBeat = audioBlockMachine.host.MasterInfo.TicksPerBeat;
            int ticking = 0;

            if (!PatternEditorWave)
            {
                int nextTick = /*audioBlockMachine.host.MasterInfo.TicksPerBeat -*/ ((int)(SlidingWindowOffsetSeconds * audioBlockMachine.host.MasterInfo.TicksPerSec)) % audioBlockMachine.host.MasterInfo.TicksPerBeat;
                if (nextTick >= Global.Buzz.TPB)
                    nextTick = 0;

                start = (SlidingWindowOffsetSeconds * audioBlockMachine.host.MasterInfo.TicksPerSec);
                start = 1 - (start - Math.Floor(start));
                start *= pixelsPerTick;
                ticking = (int)nextTick + 1;
            }

            if (pixelsPerTick > 10)
            {
                for (double y = start; y < canvas.Height; y += pixelsPerTick)
                {
                    Line myLine = new Line();

                    myLine.Stroke = strokeBrush;
                    myLine.SnapsToDevicePixels = true;
                    myLine.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
                    myLine.StrokeThickness = 1;

                    if (ticking % ticksPerBeat == 0)
                        myLine.StrokeThickness = 2;

                    ticking++;

                    myLine.X1 = 0;
                    myLine.Y1 = y;
                    myLine.X2 = canvas.Width - 1;
                    myLine.Y2 = y;

                    canvas.Children.Add(myLine);
                }
            }
            else if (pixelsPerTick * ticksPerBeat > 10)
            {
                for (double y = start; y < canvas.Height; y += pixelsPerTick)
                {
                    if (ticking % ticksPerBeat == 0)
                    {
                        Line myLine = new Line();

                        myLine.Stroke = strokeBrush;
                        myLine.SnapsToDevicePixels = true;
                        myLine.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
                        myLine.StrokeThickness = 1;

                        myLine.X1 = 0;
                        myLine.Y1 = y;
                        myLine.X2 = canvas.Width - 1;
                        myLine.Y2 = y;

                        canvas.Children.Add(myLine);
                    }

                    ticking++;
                }
            }
        }

        private void DrawTickLinesForHorWave(Canvas canvas, double offset, int sampleNum, double sampleLength, Brush strokeBrush)
        {   
            double numberOfTicks = patternLengthInSeconds * audioBlockMachine.host.MasterInfo.TicksPerSec;
            double pixelsPerTick = TickHeight > 0 ? TickHeight : canvas.Width / numberOfTicks;

            int tick = (Global.Buzz.TPB - (Time % Global.Buzz.TPB));
            if (tick == Global.Buzz.TPB)
                tick = 0;

            double patPosTPBPixels = tick * pixelsPerTick;
            double start = patPosTPBPixels + 1;

            int ticksPerBeat = audioBlockMachine.host.MasterInfo.TicksPerBeat;
            int ticking = 0;// ((int)(offsetInPixels / pixelsPerTick)) % ticksPerBeat + 1;

            if (!PatternEditorWave)
            {
                int nextTick = /*audioBlockMachine.host.MasterInfo.TicksPerBeat -*/ ((int)(SlidingWindowOffsetSeconds * audioBlockMachine.host.MasterInfo.TicksPerSec)) % audioBlockMachine.host.MasterInfo.TicksPerBeat;
                if (nextTick >= Global.Buzz.TPB)
                    nextTick = 0;

                start = (SlidingWindowOffsetSeconds * audioBlockMachine.host.MasterInfo.TicksPerSec);
                start = 1 - (start - Math.Floor(start));
                start *= pixelsPerTick;
                ticking = (int)nextTick + 1;
            }

            if (pixelsPerTick > 10)
            {
                for (double x = start; x < canvas.Width; x += pixelsPerTick)
                {
                    Line myLine = new Line();

                    myLine.Stroke = strokeBrush;
                    myLine.SnapsToDevicePixels = true;
                    myLine.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
                    myLine.StrokeThickness = 1;

                    if (ticking % ticksPerBeat == 0)
                        myLine.StrokeThickness = 2;

                    ticking++;

                    myLine.X1 = x;
                    myLine.Y1 = 0;
                    myLine.X2 = x;
                    myLine.Y2 = canvas.Height - 1;

                    canvas.Children.Add(myLine);
                }
            }
            else if (pixelsPerTick * ticksPerBeat > 10)
            {
                for (double x = start; x < canvas.Width; x += pixelsPerTick)
                {
                    if (ticking % ticksPerBeat == 0)
                    {
                        Line myLine = new Line();

                        myLine.Stroke = strokeBrush;
                        myLine.SnapsToDevicePixels = true;
                        myLine.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
                        myLine.StrokeThickness = 1;

                        myLine.X1 = x;
                        myLine.Y1 = 0;
                        myLine.X2 = x;
                        myLine.Y2 = canvas.Height - 1;

                        canvas.Children.Add(myLine);
                    }

                    ticking++;
                }
            }
        }

        private void DrawOffsetForHorWave(Canvas canvas, double offset, int sampleNum, double sampleLength, Brush strokeBrush)
        {
            offset += SlidingWindowOffsetSeconds;
            // Draw Offset Line
            if (offset < 0)
            {
                double step = ((double)audioBlockMachine.GetSampleFrequency(sampleNum)) / ((double)Global.Buzz.SelectedAudioDriverSampleRate);

                double offsetX = canvas.Width * (-offset / (sampleLength));
                offsetX /= step;

                Line myLine = new Line();

                myLine.Stroke = strokeBrush;
                myLine.SnapsToDevicePixels = true;
                myLine.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
                myLine.StrokeThickness = 1;

                myLine.StrokeDashArray = new DoubleCollection() { 4, 2 };

                myLine.X1 = offsetX;
                myLine.Y1 = 0;
                myLine.X2 = offsetX;
                myLine.Y2 = canvas.Height - 1;

                canvas.Children.Add(myLine);
            }
        }

        private void DrawEnvelopeLimitsHor(Canvas canvas, Brush strokeBrush)
        {
            double y = canvas.Height * ((1.0 - EnvelopeBase.ENVELOPE_VIEW_SCALE_ADJUST) / 2.0);
            Line myLine = new Line();

            myLine.Stroke = strokeBrush;
            myLine.SnapsToDevicePixels = true;
            myLine.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
            myLine.StrokeThickness = 1;

            myLine.Opacity = 0.6;

            myLine.X1 = 0;
            myLine.Y1 = y;
            myLine.X2 = canvas.Width;
            myLine.Y2 = y;
            myLine.StrokeDashArray = new DoubleCollection() { 4, 4 };

            canvas.Children.Add(myLine);

            myLine = new Line();

            myLine.Stroke = strokeBrush;
            myLine.SnapsToDevicePixels = true;
            myLine.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
            myLine.StrokeThickness = 1;
            myLine.StrokeDashArray = new DoubleCollection() { 4, 4 };

            y = canvas.Height * (1.0 - (1.0 - EnvelopeBase.ENVELOPE_VIEW_SCALE_ADJUST) / 2.0);
            myLine.Opacity = 0.6;

            myLine.X1 = 0;
            myLine.Y1 = y;
            myLine.X2 = canvas.Width;
            myLine.Y2 = y;

            canvas.Children.Add(myLine);
        }

        private void DrawEnvelopeLimitsVer(Canvas canvas, Brush strokeBrush)
        {
            double x = canvas.Width * ((1.0 - EnvelopeBase.ENVELOPE_VIEW_SCALE_ADJUST) / 2.0);
            Line myLine = new Line();

            myLine.Stroke = strokeBrush;
            myLine.SnapsToDevicePixels = true;
            myLine.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
            myLine.StrokeThickness = 1;

            myLine.Opacity = 0.6;
            myLine.X1 = x;
            myLine.Y1 = 0;
            myLine.X2 = x;
            myLine.Y2 = canvas.Height;
            myLine.StrokeDashArray = new DoubleCollection() { 4, 4 };

            canvas.Children.Add(myLine);

            myLine = new Line();
            myLine.Opacity = 0.6;
            myLine.Stroke = strokeBrush;
            myLine.SnapsToDevicePixels = true;
            myLine.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
            myLine.StrokeThickness = 1;
            myLine.StrokeDashArray = new DoubleCollection() { 4, 4 };

            x = canvas.Width * (1.0 - (1.0 - EnvelopeBase.ENVELOPE_VIEW_SCALE_ADJUST) / 2.0);

            myLine.X1 = x;
            myLine.Y1 = 0;
            myLine.X2 = x;
            myLine.Y2 = canvas.Height;

            canvas.Children.Add(myLine);
        }

        public void DrawHorizontalSpectrogram(Canvas canvas)
        {
            IWavetable wt = audioBlockMachine.host.Machine.Graph.Buzz.Song.Wavetable;
            Brush strokeBrush = new SolidColorBrush(Color.FromArgb(0x70, 0, 0, 0));

            int sampleNum = audioBlockMachine.MachineState.AudioBlockInfoTable[AudioBlockIndex].WavetableIndex;
            if (sampleNum < 0)
                return;

            var targetLayer = wt.Waves[sampleNum].Layers.Last();

            string patternName = audioBlockMachine.MachineState.AudioBlockInfoTable[AudioBlockIndex].Pattern;
            bool isStereo = audioBlockMachine.IsStereo(sampleNum);
            double offset = (audioBlockMachine.GetOffsetForPatternInMs(patternName) / 1000.0 + SlidingWindowOffsetSeconds) * (double)targetLayer.SampleRate;
            double sampleLength;

            sampleLength = ((double)Global.Buzz.SelectedAudioDriverSampleRate) * PatternLengthInSeconds;

            //if (SelectedWave)
            //{
            //    this.Background = new SolidColorBrush(Utils.ChangeColorBrightness(Global.Buzz.ThemeColors["SE Pattern Box"], 0.4f));
            //}

            this.Background = Utils.CreateLGBackgroundBrush(ViewOrientationMode == ViewOrientationMode.Horizontal);

            double drawScale = DRAW_SCALE * audioBlockMachine.MachineState.AudioBlockInfoTable[AudioBlockIndex].Gain;

            // Only draw every x pixel to speed up graph

            int readSize = MAXIMUN_SAMPLE_READ_SIZE;

            double readPos = 0;

            int len = (int)sampleLength;

            float[] pointsL = new float[len];
            float[] pointsR = new float[len];
            int readOffset = 0;

            Sample[] output = new Sample[readSize];

            while (readOffset < len)
            {
                if (readSize > len - readOffset)
                {
                    readSize = len - readOffset;
                }

                for (int j = 0; j < readSize; j++)
                {
                    output[j] = new Sample(0.0f, 0.0f);
                }


                GetSamples(AudioBlockIndex, ref output, readSize, readPos);

                for (int j = 0; j < readSize; j++)
                {
                    pointsL[readOffset] = output[j].L * 32768.0f;
                    pointsR[readOffset] = output[j].R * 32768.0f;
                    readOffset++;
                }

                // Do we need this?
                // if (readPos % 50 == 0)
                //    Thread.Sleep(1);

                readPos += readSize;
            }

            // ToDo: Get Overall Min/Max to scale the wave

            double leftHeight = canvas.Height;
            double leftCenter = canvas.Height / 2.0;
            double rightHeight = canvas.Height / 2.0;
            double rightCenter = canvas.Height * 3.0 / 4.0;

            if (isStereo)
            {
                leftHeight /= 2.0;
                leftCenter /= 2.0;
            }
            double realWidth = canvas.Width * (patternLengthInSeconds / canvas.Width) * GetPixelsPerSecond();

            var spec = new Spectrogram.Spectrogram(sampleRate: Global.Buzz.SelectedAudioDriverSampleRate, fftSize: 4096, step: (int)(sampleLength / (canvas.Width)));
            spec.AddExtend(pointsL);

            var bmp = spec.GetBitmap(intensity: SpectrogramIntensity, freqLow: SpectrogramLowFreq, freqHigh: SpectrogramHighFreq, colormap: SpectrogramColorMap);

            BitmapImage bmSpec = Utils.ToBitmapImage(bmp, Rotation.Rotate0);
            var image = new System.Windows.Controls.Image();
            image.Source = bmSpec;
            image.Stretch = Stretch.Fill;
            image.Height = leftHeight;
            image.Width = realWidth;
            canvas.Children.Add(image);

            if (isStereo)
            {
                spec = new Spectrogram.Spectrogram(sampleRate: Global.Buzz.SelectedAudioDriverSampleRate, fftSize: 4096, step: (int)(sampleLength / (canvas.Width)));
                spec.AddExtend(pointsR);

                bmp = spec.GetBitmap(intensity: SpectrogramIntensity, freqLow: SpectrogramLowFreq, freqHigh: SpectrogramHighFreq, colormap: SpectrogramColorMap);

                bmSpec = Utils.ToBitmapImage(bmp, Rotation.Rotate0);
                image = new System.Windows.Controls.Image();
                image.Source = bmSpec;
                image.Stretch = Stretch.Fill;
                image.Height = rightHeight;
                image.Width = realWidth;
                Canvas.SetTop(image, leftHeight);
                canvas.Children.Add(image);
            }

            Brush tickBrush = new SolidColorBrush(Utils.ChangeColorBrightness(Global.Buzz.ThemeColors["SE Pattern Box"], -0.3f));
            tickBrush.Opacity = 0.5;
            DrawTickLinesForHorWave(canvas, offset, sampleNum, sampleLength, tickBrush);

            // canvas.Children.Add(LeftWave);
            // if (isStereo)
            //    canvas.Children.Add(RightWave);

            Utils.DrawBox(backgroundWaveCanvas);

            if (audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].LoopEnabled)
                DrawLoopPointsForHorizontalWave(canvas, sampleNum, sampleLength, Brushes.Yellow);

            DrawOffsetForHorWave(canvas, offset, sampleNum, sampleLength, strokeBrush);

            canvas.ToolTip = "Low: " + SpectrogramLowFreq + " Hz, High: " + SpectrogramHighFreq + " Hz";
            ToolTipService.SetShowDuration(canvas, 3000);
        }

        public void DrawVerticalSpectrogram(Canvas canvas)
        {
            IWavetable wt = audioBlockMachine.host.Machine.Graph.Buzz.Song.Wavetable;
            Brush strokeBrush = new SolidColorBrush(Color.FromArgb(0x70, 0, 0, 0));

            int sampleNum = audioBlockMachine.MachineState.AudioBlockInfoTable[AudioBlockIndex].WavetableIndex;
            if (sampleNum < 0)
                return;

            var targetLayer = wt.Waves[sampleNum].Layers.Last();

            string patternName = audioBlockMachine.MachineState.AudioBlockInfoTable[AudioBlockIndex].Pattern;
            bool isStereo = audioBlockMachine.IsStereo(sampleNum);
            double offset = (audioBlockMachine.GetOffsetForPatternInMs(patternName) / 1000.0 + SlidingWindowOffsetSeconds) * (double)targetLayer.SampleRate;
            double sampleLength;

            sampleLength = ((double)Global.Buzz.SelectedAudioDriverSampleRate) * PatternLengthInSeconds;

            //if (SelectedWave)
            //{
            //    this.Background = new SolidColorBrush(Utils.ChangeColorBrightness(Global.Buzz.ThemeColors["SE Pattern Box"], 0.4f));
            //}

            this.Background = Utils.CreateLGBackgroundBrush(ViewOrientationMode == ViewOrientationMode.Horizontal);

            double drawScale = DRAW_SCALE * audioBlockMachine.MachineState.AudioBlockInfoTable[AudioBlockIndex].Gain;

            // Only draw every x pixel to speed up graph

            int readSize = MAXIMUN_SAMPLE_READ_SIZE;

            double readPos = 0;

            int len = (int)sampleLength;

            float[] pointsL = new float[len];
            float[] pointsR = new float[len];
            int readOffset = 0;

            Sample[] output = new Sample[readSize];

            while (readOffset < len)
            {
                if (readSize > len - readOffset)
                {
                    readSize = len - readOffset;
                }

                for (int j = 0; j < readSize; j++)
                {
                    output[j] = new Sample(0.0f, 0.0f);
                }


                GetSamples(AudioBlockIndex, ref output, readSize, readPos);

                for (int j = 0; j < readSize; j++)
                {
                    pointsL[readOffset] = output[j].L * 32768.0f;
                    pointsR[readOffset] = output[j].R * 32768.0f;
                    readOffset++;
                }

                // Do we need this?
                // if (readPos % 50 == 0)
                //    Thread.Sleep(1);

                readPos += readSize;
            }

            // ToDo: Get Overall Min/Max to scale the wave

            double leftWidth = canvas.Width;
            double leftCenter = canvas.Width / 2.0;
            double rightWidth = canvas.Width / 2.0;
            double rightCenter = canvas.Width * 3.0 / 4.0;

            if (isStereo)
            {
                leftWidth /= 2.0;
                leftCenter /= 2.0;
            }

            var spec = new Spectrogram.Spectrogram(sampleRate: Global.Buzz.SelectedAudioDriverSampleRate, fftSize: 4096, step: (int)(sampleLength / (canvas.Height)));
            spec.AddExtend(pointsL);

            var bmp = spec.GetBitmap(intensity: SpectrogramIntensity, freqLow: SpectrogramLowFreq, freqHigh: SpectrogramHighFreq, colormap: SpectrogramColorMap);

            double realHeight = canvas.Height * (patternLengthInSeconds / canvas.Height) * GetPixelsPerSecond();

            BitmapImage bmSpec = Utils.ToBitmapImage(bmp, Rotation.Rotate90);
            var image = new System.Windows.Controls.Image();

            image.Source = bmSpec;
            image.Height = realHeight;
            image.Width = leftWidth;
            image.Stretch = Stretch.Fill;
            canvas.Children.Add(image);

            if (isStereo)
            {
                spec = new Spectrogram.Spectrogram(sampleRate: Global.Buzz.SelectedAudioDriverSampleRate, fftSize: 4096, step: (int)(sampleLength / (canvas.Height)));
                spec.AddExtend(pointsR);

                bmp = spec.GetBitmap(intensity: SpectrogramIntensity, freqLow: SpectrogramLowFreq, freqHigh: SpectrogramHighFreq, colormap: SpectrogramColorMap);

                bmSpec = Utils.ToBitmapImage(bmp, Rotation.Rotate90);
                image = new System.Windows.Controls.Image();
                image.Source = bmSpec;

                image.Stretch = Stretch.Fill;
                image.Height = realHeight;
                image.Width = rightWidth;
                Canvas.SetLeft(image, leftWidth);
                canvas.Children.Add(image);
            }

            Brush tickBrush = new SolidColorBrush(Utils.ChangeColorBrightness(Global.Buzz.ThemeColors["SE Pattern Box"], -0.3f));
            tickBrush.Opacity = 0.5;
            DrawTickLinesForVerWave(canvas, offset, sampleNum, sampleLength, tickBrush);

            Utils.DrawBox(backgroundWaveCanvas);

            if (audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].LoopEnabled)
                DrawLoopPointsForVerticalWave(canvas, sampleNum, sampleLength, Brushes.Yellow);

            DrawOffsetForVerWave(canvas, offset, sampleNum, sampleLength, strokeBrush);

            canvas.ToolTip = "Low: " + SpectrogramLowFreq + " Hz, High: " + SpectrogramHighFreq + " Hz";
            ToolTipService.SetShowDuration(canvas, 3000);
        }

        private Point GetMinMaxL(ref Sample[] output, int readSize)
        {
            float min = 100000;
            float max = -100000;

            for (int i = 0; i < readSize; i++)
            {
                min = Math.Min(output[i].L, min);
                max = Math.Max(output[i].L, max);
            }
            return new Point(min, max);
        }

        private Point GetMinMaxR(ref Sample[] output, int readSize)
        {
            float min = 100000;
            float max = -100000;

            for (int i = 0; i < readSize; i++)
            {
                min = Math.Min(output[i].R, min);
                max = Math.Max(output[i].R, max);
            }
            return new Point(min, max);
        }

        private void GetSamples(int audioBlockWave, ref Sample[] output, int readSize, double pos)
        {

            double playPositionInSample = pos;

            int sampleIndex = audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockWave].WavetableIndex;
            if (sampleIndex >= 0)
            {
                double step;
                step = audioBlockMachine.GetSampleFrequency(sampleIndex) / (double)Global.Buzz.SelectedAudioDriverSampleRate;

                Sample[] outputTmp = new Sample[(int)(Math.Ceiling(readSize * step))];
                double offset = (double)audioBlockMachine.GetSampleFrequency(sampleIndex) * (SlidingWindowOffsetSeconds + audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockWave].OffsetInSeconds +
                    audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockWave].OffsetInMs / 1000.0);

                playPositionInSample *= step;
                playPositionInSample += offset;

                double patternLenght = 0.0;
                bool cutIfLooped = false;
                // Don't draw beyond loop
                if (audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].LoopEnabled &&
                    audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].OpenLoop)
                {
                    playPositionInSample = audioBlockMachine.UpdatePlayPosIfLooped(playPositionInSample, sampleIndex, audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].Pattern);
                    patternLenght = audioBlockMachine.GetPatternLenghtInSeconds(audioBlockIndex) *
                        (double)Global.Buzz.SelectedAudioDriverSampleRate;
                    cutIfLooped = true;
                }

                int outputOffset = 0;

                // Offest in the beginning...
                if (playPositionInSample < 0)
                {
                    for (int i = 0; i < readSize; i++)
                    {
                        playPositionInSample += step;
                        outputOffset++;
                        if (playPositionInSample >= 0)
                            break;
                    }
                }

                if (cutIfLooped && (pos > patternLenght))
                {
                    // Pattern ended --> fill zeroes
                    for (int i = 0; i < readSize - outputOffset; i++)
                    {
                        output[i + outputOffset].L = 0;
                        output[i + outputOffset].R = 0;
                    }
                }
                else
                {
                    audioBlockMachine.GetSamplesLowPriority(sampleIndex, ref outputTmp, outputTmp.Length, (int)(playPositionInSample), false);

                    double stepper = 0.0;
                    for (int i = 0; i < readSize - outputOffset; i++)
                    {
                        output[i + outputOffset].L = outputTmp[(int)stepper].L;
                        output[i + outputOffset].R = outputTmp[(int)stepper].R;
                        stepper += step;
                    }
                }
            }
        }
    }
}
