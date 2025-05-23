using BuzzGUI.Common;
using BuzzGUI.Common.DSP;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WDE.ConnectionMixer
{
    public enum EMIDIControlType
    {
        Mute,
        Solo,
        Pan,
        Volume,
        P1,
        P2,
        P3,
        P4
    };

    /// <summary>
    /// Interaction logic for MixerControl.xaml
    /// </summary>
    public partial class MixerControl : UserControl, INotifyPropertyChanged
    {
        private IMachineConnection selectedConnection;

        const int PanRight = 0x8000;
        const int PanCenter = 0x4000;
        const int amp12 = 0xFFFE;
        const int amp0 = 0x4000;
        const double MinAmp = 66;

        DispatcherTimer timer;
        DispatcherTimer peakTimerL;
        DispatcherTimer peakTimerR;
        DispatcherTimer dragTimer;
        DispatcherTimer dtVolTooltip;
        float maxSampleL;
        float maxSampleR;
        float peakSampleL;
        float peakSampleR;
        private bool volDragging = false;
        private bool sliderDragging;
        private ConnectionMixer cmm;

        public int MixerNumber { get; set; }

        public string VUMeterToolTip { get; set; }
        public double VUMeterLevelL { get; set; }
        public double VUMeterLevelR { get; set; }
        public double VUMeterRMSLevelL { get; set; }
        public double VUMeterRMSLevelR { get; set; }

        const double VUMeterRange = 80.0;

        private LinearGradientBrush VUMEterBrush;
        private bool dropPeakL;
        private bool dropPeakR;
        private const double dropPeakDb = 1.0;

        public IParameter[] MachineParameters = new IParameter[ConnectionMixer.NUM_PARAMS];
        public Slider[] ParamSliders = new Slider[ConnectionMixer.NUM_PARAMS];
        public Menu[] ParamMenus = new Menu[ConnectionMixer.NUM_PARAMS];

        SolidColorBrush topBGBrush = Brushes.DarkGray;
        SolidColorBrush topBGBrushSelected = Brushes.Gray;
        SolidColorBrush topBGBrushMouseOver = Brushes.DarkGray;

        Point oldValPosition;
        Point prevMousePosition;

        public IMachineConnection SelectedConnection
        {
            get { return selectedConnection; }
            set
            {
                if (selectedConnection != null)
                {
                    selectedConnection.Tap -= SelectedConnection_Tap;
                    timer.Stop();
                    AssignToMachineEvents(selectedConnection, false);
                }

                // Init stuff
                selectedConnection = null;
                volLevel.ToolTip = null;
                peakLineL.Visibility = peakLineR.Visibility = Visibility.Collapsed;
                tbSolo.IsEnabled = true;
                sliderPan.IsEnabled = true;
                peakSampleL = peakSampleR = 0;
                textBlockVol.Text = textBlockPan.Text = "";

                selectedConnection = value;
                if (selectedConnection != null)
                {
                    selectedConnection.Tap += SelectedConnection_Tap;
                    UpdateConnectionVolume();

                    //sliderPan.IsEnabled = selectedConnection.Destination.DLL.Info.Type == MachineType.Master || selectedConnection.Destination.HasStereoInput == true;

                    textBlockVol.Text = UpdateVolText();
                    textBlockPan.Text = UpdatePanText();

                    ToolTip tt = new ToolTip();
                    tt.Placement = System.Windows.Controls.Primitives.PlacementMode.Relative;
                    volLevel.ToolTip = tt;

                    tt = new ToolTip();
                    tt.Placement = System.Windows.Controls.Primitives.PlacementMode.Relative;
                    sliderPan.ToolTip = tt;

                    timer.Start();

                    AssignToMachineEvents(selectedConnection, true);
                }
            }
        }

        bool isSoloed;
        public bool IsSoloed
        {
            get { return isSoloed; }
            set
            {
                isSoloed = value;
                if (selectedConnection != null)
                {
                    AssignToMachineEvents(selectedConnection, false);

                    List<IMachine> machines = new List<IMachine>();
                    FindRootGeneratorMachines(selectedConnection, machines);

                    foreach (IMachine machine in machines)
                    {
                        machine.IsSoloed = isSoloed;
                    }

                    AssignToMachineEvents(selectedConnection, true);
                }
            }
        }

        internal void SetRootGeneratorMachineSoloState(IMachineConnection c, bool solo)
        {
            if (c != null)
            {
                List<IMachine> machines = new List<IMachine>();
                FindRootGeneratorMachines(c, machines);

                foreach (IMachine machine in machines)
                {
                    machine.IsSoloed = solo;
                }
            }
        }

        internal bool AllRootGeneratorMachinesSoloed(IMachineConnection c)
        {
            bool result = true;

            if (c != null)
            {
                List<IMachine> machines = new List<IMachine>();
                FindRootGeneratorMachines(c, machines);

                foreach (IMachine machine in machines)
                {
                    result = result && machine.IsSoloed;
                }
            }

            return result;
        }

        internal void AssignToMachineEvents(IMachineConnection c, bool assign)
        {
            List<IMachine> machines = new List<IMachine>();

            FindRootGeneratorMachines(c, machines);

            foreach (IMachine machine in machines)
            {
                if (assign)
                    machine.PropertyChanged += Machine_PropertyChanged;
                else
                    machine.PropertyChanged -= Machine_PropertyChanged;
            }
        }

        private void FindRootGeneratorMachines(IMachineConnection c, List<IMachine> machines)
        {
            if (c.Source.DLL.Info.Type != MachineType.Generator)
            {
                if (c.Source.Inputs.Count == 0)
                    return;
                else
                    foreach (IMachineConnection cnext in c.Source.Inputs)
                        FindRootGeneratorMachines(cnext, machines);
            }
            else
                machines.Add(c.Source);
        }

        private void Machine_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsSoloed")
            {
                var mac = sender as IMachine;
                // If any gen is not soloed, then button not pressed
                if (mac.IsSoloed == false)
                    isSoloed = false;
                else if (AllRootGeneratorMachinesSoloed(SelectedConnection))
                    isSoloed = true;

                PropertyChanged.Raise(this, "IsSoloed");
            }
        }

        private void UpdateConnectionVolume()
        {
            int v = SelectedConnection.Amp;
            double ampLine;
            if (v <= 0)
                ampLine = 0;
            else if (v >= 0xfffe)
                ampLine = 1.0;
            else
                ampLine = Math.Min(1, (Decibel.FromAmplitude(v / (double)0x4000) + MinAmp) / (MinAmp + Decibel.FromAmplitude(0xfffe / (double)0x4000)));

            volLevel.Y1 = volLevel.Y2 = volCanvas.ActualHeight - volCanvas.ActualHeight * ampLine;
        }

        private void SelectedConnection_Tap(float[] arg1, bool arg2, SongTime arg3)
        {
            if (!arg2) // Mono
            {
                maxSampleL = Math.Max(maxSampleL, DSP.AbsMax(arg1) * (1.0f / 32768.0f));
                maxSampleR = maxSampleL;
            }
            else
            {
                float[] L = new float[arg1.Length / 2];
                float[] R = new float[arg1.Length / 2];
                for (int i = 0; i < arg1.Length / 2; i++)
                {
                    L[i] = arg1[i * 2];
                    R[i] = arg1[i * 2 + 1];
                }

                maxSampleL = Math.Max(maxSampleL, DSP.AbsMax(L) * (1.0f / 32768.0f));
                maxSampleR = Math.Max(maxSampleR, DSP.AbsMax(R) * (1.0f / 32768.0f));
            }

            if (peakSampleL <= maxSampleL)
            {
                dropPeakL = false;
                peakTimerL.Start(); // Reset timer

                peakSampleL = maxSampleL;
            }

            if (peakSampleR <= maxSampleR)
            {
                dropPeakR = false;
                peakTimerR.Start(); // Reset timer

                peakSampleR = maxSampleR;
            }
        }


        void SetPeakTimer()
        {
            peakTimerL = new DispatcherTimer();
            peakTimerL.Interval = TimeSpan.FromMilliseconds(1500);
            peakTimerL.Tick += (sender, e) =>
            {
                dropPeakL = true;
            };

            peakTimerR = new DispatcherTimer();
            peakTimerR.Interval = TimeSpan.FromMilliseconds(1500);
            peakTimerR.Tick += (sender, e) =>
            {
                dropPeakR = true;
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

                if (dropPeakL && connectionMixerGUI.DropPeak)
                {
                    peakSampleL = (float)Decibel.ToAmplitude(Decibel.FromAmplitude(peakSampleL) - dropPeakDb);
                }
                if (dropPeakR && connectionMixerGUI.DropPeak)
                {
                    peakSampleR = (float)Decibel.ToAmplitude(Decibel.FromAmplitude(peakSampleR) - dropPeakDb);
                }

                // Draw peak
                if (peakSampleL >= maxSampleL && peakSampleL > 0)
                {
                    var db = Math.Min(Math.Max(Decibel.FromAmplitude(peakSampleL), -VUMeterRange), 0.0);
                    var pos = (db + VUMeterRange) / VUMeterRange;
                    peakLineL.Y1 = peakLineL.Y2 = volCanvas.ActualHeight - pos * volCanvas.ActualHeight;

                    if (VUMEterBrush != null)
                    {
                        double peakPos = pos;// 1.0 - peakLineL.Y1 / volCanvas.ActualHeight;
                        Color c = GetRelativeColor(VUMEterBrush.GradientStops, peakPos);
                        peakLineL.Stroke = new SolidColorBrush(c);
                    }

                    if (peakLineL.Y1 >= volCanvas.ActualHeight)
                        peakLineL.Visibility = Visibility.Collapsed;
                    else
                        peakLineL.Visibility = Visibility.Visible;
                }

                if (peakSampleR >= maxSampleR && peakSampleR > 0)
                {
                    var db = Math.Min(Math.Max(Decibel.FromAmplitude(peakSampleR), -VUMeterRange), 0.0);
                    var pos = (db + VUMeterRange) / VUMeterRange;
                    peakLineR.Y1 = peakLineR.Y2 = volCanvas.ActualHeight - pos * volCanvas.ActualHeight;

                    if (VUMEterBrush != null)
                    {
                        double peakPos = pos;// 1.0 - peakLineR.Y1 / volCanvas.ActualHeight;
                        Color c = GetRelativeColor(VUMEterBrush.GradientStops, peakPos);
                        peakLineR.Stroke = new SolidColorBrush(c);
                    }

                    if (peakLineR.Y1 >= volCanvas.ActualHeight)
                        peakLineR.Visibility = Visibility.Collapsed;
                    else
                        peakLineR.Visibility = Visibility.Visible;
                }

                if (peakLineL.Y1 >= volCanvas.ActualHeight)
                {
                    dropPeakL = false;
                }
                if (peakLineR.Y1 >= volCanvas.ActualHeight)
                {
                    dropPeakR = false;
                }
            };
        }

        public MixerControl(ConnectionMixer connectionMixerMachine, int num, ConnectionMixerGUI connectionMixerGUI)
        {
            DataContext = this;

            this.connectionMixerGUI = connectionMixerGUI;

            ResourceDictionary rd = ConnectionMixer.GetBuzzThemeResources();
            if (rd != null) this.Resources.MergedDictionaries.Add(rd);

            this.Resources.MergedDictionaries.Add(connectionMixerGUI.Resources);

            InitializeComponent();

            MixerNumber = num;
            PropertyChanged.Raise(this, "MixerNumber");

            this.cmm = connectionMixerMachine;

            VUMEterBrush = this.TryFindResource("VUMeterBrush") as LinearGradientBrush;
            topBGBrush = this.TryFindResource("TopBGBrush") as SolidColorBrush;
            topBGBrushSelected = this.TryFindResource("TopBGBrushSelected") as SolidColorBrush;
            topBGBrushMouseOver = this.TryFindResource("TopBGBrushMouseOver") as SolidColorBrush;

            SetTimer();
            SetPeakTimer();
            CreateMenus();

            sliderPan.MouseEnter += (s, e) =>
            {
                UpdatePanToolTip();
            };

            sliderPan.MouseLeave += (s, e) =>
            {
                var ttip = sliderPan.ToolTip as ToolTip;
                if (ttip != null)
                    ttip.IsOpen = false;
            };

            sliderPan.ToolTipOpening += (s, e) =>
            {
                e.Handled = true;
            };

            sliderPan.PreviewMouseLeftButtonDown += (s, e) =>
            {
                sliderPan.PreviewMouseMove += SliderPan_MouseMove;
                oldValPosition = e.GetPosition(sliderPan);
                prevMousePosition = oldValPosition;
                sliderDragging = true;
            };

            sliderPan.PreviewMouseLeftButtonUp += (s, e) =>
            {
                sliderPan.MouseMove -= SliderPan_MouseMove;
                sliderDragging = false;

                ToolTip tt = sliderPan.ToolTip as ToolTip;
                if (tt != null)
                    tt.IsOpen = false;
            };

            sliderPan.PreviewMouseWheel += (s, e) =>
            {
                if (!RegSettings.MouseWheelEnabled)
                    return;

                if (selectedConnection != null)
                {
                    double delta = Keyboard.Modifiers == ModifierKeys.Control ? e.Delta * 0.1 : e.Delta;

                    int newPan = (int)Math.Max(0, Math.Min(PanRight, selectedConnection.Pan + delta));
                    selectedConnection.Pan = newPan;

                    cmm.UpdatePan(MixerNumber - 1, selectedConnection);
                    UpdatePanToolTip();

                    e.Handled = true;
                }
            };

            volLevel.MouseEnter += (s, e) =>
            {
                Mouse.OverrideCursor = Cursors.SizeNS;
                UpdateVolToolTip();
            };

            volLevel.MouseLeave += (s, e) =>
            {
                if (!volDragging)
                    Mouse.OverrideCursor = null;

                var ttip = volLevel.ToolTip as ToolTip;
                if (ttip != null)
                    ttip.IsOpen = false;
            };

            volLevel.PreviewMouseLeftButtonDown += (s, e) =>
            {
                Mouse.Capture(volCanvas);
                volCanvas.MouseMove += VolCanvas_MouseMove;
                Mouse.OverrideCursor = Cursors.SizeNS;
                volDragging = true;
                oldValPosition = e.GetPosition(volCanvas);
                prevMousePosition = oldValPosition;
                e.Handled = true;
            };

            volCanvas.PreviewMouseLeftButtonUp += (s, e) =>
            {
                volCanvas.ReleaseMouseCapture();
                volCanvas.MouseMove -= VolCanvas_MouseMove;
                volDragging = false;
                Mouse.OverrideCursor = null;

                ToolTip tt = volLevel.ToolTip as ToolTip;
                if (tt != null)
                    tt.IsOpen = false;

                e.Handled = true;
            };

            dtVolTooltip = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(1000) };
            dtVolTooltip.Tick += (sender, e) =>
            {
                var tt = volLevel.ToolTip as ToolTip;
                if (tt != null)
                {
                    tt.IsOpen = false;
                }
                dtVolTooltip.Stop();
            };

            volCanvas.PreviewMouseWheel += (s, e) =>
            {
                if (!RegSettings.MouseWheelEnabled)
                    return;

                if (SelectedConnection != null)
                {
                    double db = Math.Min(1, (Decibel.FromAmplitude(SelectedConnection.Amp / (double)0x4000) + MinAmp) / (MinAmp + Decibel.FromAmplitude(0xfffe / (double)0x4000)));
                    double delta = Keyboard.Modifiers == ModifierKeys.Control ? e.Delta * 0.00001 : e.Delta * 0.00005;
                    db += delta;
                    db = Math.Max(0, Math.Min(db, 1));

                    int newamp = db == 0 ? 0 : (int)Math.Round(Decibel.ToAmplitude(db * (MinAmp + Decibel.FromAmplitude((double)0xfffe / 0x4000)) - MinAmp) * 0x4000);
                    SelectedConnection.Amp = newamp;
                    cmm.UpdateVol(MixerNumber - 1, selectedConnection);

                    UpdateVolToolTip();
                    dtVolTooltip.Start();

                    e.Handled = true;
                }
            };

            ParamSliders[0] = sliderPar1;
            ParamSliders[1] = sliderPar2;
            ParamSliders[2] = sliderPar3;
            ParamSliders[3] = sliderPar4;

            for (int i = 0; i < ConnectionMixer.NUM_PARAMS; i++)
            {
                ParamSliders[i].Tag = i;
                ParamSliders[i].MouseRightButtonDown += Param_Slider_PreviewMouseRightButtonDown;

                // ToolTips for params...

                ParamSliders[i].ToolTipOpening += Param_Slider_ToolTipOpening;
                ParamSliders[i].PreviewMouseLeftButtonDown += Param_Slider_PreviewMouseLeftButtonDown;
                ParamSliders[i].MouseEnter += Param_Slider_MouseEnter;
                ParamSliders[i].MouseLeave += Param_Slider_MouseLeave;
                ParamSliders[i].PreviewMouseLeftButtonUp += Param_Slider_PreviewMouseLeftButtonUp;

                ParamSliders[i].PreviewMouseWheel += (s, e) =>
                {
                    if (!RegSettings.MouseWheelEnabled)
                        return;

                    Slider slider = (Slider)s;
                    int paramNum = (int)slider.Tag;

                    IParameter par = MachineParameters[paramNum];
                    int track = cmm.SelectedTrack(MixerNumber - 1, paramNum);

                    if (selectedConnection != null && par != null)
                    {
                        int stepSize = Math.Max(1, (par.MaxValue - par.MinValue) / 255);
                        int delta = Keyboard.Modifiers == ModifierKeys.Control ? e.Delta / 120 : (e.Delta / 120) * stepSize;

                        int newPar = (int)Math.Max(par.MinValue, Math.Min(par.MaxValue, par.GetValue(track) + delta));
                        par.SetValue(track, newPar);
                        if (par.Group.Machine.DLL.Info.Version >= 42)
                            par.Group.Machine.SendControlChanges();

                        UpdateParamSliderToolTip(paramNum);
                        e.Handled = true;
                    }
                };
            }

            tbMachineSource.PreviewMouseLeftButtonDown += (sender, e) =>
            {
                if (selectedConnection != null)
                {
                    if (e.ClickCount == 1)
                    {
                        if (!Global.Buzz.MIDIFocusLocked)
                            Global.Buzz.MIDIFocusMachine = selectedConnection.Source;

                        e.Handled = true;
                    }
                    else if (e.ClickCount == 2)
                    {
                        selectedConnection.Source.DoubleClick();
                        e.Handled = true;
                    }
                }
            };

            tbMachineDestination.PreviewMouseLeftButtonDown += (sender, e) =>
            {
                if (selectedConnection != null)
                {
                    if (e.ClickCount == 1)
                    {
                        if (!Global.Buzz.MIDIFocusLocked)
                            Global.Buzz.MIDIFocusMachine = selectedConnection.Destination;

                        e.Handled = true;
                    }
                    else if (e.ClickCount == 2)
                    {
                        selectedConnection.Destination.DoubleClick();
                        e.Handled = true;
                    }
                }
            };

            tbMachineSource.MouseRightButtonDown += (sender, e) =>
            {
                if (selectedConnection != null)
                {
                    if (Global.Buzz.MIDIFocusLocked && Global.Buzz.MIDIFocusMachine == selectedConnection.Source)
                        Global.Buzz.MIDIFocusLocked = false;
                    else
                    {
                        Global.Buzz.MIDIFocusMachine = selectedConnection.Source;
                        Global.Buzz.MIDIFocusLocked = true;
                    }
                }
            };

            tbMachineDestination.MouseRightButtonDown += (sender, e) =>
            {
                if (selectedConnection != null)
                {
                    if (Global.Buzz.MIDIFocusLocked && Global.Buzz.MIDIFocusMachine == selectedConnection.Destination)
                        Global.Buzz.MIDIFocusLocked = false;
                    else
                    {
                        Global.Buzz.MIDIFocusMachine = selectedConnection.Destination;
                        Global.Buzz.MIDIFocusLocked = true;
                    }
                }
            };

            bHeader.MouseLeftButtonDown += (sender, e) =>
            {
                bHeader.Background = topBGBrushSelected;
                Mouse.Capture(bHeader);
                headerGragged = true;
            };

            bHeader.MouseMove += (sender, e) =>
            {
                if (headerGragged)
                {
                    Point pos = e.GetPosition(bHeader);
                    if (pos.X < 20 && MixerNumber - 1 > 0)
                    {
                        cmm.SwitchMixers(MixerNumber - 2, MixerNumber - 1);
                        connectionMixerGUI.SwitchMixers(MixerNumber - 2, MixerNumber - 1);
                    }
                    else if (pos.X > this.ActualWidth + 20 && MixerNumber - 1 < cmm.MachineState.NumMixerConsoles - 1)
                    {
                        cmm.SwitchMixers(MixerNumber - 1, MixerNumber);
                        connectionMixerGUI.SwitchMixers(MixerNumber - 1, MixerNumber);
                    }
                }
                e.Handled = true;
            };

            bHeader.MouseLeftButtonUp += (sender, e) =>
            {
                bHeader.ReleaseMouseCapture();
                Point pos = e.GetPosition(bHeader);

                bHeader.Background = topBGBrush;

                headerGragged = false;

                if (bHeader.IsPointInside(pos))
                    bHeader.Background = topBGBrushMouseOver;
            };

            bHeader.MouseEnter += (sender, e) =>
            {
                if (!headerGragged)
                {
                    bHeader.Background = topBGBrushMouseOver;
                }
            };

            bHeader.MouseLeave += (sender, e) =>
            {
                bHeader.Background = topBGBrush;
            };

            dragTimer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(10) };
            dragTimer.Start();
            dragTimer.Tick += (sender, e) =>
            {
                if (headerGragged)
                {
                    Point pSw = Mouse.GetPosition(connectionMixerGUI.sw);
                    if (pSw.X < 0)
                    {
                        connectionMixerGUI.sw.ScrollToHorizontalOffset(connectionMixerGUI.sw.HorizontalOffset - 10.0);
                    }
                    else if (pSw.X > connectionMixerGUI.sw.ActualWidth)
                    {
                        connectionMixerGUI.sw.ScrollToHorizontalOffset(connectionMixerGUI.sw.HorizontalOffset + 10.0);
                    }
                }
            };

            tbMute.PreviewMouseRightButtonDown += (sender, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    tbMute.ContextMenu = CreateContextMenu(EMIDIControlType.Mute);
                    e.Handled = true;
                }
                else
                {
                    tbMute.ContextMenu = null;
                }
            };

            tbSolo.PreviewMouseRightButtonDown += (sender, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    tbSolo.ContextMenu = CreateContextMenu(EMIDIControlType.Solo);
                    e.Handled = true;
                }
                else
                {
                    tbSolo.ContextMenu = null;
                }
            };

            sliderPan.PreviewMouseRightButtonDown += (sender, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    sliderPan.ContextMenu = CreateContextMenu(EMIDIControlType.Pan);
                    e.Handled = true;
                }
                else
                {
                    sliderPan.Value = amp0;
                    cmm.UpdatePan(MixerNumber - 1, SelectedConnection);
                    cmm.ResetMIDIConnection(MixerNumber - 1, EMIDIControlType.Pan);
                    sliderPan.ContextMenu = null;
                    UpdatePanToolTip();
                }
            };

            sliderPar1.PreviewMouseRightButtonDown += (sender, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    sliderPar1.ContextMenu = CreateContextMenu(EMIDIControlType.P1);
                    e.Handled = true;
                }
                else
                {
                    sliderPar1.ContextMenu = null;
                }
            };

            sliderPar2.PreviewMouseRightButtonDown += (sender, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    sliderPar2.ContextMenu = CreateContextMenu(EMIDIControlType.P2);
                    e.Handled = true;
                }
                else
                {
                    sliderPar2.ContextMenu = null;
                }
            };
            sliderPar3.PreviewMouseRightButtonDown += (sender, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    sliderPar3.ContextMenu = CreateContextMenu(EMIDIControlType.P3);
                    e.Handled = true;
                }
                else
                {
                    sliderPar3.ContextMenu = null;
                }
            };
            sliderPar4.PreviewMouseRightButtonDown += (sender, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    sliderPar4.ContextMenu = CreateContextMenu(EMIDIControlType.P4);
                    e.Handled = true;
                }
                else
                {
                    sliderPar4.ContextMenu = null;
                }
            };
            volLevel.PreviewMouseRightButtonDown += (sender, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    volLevel.ContextMenu = CreateContextMenu(EMIDIControlType.Volume);
                    e.Handled = true;
                }
                else
                {
                    cmm.ResetMIDIConnection(MixerNumber - 1, EMIDIControlType.Volume);
                    SetVolLine(amp0);
                    cmm.UpdateVol(MixerNumber - 1, SelectedConnection);
                    UpdateVolToolTip();
                    volLevel.ContextMenu = null;
                }
            };

            Loaded += MixerControl_Loaded;
            Unloaded += MixerControl_Unloaded;
            cmm.PropertyChanged += Cmm_PropertyChanged;
        }

        private ContextMenu CreateContextMenu(EMIDIControlType type)
        {
            ContextMenu menu = new ContextMenu();
            MenuItem menuItem = new MenuItem();
            menuItem.Header = "Bind to MIDI...";
            menuItem.Tag = type;
            menuItem.Click += Menu_Click_BindMIDI;
            menu.Items.Add(menuItem);
            menuItem = new MenuItem();
            menuItem.Header = "Unbind MIDI";
            menuItem.Tag = type;
            menuItem.Click += Menu_Click_UnbindMIDI;
            menuItem.IsEnabled = cmm.IsMIDIBind(MixerNumber - 1, type);
            menu.Items.Add(menuItem);

            return menu;
        }

        private void Menu_Click_UnbindMIDI(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = (MenuItem)sender;
            cmm.BindMidi(MixerNumber - 1, (EMIDIControlType)menuItem.Tag, -1, 0);
        }

        private void Menu_Click_BindMIDI(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = (MenuItem)sender;
            EMIDIControlType type = (EMIDIControlType)menuItem.Tag;

            //var mcd = cmm.MachineState.Mcd[MixerNumber - 1];
            var MIDIData = cmm.GetMIDIData(MixerNumber - 1, type);
            MIDIWindow MIDIWnd = new MIDIWindow(MIDIData.Item1 + 1, MIDIData.Item2);
            MIDIWnd.Title = "Bind MIDI to " + MIDIData.Item3 + ". Mixer Control: " + this.MixerNumber;
            MIDIWnd.ShowDialog();

            if (MIDIWnd.DialogResult == true)
            {
                cmm.BindMidi(MixerNumber - 1, type, MIDIWnd.ValueChannel - 1, MIDIWnd.ValueCC);
            }
        }

        public void MixerXontrolsChanged()
        {
            PropertyChanged.Raise(this, "MixerNumber");
        }

        private void Cmm_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Name" || e.PropertyName == "MachineRemoved" || e.PropertyName == "MachineAdded" || e.PropertyName == "ConnectionChanged")
            {
                for (int i = 0; i < ConnectionMixer.NUM_PARAMS; i++)
                    ClearParam(i, false);

                LoadValues(MixerNumber - 1);
                UpdateConnValue();

                peakLineL.Visibility = Visibility.Collapsed;
                peakLineR.Visibility = Visibility.Collapsed;
            }
            else if (e.PropertyName == "Vol")
            {
                if (selectedConnection != null)
                {
                    UpdateConnectionVolume();
                    textBlockVol.Text = UpdateVolText();
                }
                else
                {
                    SetVolLine(amp0);
                }
            }
            else if (e.PropertyName == "Pan")
            {
                if (selectedConnection != null)
                {
                    // Pan value already updated in connection, just notify ui
                    PropertyChanged.Raise(this, "SelectedConnection");
                    textBlockPan.Text = UpdatePanText();
                }
            }
        }

        private void LoadValues(int v)
        {
            var mcd = cmm.MachineState.Mcd[v];
            IMachineConnection connectionFound = null;

            foreach (var mac in Global.Buzz.Song.Machines)
            {
                foreach (var conn in mac.Outputs)
                {
                    if (conn.Source.Name == mcd.source && conn.Destination.Name == mcd.destination)
                    {
                        connectionFound = conn;
                        break;
                    }
                }
            }

            SelectedConnection = connectionFound;

            // Start listening events
            cmm.ConnectionSelected(v, SelectedConnection);

            for (int i = 0; i < ConnectionMixer.NUM_PARAMS; i++)
            {
                var mpd = mcd.paramTable[i];

                foreach (var mac in Global.Buzz.Song.Machines)
                {
                    if (mac.Name == mpd.machine)
                    {
                        foreach (var group in mac.ParameterGroups)
                            foreach (var par in group.Parameters)
                            {
                                if (par.Name == mpd.param)
                                    AssignMachineParam(par, i, mpd.track);
                            }
                    }
                }
            }
        }

        private void Param_Slider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Slider slider = (Slider)sender;
            slider.MouseMove -= Param_Slider_PreviewMouseMove;
            sliderDragging = false;

            ToolTip tt = slider.ToolTip as ToolTip;
            if (tt != null)
                tt.IsOpen = false;
        }

        private void Param_Slider_MouseLeave(object sender, MouseEventArgs e)
        {
            Slider slider = (Slider)sender;
            var ttip = slider.ToolTip as ToolTip;
            if (ttip != null)
                ttip.IsOpen = false;
        }

        private void Param_Slider_MouseEnter(object sender, MouseEventArgs e)
        {
            Slider s = (Slider)sender;
            int num = (int)s.Tag;
            UpdateParamSliderToolTip(num);
        }

        private void Param_Slider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Slider s = (Slider)sender;
            s.PreviewMouseMove += Param_Slider_PreviewMouseMove;
            oldValPosition = e.GetPosition(sliderPan);
            prevMousePosition = oldValPosition;
            sliderDragging = true;
        }

        private void Param_Slider_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            Slider slider = (Slider)sender;

            int num = (int)slider.Tag;

            IParameter par = MachineParameters[num];
            int track = cmm.SelectedTrack(MixerNumber - 1, num);

            if (sliderDragging && selectedConnection != null && par != null)
            {
                Point newMousePosition = e.GetPosition(volCanvas);

                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    double direction = (newMousePosition.X - prevMousePosition.X) > 0 ? 1 : -1;
                    double x_old = direction; //* (par.MaxValue - par.MinValue);
                    oldValPosition = newMousePosition;
                    int newPar = (int)Math.Max(0, Math.Min(PanRight, par.GetValue(track) + x_old));
                    par.SetValue(track, newPar);
                    if (par.Group.Machine.DLL.Info.Version >= 42)
                        par.Group.Machine.SendControlChanges();
                    e.Handled = true;
                }

                prevMousePosition = newMousePosition;

                UpdateParamSliderToolTip(num);
            }
        }

        private void Param_Slider_ToolTipOpening(object sender, ToolTipEventArgs e)
        {
            e.Handled = true;
        }

        private void MixerControl_Unloaded(object sender, RoutedEventArgs e)
        {
            cmm.PropertyChanged -= Cmm_PropertyChanged;

            for (int i = 0; i < ConnectionMixer.NUM_PARAMS; i++)
            {
                ParamSliders[i].Tag = i;
                ParamSliders[i].PreviewMouseRightButtonDown -= Param_Slider_PreviewMouseRightButtonDown;

                IParameter par = MachineParameters[i];
                if (par != null)
                {
                    UnassignMachineParam(par, i, cmm.SelectedTrack(MixerNumber - 1, i));

                    MachineParameters[i] = null;
                }

                // ToolTips for params...
                ParamSliders[i].ToolTipOpening -= Param_Slider_ToolTipOpening;
                ParamSliders[i].PreviewMouseLeftButtonDown -= Param_Slider_PreviewMouseLeftButtonDown;
                ParamSliders[i].MouseEnter -= Param_Slider_MouseEnter;
                ParamSliders[i].MouseLeave -= Param_Slider_MouseLeave;
                ParamSliders[i].PreviewMouseLeftButtonUp -= Param_Slider_PreviewMouseLeftButtonUp;
            }
        }

        private void CreateMenus()
        {
            // Main Menu
            MenuItem miConnect = new MenuItem() { Header = " 🡇 " };
            MenuItem mi = new MenuItem() { };
            mi.Header = "Reset Connection";
            mi.Tag = new ConnectionInfo();
            mi.Click += Mi_Click_Set_Connection;
            miConnect.Items.Add(mi);
            mi = new MenuItem() { };
            mi.Header = "Reset All";
            mi.Tag = new ConnectionInfo();
            mi.Click += Mi_Click_Reset_All;
            miConnect.Items.Add(mi);
            miConnect.Items.Add(new Separator());

            mi = new MenuItem() { };
            mi.Header = "Clear MIDI bindings...";
            mi.Click += Mi_Click_Clear_MIDI_Bindings;
            miConnect.Items.Add(mi);
            miConnect.Items.Add(new Separator());

            MenuItem miSelectConnection = new MenuItem();
            miSelectConnection.Header = "Select Connection...";
            miSelectConnection.Icon = "🡆 ";
            miConnect.Items.Add(miSelectConnection);

            miConnect.SubmenuOpened += (s, e) =>
            {
                if (e.OriginalSource == miConnect)
                {
                    bool connectedMachine = false;
                    foreach (var mac in Global.Buzz.Song.Machines)
                    {
                        if (mac.Outputs.Count > 0)
                        {
                            connectedMachine = true;
                            break;
                        }
                    }

                    if (connectedMachine)
                    {
                        object dummySub = new object();
                        miSelectConnection.Items.Add(dummySub);
                    }
                    else
                    {
                        miSelectConnection.Items.Clear();
                    }
                }
            };

            miSelectConnection.SubmenuOpened += delegate
            {
                if (miSelectConnection.Items[0].GetType() == typeof(object))
                {
                    BuildConnectionMenu(miSelectConnection);
                }
            };

            miSelectConnection.SubmenuClosed += (s, e) =>
            {
                if (e.OriginalSource == miSelectConnection)
                {
                    object dummySub = new object();
                    miSelectConnection.Items.Clear();
                    miSelectConnection.Items.Add(dummySub);
                }
            };

            mConnection.Items.Add(miConnect);

            // Create param menus
            CreateParamMenu(0, menuPar1);
            CreateParamMenu(1, menuPar2);
            CreateParamMenu(2, menuPar3);
            CreateParamMenu(3, menuPar4);

            ParamMenus[0] = menuPar1;
            ParamMenus[1] = menuPar2;
            ParamMenus[2] = menuPar3;
            ParamMenus[3] = menuPar4;
        }

        private void Mi_Click_Clear_MIDI_Bindings(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("Are you sure?", "Clear MIDI Bindings", MessageBoxButton.OKCancel);
            if (result == MessageBoxResult.OK)
            {
                cmm.UnbindMidi(MixerNumber - 1);
            }
        }

        private void Param_Slider_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            Slider s = (Slider)sender;
            int paramNum = (int)s.Tag;
            IParameter par = MachineParameters[paramNum];
            if (par != null)
            {
                s.Value = MachineParameters[paramNum].DefValue;
                cmm.ResetMIDIParameterConnection(MixerNumber - 1, paramNum);
            }
        }


        private void CreateParamMenu(int v, Menu menu)
        {
            MenuItem miPar = new MenuItem();
            miPar.Header = "P" + (v + 1);
            menu.Items.Add(miPar);
            menu.ToolTip = "Machine Parameter " + (v + 1);

            MenuItem mi = new MenuItem();
            mi.Header = "Reset";
            mi.Click += Mi_Click_Reset_Parameter;
            mi.Tag = v;
            miPar.Items.Add(mi);

            MenuItem miMachine = new MenuItem();
            miMachine.Header = "Open";
            miMachine.Click += MiMachine_Click; ;
            miMachine.Tag = v;
            miPar.Items.Add(miMachine);

            miPar.Items.Add(new Separator());

            MenuItem miSelectPar = new MenuItem();
            miSelectPar.Header = "Select Param";
            miSelectPar.Icon = " ⮀";
            miPar.Items.Add(miSelectPar);

            object dummySub = new object();
            miSelectPar.Items.Add(dummySub);

            int parNum = v;

            miSelectPar.SubmenuOpened += delegate
            {
                if (miSelectPar.Items[0].GetType() == typeof(object))
                {
                    BuildParamMenu(miSelectPar, parNum);
                }
            };

            miSelectPar.SubmenuClosed += (s, e) =>
            {
                if (e.OriginalSource == miSelectPar)
                {
                    miSelectPar.Items.Clear();
                    miSelectPar.Items.Add(dummySub);
                }
            };

            MenuItem miFlip = new MenuItem();
            miFlip.Header = "Flip";
            miFlip.IsCheckable = true;
            miFlip.IsChecked = cmm.MachineState.Mcd[MixerNumber - 1].paramTable[parNum].flip;
            miFlip.Checked += (s, e) =>
            {
                cmm.MachineState.Mcd[MixerNumber - 1].paramTable[parNum].flip = true;
            };
            miFlip.Unchecked += (s, e) =>
            {
                cmm.MachineState.Mcd[MixerNumber - 1].paramTable[parNum].flip = false;
            };


            MenuItem miTrack = new MenuItem();
            miTrack.Header = "Track";
            miTrack.Icon = " ⮇";
            miTrack.Tag = v;
            miPar.Items.Add(miTrack);

            miPar.Items.Add(miFlip);

            miPar.SubmenuOpened += (s, e) =>
            {
                if (e.OriginalSource == miPar)
                {
                    IParameter par = MachineParameters[parNum];
                    miTrack.IsEnabled = (par != null && par.Group.TrackCount > 1);

                    miMachine.IsEnabled = par != null;
                }
            };
            miTrack.Items.Add(dummySub);

            miTrack.SubmenuOpened += delegate
            {
                if (miTrack.Items[0].GetType() == typeof(object))
                {
                    miTrack.Items.Clear();
                    IParameter par = MachineParameters[parNum];
                    if (par != null)
                    {
                        int selectedTrack = cmm.SelectedTrack(MixerNumber - 1, parNum);

                        for (int i = 0; i < par.Group.TrackCount; i++)
                        {
                            MenuItem mit = new MenuItem();
                            mit.Header = "" + i;
                            mit.Icon = selectedTrack == i ? "✓" : "";
                            mit.Tag = new Tuple<int, int>(parNum, i);
                            mit.Click += Mi_Click_Track;
                            miTrack.Items.Add(mit);
                        }
                    }
                }
            };

            miTrack.SubmenuClosed += (s, e) =>
            {
                if (e.OriginalSource == miTrack)
                {
                    miTrack.Items.Clear();
                    miTrack.Items.Add(dummySub);
                }
            };
        }

        private void MiMachine_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            int parNum = (int)menuItem.Tag;

            string macName = cmm.MachineState.Mcd[MixerNumber - 1].paramTable[parNum].machine;
            IMachine machine = Global.Buzz.Song.Machines.FirstOrDefault(x => x.Name == macName);

            if (machine != null)
            {
                machine.DoubleClick();
            }
        }

        private void Mi_Click_Track(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            Tuple<int, int> targetParTrack = (Tuple<int, int>)mi.Tag;
            int num = targetParTrack.Item1;
            int track = targetParTrack.Item2;
            int oldTrack = cmm.SelectedTrack(MixerNumber - 1, num);

            IParameter par = MachineParameters[num];

            if (par != null)
            {
                // Unsubscribe previous
                UnassignMachineParam(par, num, oldTrack);
                cmm.SetSelectedTrack(MixerNumber - 1, num, track);
                AssignMachineParam(par, num, track);
            }
        }

        private void BuildParamMenu(MenuItem miSelectPar, int parNum)
        {
            miSelectPar.Items.Clear();
            CreateAllMachineMenu(miSelectPar, parNum);
        }

        private void CreateAllMachineMenu(MenuItem miMac, int parNum)
        {
            foreach (IMachine mac in Global.Buzz.Song.Machines)
            {
                MenuItem mi = new MenuItem();
                mi.Header = mac.Name;
                mi.Tag = mac;
                mi.Icon = "";
                object dummySub = new object();
                mi.Items.Add(dummySub);

                mi.SubmenuOpened += delegate
                {
                    if (mi.Items[0].GetType() == typeof(object))
                    {
                        mi.Items.RemoveAt(0);
                        CreateParamsGroupMenu((IMachine)mi.Tag, mi, parNum);
                    }
                };
                miMac.Items.Add(mi);
            }
        }

        private void CreateParamsGroupMenu(IMachine mac, MenuItem miParent, int parNum)
        {
            int i = 0;
            foreach (IParameterGroup pg in mac.ParameterGroups)
            {
                MenuItem mi = new MenuItem();
                mi.Header = "Group " + i;
                mi.Icon = "";
                mi.Tag = new Tuple<int, IParameterGroup>(i, pg);
                i++;

                if (pg.Parameters.Count > 0)
                {
                    object dummySub = new object();
                    mi.Items.Add(dummySub);

                    mi.SubmenuOpened += delegate
                    {
                        if (mi.Items[0].GetType() == typeof(object))
                        {
                            mi.Items.Clear();
                            IParameterGroup pg1 = ((Tuple<int, IParameterGroup>)mi.Tag).Item2;
                            foreach (IParameter par in pg1.Parameters)
                            {
                                MenuItem miPar = new MenuItem();
                                miPar.Header = par.Name;
                                miPar.Icon = "";
                                miPar.Tag = new Tuple<IParameter, int>(par, parNum);
                                miPar.Click += Mi_Click_Assign_Param;
                                mi.Items.Add(miPar);
                            }
                        }
                    };

                    miParent.Items.Add(mi);
                }
            }
        }

        private void Mi_Click_Assign_Param(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            Tuple<IParameter, int> p = menuItem.Tag as Tuple<IParameter, int>;
            IParameter par = p.Item1;
            int num = p.Item2;

            IParameter prePar = MachineParameters[num];
            UnassignMachineParam(prePar, num, cmm.SelectedTrack(MixerNumber - 1, num));
            cmm.SetSelectedTrack(MixerNumber - 1, num, 0);
            cmm.SetParameter(MixerNumber - 1, par, num);
            AssignMachineParam(par, num, 0);
        }

        private void UnassignMachineParam(IParameter par, int num, int track)
        {
            if (par != null)
            {
                try
                {
                    // Exception here randommly with VSTs when track is changed
                    par.UnsubscribeEvents(track, ParameterValueChanged, null);
                }
                catch (Exception ex)
                {
                }
            }
            MachineParameters[num] = null;
        }

        private void AssignMachineParam(IParameter par, int num, int track)
        {
            MachineParameters[num] = par;
            Slider s = ParamSliders[num];
            s.ValueChanged -= Slider_ValueChanged;
            s.Tag = num;
            s.Maximum = par.MaxValue;
            s.Minimum = par.MinValue;
            s.Value = par.GetValue(track);
            s.ValueChanged += Slider_ValueChanged;
            ToolTip tt = new ToolTip();
            tt.Placement = System.Windows.Controls.Primitives.PlacementMode.Relative;
            s.ToolTip = tt;
            ParamMenus[num].ToolTip = par.Group.Machine.Name + " | " + par.Name + " | Track: " + track;

            try
            {
                // Exception here randommly with VSTs when track is changed
                par.SubscribeEvents(track, ParameterValueChanged, null);
            }
            catch (Exception ex)
            {
            }
        }

        int lastValue = -1;
        private bool headerGragged;
        private ConnectionMixerGUI connectionMixerGUI;

        private void ParameterValueChanged(IParameter par, int t)
        {
            int newvalue = par.GetValue(t);
            if (newvalue != lastValue)
            {
                for (int i = 0; i < ConnectionMixer.NUM_PARAMS; i++)
                {
                    int track = cmm.SelectedTrack(MixerNumber - 1, i);
                    if (MachineParameters[i] == par && track == t)
                    {
                        ParamSliders[i].ValueChanged -= Slider_ValueChanged;
                        ParamSliders[i].Value = FlipPar(par, par.GetValue(track), i);
                        ParamSliders[i].ValueChanged += Slider_ValueChanged;
                    }
                }
            }
        }

        int FlipPar(IParameter par, int value, int cmmParIndex)
        {
            if (cmm.MachineState.Mcd[MixerNumber - 1].paramTable[cmmParIndex].flip)
            {
                value = par.MaxValue - (value - par.MinValue);
            }
            return value;
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var s = sender as Slider;
            int num = (int)s.Tag;
            IParameter par = MachineParameters[num];
            int track = cmm.SelectedTrack(MixerNumber - 1, num);

            int newVal = (int)s.Value;

            newVal = FlipPar(par, newVal, num);

            par.SetValue(track, newVal);
            if (par.Group.Machine.DLL.Info.Version >= 42)
                par.Group.Machine.SendControlChanges();
        }

        private void Mi_Click_Reset_Parameter(object sender, RoutedEventArgs e)
        {
            var mi = sender as MenuItem;
            int num = (int)mi.Tag;
            ClearParam(num, true);
        }

        private void ClearParam(int num, bool zeroData)
        {
            ParamMenus[num].ToolTip = "Machine Parameter " + (num + 1);
            Slider s = ParamSliders[num];
            s.ValueChanged -= Slider_ValueChanged;
            s.Minimum = 0;
            s.Maximum = 10;
            s.Value = 0;
            if (s.ToolTip != null)
                (s.ToolTip as ToolTip).IsOpen = false;
            s.ToolTip = null;
            IParameter par = MachineParameters[num];
            MachineParameters[num] = null;
            if (par != null)
            {
                try
                {
                    UnassignMachineParam(par, num, cmm.SelectedTrack(MixerNumber - 1, num));
                }
                catch (Exception ex)
                {
                }
            }

            if (zeroData)
            {
                cmm.MachineState.Mcd[MixerNumber - 1].paramTable[num] = new ConnectionMixer.MachineParamData();
            }
        }

        private void SliderPan_MouseMove(object sender, MouseEventArgs e)
        {
            if (sliderDragging && selectedConnection != null)
            {
                Point pos = Win32Mouse.GetScreenPosition();
                pos.X /= WPFExtensions.PixelsPerDip;
                pos.Y /= WPFExtensions.PixelsPerDip;

                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    double direction = (pos.X - prevMousePosition.X) > 0 ? 1 : -1;
                    double x_old = direction * 10;
                    oldValPosition = pos;
                    int newPan = (int)Math.Max(0, Math.Min(PanRight, selectedConnection.Pan + x_old));
                    selectedConnection.Pan = newPan;
                    e.Handled = true;
                }

                prevMousePosition = pos;

                cmm.UpdatePan(MixerNumber - 1, selectedConnection);
                UpdatePanToolTip();
            }
        }

        private void MixerControl_Loaded(object sender, RoutedEventArgs e)
        {
            volLevel.X2 = volCanvas.ActualWidth;
            volLevel.ToolTipOpening += VolLevel_ToolTipOpening;

            peakLineL.Y1 = peakLineL.Y2 = volCanvas.ActualHeight;
            peakLineR.Y1 = peakLineR.Y2 = volCanvas.ActualHeight;

            sliderPan.Value = PanCenter;

            SetVolLine(amp0);
            DrawDbText();

            LoadValues(MixerNumber - 1);
            UpdateConnValue();
        }

        private void SetVolLine(int amp)
        {
            double ampLine = Math.Min(1, (Decibel.FromAmplitude(amp / (double)0x4000) + MinAmp) / (MinAmp + Decibel.FromAmplitude(0xfffe / (double)0x4000)));
            volLevel.Y1 = volLevel.Y2 = volCanvas.ActualHeight - ampLine * volCanvas.ActualHeight;
            if (SelectedConnection != null)
            {
                SelectedConnection.Amp = amp;
            }
        }

        private void VolLevel_ToolTipOpening(object sender, ToolTipEventArgs e)
        {
            e.Handled = true;
        }

        public void UpdateVolToolTip()
        {
            if (SelectedConnection != null && volLevel.ToolTip != null)
            {
                var tt = volLevel.ToolTip as ToolTip;
                Point pos = Win32Mouse.GetScreenPosition();
                pos.X /= WPFExtensions.PixelsPerDip;
                pos.Y /= WPFExtensions.PixelsPerDip;

                tt.Content = UpdateVolText();

                tt.HorizontalOffset = pos.X + 20;
                tt.VerticalOffset = pos.Y - 10;
                if (tt != null && (string)tt.Content != "")
                    tt.IsOpen = true;
                else
                    tt.IsOpen = false;
            }
        }

        public string UpdateVolText()
        {
            int v = SelectedConnection.Amp;
            string txt = v > 0 ? string.Format("{0:F1}dB", Decibel.FromAmplitude(v * (1.0 / 0x4000))) : "-inf.dB";
            return txt;
        }

        public void UpdateParamSliderToolTip(int num)
        {
            Slider slider = ParamSliders[num];
            IParameter par = MachineParameters[num];
            int track = cmm.SelectedTrack(MixerNumber - 1, num);

            if (par != null && slider.ToolTip != null)
            {
                var tt = slider.ToolTip as ToolTip;
                Point pos = Win32Mouse.GetScreenPosition();
                pos.X /= WPFExtensions.PixelsPerDip;
                pos.Y /= WPFExtensions.PixelsPerDip;

                tt.Content = par.DescribeValue(par.GetValue(track));

                tt.HorizontalOffset = pos.X + 30;
                tt.VerticalOffset = pos.Y - 10;
                if ((string)tt.Content != "")
                    tt.IsOpen = true;
                else
                    tt.IsOpen = false;
            }
        }

        public void UpdatePanToolTip()
        {
            if (SelectedConnection != null && sliderPan.ToolTip != null)
            {
                var tt = sliderPan.ToolTip as ToolTip;
                Point pos = Win32Mouse.GetScreenPosition();
                pos.X /= WPFExtensions.PixelsPerDip;
                pos.Y /= WPFExtensions.PixelsPerDip;

                tt.Content = UpdatePanText();

                tt.HorizontalOffset = pos.X + 30;
                tt.VerticalOffset = pos.Y - 10;
                if ((string)tt.Content != "")
                    tt.IsOpen = true;
                else
                    tt.IsOpen = false;
            }
        }

        string UpdatePanText()
        {
            string ret = "";
            int v = SelectedConnection.Pan;
            if (v == 0) ret = "L";
            else if (v == PanCenter) ret = "C";
            else if (v == PanRight) ret = "R";
            else if (v < PanCenter) ret = string.Format("{0:F0}L", (PanCenter - v) * (100.0 / PanCenter));
            else if (v > PanCenter) ret = string.Format("{0:F0}R", (v - PanCenter) * (100.0 / PanCenter));

            return ret;
        }

        private void VolCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (volDragging)
            {
                Point newMousePosition = e.GetPosition(volCanvas);
                double y = 0;
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    double direction = (newMousePosition.Y - prevMousePosition.Y) > 0 ? 1 : -1;
                    double y_old = oldValPosition.Y + direction * 0.5;
                    y = Math.Min(Math.Max(0, y_old), volCanvas.ActualHeight);
                    oldValPosition = newMousePosition;
                    oldValPosition.Y = y_old;
                }
                else
                {
                    y = Math.Min(Math.Max(0, newMousePosition.Y), volCanvas.ActualHeight);
                    oldValPosition = newMousePosition;
                }

                prevMousePosition = newMousePosition;

                volLevel.Y1 = y;
                volLevel.Y2 = y;

                if (y == volCanvas.ActualHeight)
                {
                    maxSampleL = maxSampleR = 0;
                }

                if (SelectedConnection != null)
                {
                    double v = (volCanvas.ActualHeight - y) / volCanvas.ActualHeight;

                    int newamp = v == 0 ? 0 : (int)Math.Round(Decibel.ToAmplitude(v * (MinAmp + Decibel.FromAmplitude((double)0xfffe / 0x4000)) - MinAmp) * 0x4000);
                    SelectedConnection.Amp = newamp;
                    cmm.UpdateVol(MixerNumber - 1, selectedConnection);
                }
                UpdateVolToolTip();
            }
        }

        public class ConnectionInfo
        {
            private IMachineConnection connection;
            private string connectionName;
            public IMachineConnection Connection
            {
                get { return connection; }
                set
                {
                    connection = value;
                }
            }

            public void UpdateConnectionName(bool reverse)
            {
                connectionName = "";

                if (connection != null)
                {
                    if (!reverse)
                    {
                        connectionName = connection.Source.Name + " 🡆 " + connection.Destination.Name;
                    }
                    else
                    {
                        connectionName = connection.Destination.Name + " 🡄 " + connection.Source.Name;
                    }

                }
            }

            public override string ToString()
            {
                return connectionName;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void BuildConnectionMenu(MenuItem miSelectConnection)
        {
            List<ConnectionInfo> ciList = new List<ConnectionInfo>();

            foreach (var mac in Global.Buzz.Song.Machines)
            {
                foreach (var conn in mac.Outputs)
                {
                    ConnectionInfo ci = new ConnectionInfo();
                    ci.Connection = conn;
                    ci.UpdateConnectionName(Keyboard.Modifiers == ModifierKeys.Control);
                    ciList.Add(ci);
                }
            }

            ciList.Sort((x, y) => string.Compare(x.ToString(), y.ToString()));

            miSelectConnection.Items.Clear();

            foreach (var ci in ciList)
            {
                MenuItem mi = new MenuItem();
                mi.Header = ci.ToString();
                mi.Tag = ci;
                mi.Click += Mi_Click_Set_Connection;
                miSelectConnection.Items.Add(mi);
            }
        }

        private void Mi_Click_Set_Connection(object sender, RoutedEventArgs e)
        {
            var mi = (MenuItem)sender;
            var ci = (ConnectionInfo)mi.Tag;

            this.SelectedConnection = ci.Connection; // null if reset

            UpdateConnValue();
            cmm.ConnectionSelected(MixerNumber - 1, SelectedConnection);
        }

        private void UpdateConnValue()
        {
            // Notify UI
            PropertyChanged.Raise(this, "SelectedConnection.Source.Name");
            PropertyChanged.Raise(this, "SelectedConnection.Destination.Name");
            PropertyChanged.Raise(this, "SelectedConnection");
            if (SelectedConnection == null)
            {
                sliderPan.Value = PanCenter;
                SetVolLine(amp0);
                VUMeterLevelL = VUMeterLevelR = VUMeterRMSLevelL = VUMeterRMSLevelR = 0;
                PropertyChanged.Raise(this, "VUMeterLevelL");
                PropertyChanged.Raise(this, "VUMeterRMSLevelL");
                PropertyChanged.Raise(this, "VUMeterLevelR");
                PropertyChanged.Raise(this, "VUMeterRMSLevelR");
            }
        }

        private void Mi_Click_Reset_All(object sender, RoutedEventArgs e)
        {
            Mi_Click_Set_Connection(sender, e);
            ResetPan();
            for (int i = 0; i < ConnectionMixer.NUM_PARAMS; i++)
                ClearParam(i, true);
        }

        private void ResetPan()
        {
            if (SelectedConnection != null)
            {
                SelectedConnection.Pan = PanCenter;
                PropertyChanged.Raise(this, "SelectedConnection");
            }
        }

        public void SetConnection(IMachineConnection mc)
        {
            SelectedConnection = mc;
        }

        private void DrawDbText()
        {
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

                string dbTxt = newamp > 0 ? string.Format("{0:F0}dB", Decibel.FromAmplitude(newamp * (1.0 / (double)0x4000))) : "-inf.dB";

                if (y + 18 + 2 >= volTextCanvas.ActualHeight)
                    dbTxt = "-inf.dB";

                tb.Text = dbTxt;
                tb.FontSize = 11;
                tb.Foreground = Brushes.LightGray;
                volTextCanvas.Children.Add((tb));

                y += 18;
            }
        }

        public static Color GetRelativeColor(GradientStopCollection gsc, double offset)
        {
            var point = gsc.SingleOrDefault(f => f.Offset == offset);
            if (point != null) return point.Color;

            GradientStop before = gsc.Where(w => w.Offset == gsc.Min(m => m.Offset)).First();
            GradientStop after = gsc.Where(w => w.Offset == gsc.Max(m => m.Offset)).First();

            foreach (var gs in gsc)
            {
                if (gs.Offset < offset && gs.Offset > before.Offset)
                {
                    before = gs;
                }
                if (gs.Offset > offset && gs.Offset < after.Offset)
                {
                    after = gs;
                }
            }

            var color = new Color();

            color.ScA = (float)((offset - before.Offset) * (after.Color.ScA - before.Color.ScA) / (after.Offset - before.Offset) + before.Color.ScA);
            color.ScR = (float)((offset - before.Offset) * (after.Color.ScR - before.Color.ScR) / (after.Offset - before.Offset) + before.Color.ScR);
            color.ScG = (float)((offset - before.Offset) * (after.Color.ScG - before.Color.ScG) / (after.Offset - before.Offset) + before.Color.ScG);
            color.ScB = (float)((offset - before.Offset) * (after.Color.ScB - before.Color.ScB) / (after.Offset - before.Offset) + before.Color.ScB);

            return color;
        }
    }
}
