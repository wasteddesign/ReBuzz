using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using ModernSequenceEditor.Interfaces;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace EnvelopeBlock
{
    /// <summary>
    /// WaveCanvas handles the actual wave drawing.
    /// </summary>
    public partial class EnvelopeCanvas : Canvas, INotifyPropertyChanged
    {
        EnvelopeBlockMachine EnvelopeBlockMachine { get; set; }

        private int envelopPatternIndex;
        private Brush waveBrush;

        private double patternLengthInSeconds = 5.0;
        //private Canvas backgroundWaveCanvas;

        //private List<Task> taskList = new List<Task>();

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

        public int EnvelopePatternIndex { get => envelopPatternIndex; set => envelopPatternIndex = value; }

        public bool DrawBackground { get; set; }
        public double TickHeight { get; internal set; }
        public bool ReDrawAfterStopped { get; private set; }
        public int Time { get; internal set; }
        public SequencerLayout LayoutMode { get; internal set; }

        private MenuItem menuItemSnapToTick;
        private MenuItem menuItemSnapToBeat;
        private MenuItem menuItemDisable;
        private MenuItem menuItemSnapTo;

#pragma warning disable CS0067 // The event 'EnvelopeCanvas.PropertyChanged' is never used
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore CS0067 // The event 'EnvelopeCanvas.PropertyChanged' is never used

        private MenuItem menuItemCopyAssigments;
        private MenuItem menuItemCopyAll;
        private MenuItem menuItemShowAll;
        private MenuItem menuItemHideAll;
        private Separator miSeparator;

        public EnvelopeCanvas(EnvelopeBlockMachine envelopeBlockMachine, int envelopePatternIndex, SequencerLayout layoutMode, double width, double height, double tickHeight, int time, double patternLength)
        {
            InitializeComponent();
            Width = width;
            Height = height;
            LayoutMode = layoutMode;

            TickHeight = tickHeight;
            this.Time = time;

            this.patternLengthInSeconds = patternLength;

            DrawBackground = true;

            this.EnvelopeBlockMachine = envelopeBlockMachine;
            this.EnvelopePatternIndex = envelopePatternIndex;

            //backgroundWaveCanvas = new Canvas() { SnapsToDevicePixels = true };
            this.SnapsToDevicePixels = true;

            this.Background = Utils.CreateLGBackgroundBrush(true);
            this.Background.Opacity = 0;

            this.Loaded += WaveCanvas_Loaded;
            this.Unloaded += WaveCanvas_Unloaded;

            ContextMenu cmCanvas = new ContextMenu() { Margin = new Thickness(4, 4, 4, 4) };
            this.ContextMenu = cmCanvas;

            CreateMenuItems();
            UpdateMenus();

            envelopes = new Envelopes(envelopeBlockMachine, this) { SnapsToDevicePixels = true };

            string patternName = envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Pattern;
            PatternLengthInSeconds = envelopeBlockMachine.GetPatternLenghtInSeconds(patternName);
        }

        private void Buzz_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if ((e.PropertyName == "BPM" || e.PropertyName == "TPB"))
            {
                if (Global.Buzz.Playing || Global.Buzz.Recording)
                    this.ReDrawAfterStopped = true;
                else
                {
                    ReDrawAfterStopped = false;
                    UpdateGraph();
                }
            }
            else if ((e.PropertyName == "Playing") && ReDrawAfterStopped)
            {
                if (!Global.Buzz.Playing && !Global.Buzz.Recording)
                {
                    ReDrawAfterStopped = false;
                    UpdateGraph();
                }
            }
        }

        private void Song_MachineRemoved(IMachine obj)
        {

        }

        private void Song_MachineAdded(IMachine obj)
        {
            UpdateMenus();
        }

        private void Machine_PatternRemoved(IPattern obj)
        {
            // This removed
            if (EnvelopeBlockMachine.MachineState.Patterns[envelopPatternIndex] != null)
                UpdateMenus();
        }

        private void Machine_PatternAdded(IPattern obj)
        {
            UpdateMenus();
        }

        private void CreateMenuItems()
        {
            menuItemShowAll = new MenuItem();
            menuItemShowAll.Header = "Show All";
            menuItemShowAll.Click += MenuItemShowAll_Click;

            menuItemHideAll = new MenuItem();
            menuItemHideAll.Header = "Hide All";
            menuItemHideAll.Click += MenuItemHideAll_Click;

            menuItemSnapTo = new MenuItem();
            menuItemSnapTo.Header = "Snap To...";

            menuItemSnapToTick = new MenuItem();
            menuItemSnapToTick.Header = "Tick";
            menuItemSnapToTick.IsCheckable = true;

            menuItemSnapToBeat = new MenuItem();
            menuItemSnapToBeat.Header = "Beat";
            menuItemSnapToBeat.IsCheckable = true;

            menuItemSnapTo.Items.Add(menuItemSnapToTick);
            menuItemSnapTo.Items.Add(menuItemSnapToBeat);

            menuItemCopyAssigments = new MenuItem();
            menuItemCopyAssigments.Header = "Copy Assignments From...";

            menuItemCopyAll = new MenuItem();
            menuItemCopyAll.Header = "Copy All From...";

            menuItemDisable = new MenuItem();
            menuItemDisable.Header = "Disable";
            menuItemDisable.IsCheckable = true;

            miSeparator = new Separator();

            ContextMenu cmCanvas = this.ContextMenu;

            cmCanvas.Items.Add(miSeparator);
            cmCanvas.Items.Add(menuItemShowAll);
            cmCanvas.Items.Add(menuItemHideAll);
            cmCanvas.Items.Add(menuItemSnapTo);

            cmCanvas.Items.Add(menuItemCopyAssigments);
            cmCanvas.Items.Add(menuItemCopyAll);
            cmCanvas.Items.Add(menuItemDisable);
        }

        private void UpdateMenus()
        {
            if (EnvelopeBlockMachine.MachineState.Patterns[EnvelopePatternIndex] == null)
            {
                return;
            }
            
            menuItemSnapToTick.IsChecked = EnvelopeBlockMachine.MachineState.Patterns[EnvelopePatternIndex].SnapToTick;
            menuItemSnapToBeat.IsChecked = EnvelopeBlockMachine.MachineState.Patterns[EnvelopePatternIndex].SnapToBeat;

            //cmCanvas.Items.Add(menuItemCopyAssigments);

            // Menus are surprisingly heavy, update only after user opens submenu
            object dummySub = new object();

            if (menuItemCopyAssigments.Items.Count == 0 && EnvelopeBlockMachine.host.Machine.Patterns.Count > 1)
                menuItemCopyAssigments.Items.Add(dummySub);

            menuItemCopyAssigments.SubmenuOpened += delegate
            {
                if (menuItemCopyAssigments.Items[0].GetType() == typeof(object))
                {
                    menuItemCopyAssigments.Items.Clear();
                    foreach (IPattern pat in EnvelopeBlockMachine.host.Machine.Patterns)
                    {
                        if (pat.Name != EnvelopeBlockMachine.MachineState.Patterns[EnvelopePatternIndex].Pattern)
                        {
                            MenuItem miPat = new MenuItem();
                            miPat.Header = pat.Name;
                            miPat.Click += MiPatCopyAssignments_Click;
                            menuItemCopyAssigments.Items.Add(miPat);
                        }
                    }
                }
            };


            //cmCanvas.Items.Add(menuItemCopyAll);

            object dummySub2 = new object();

            if (menuItemCopyAll.Items.Count == 0 &&
                EnvelopeBlockMachine.host.Machine.Patterns != null &&
                EnvelopeBlockMachine.host.Machine.Patterns.Count > 1)
                menuItemCopyAll.Items.Add(dummySub2);

            menuItemCopyAll.SubmenuOpened += delegate
            {
                if (menuItemCopyAll.Items[0].GetType() == typeof(object))
                {
                    menuItemCopyAll.Items.Clear();
                    foreach (IPattern pat in EnvelopeBlockMachine.host.Machine.Patterns)
                    {
                        if (pat.Name != EnvelopeBlockMachine.MachineState.Patterns[EnvelopePatternIndex].Pattern)
                        {
                            MenuItem miPat = new MenuItem();
                            miPat.Header = pat.Name;
                            miPat.Click += MiPatCopyAll_Click;
                            menuItemCopyAll.Items.Add(miPat);
                        }
                    }
                }
            };

            //cmCanvas.Items.Add(menuItemDisable);
            menuItemDisable.IsChecked = EnvelopeBlockMachine.MachineState.Patterns[EnvelopePatternIndex].PatternDisabled;

            if (this.envelopes != null && ContextMenu != null)
                envelopes.UpdateMenus();
        }

        private void MenuItemHideAll_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < EnvelopeBlockMachine.MachineState.Patterns[EnvelopePatternIndex].Envelopes.Length; i++)
            {
                if (EnvelopeBlockMachine.MachineState.Patterns[EnvelopePatternIndex].Envelopes[i].MachineName != "")
                {
                    EnvelopeBlockMachine.MachineState.Patterns[EnvelopePatternIndex].Envelopes[i].EnvelopeVisible = false;
                    EnvelopeBlockMachine.RaisePropertyReDraw((EnvelopeLayer)envelopes.Children[i]);
                }
            }
            EnvelopeBlockMachine.RaisePropertyUpdateMenu(this);
        }

        private void MenuItemShowAll_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < EnvelopeBlockMachine.MachineState.Patterns[EnvelopePatternIndex].Envelopes.Length; i++)
            {
                if (EnvelopeBlockMachine.MachineState.Patterns[EnvelopePatternIndex].Envelopes[i].MachineName != "")
                {
                    EnvelopeBlockMachine.MachineState.Patterns[EnvelopePatternIndex].Envelopes[i].EnvelopeVisible = true;
                    EnvelopeBlockMachine.RaisePropertyReDraw((EnvelopeLayer)envelopes.Children[i]);
                }
            }
            EnvelopeBlockMachine.RaisePropertyUpdateMenu(this);
        }

        private void MiPatCopyAll_Click(object sender, RoutedEventArgs e)
        {
            int fromIndex = EnvelopeBlockMachine.GetPatternIndex((string)((MenuItem)sender).Header);
            EnvelopeBlockMachine.CopyAll(fromIndex, EnvelopePatternIndex);

            for (int i = 0; i < EnvelopeBlockMachine.MachineState.Patterns[EnvelopePatternIndex].Envelopes.Length; i++)
                EnvelopeBlockMachine.UpdateEnvPoints(EnvelopePatternIndex, i);

            foreach (var el in envelopes.Children)
            {
                EnvelopeBlockMachine.RaisePropertyReDraw((EnvelopeLayer)el);
            }

            // Update every Layer indepenently
            // EnvelopeBlockMachine.RaisePropertyReDrawCanvas(this);
            EnvelopeBlockMachine.RaisePropertyUpdateMenu(this);
        }

        private void MiPatCopyAssignments_Click(object sender, RoutedEventArgs e)
        {
            int fromIndex = EnvelopeBlockMachine.GetPatternIndex((string)((MenuItem)sender).Header);
            EnvelopeBlockMachine.CopyAssignments(fromIndex, EnvelopePatternIndex);

            for (int i = 0; i < EnvelopeBlockMachine.MachineState.Patterns[EnvelopePatternIndex].Envelopes.Length; i++)
                EnvelopeBlockMachine.UpdateEnvPoints(EnvelopePatternIndex, i);

            foreach (var el in envelopes.Children)
            {
                EnvelopeBlockMachine.RaisePropertyReDraw((EnvelopeLayer)el);
            }
            EnvelopeBlockMachine.RaisePropertyUpdateMenu(this);
        }

        private void EnvelopeBlockMachine_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "UpdateMenu")
            {
                EnvelopeCanvas ec = (EnvelopeCanvas)sender;
                if (this.envelopPatternIndex != ec.envelopPatternIndex)
                    return;

                DisableEvents();
                UpdateMenus();
                if (this.ContextMenu != null)
                    this.ContextMenu.IsOpen = false;
                EnableEvents();
            }
            else if (e.PropertyName == "UpdateAllMenus")
            {
                DisableEvents();
                UpdateMenus();
                if (this.ContextMenu != null)
                    this.ContextMenu.IsOpen = false;
                EnableEvents();
            }
            else if (e.PropertyName == "ReDrawCanvas")
            {
                EnvelopeBlockEvent ec = (EnvelopeBlockEvent)sender;
                if (this.envelopPatternIndex != ec.envelopPatternIndex)
                    return;

                string patternName = EnvelopeBlockMachine.MachineState.Patterns[envelopPatternIndex].Pattern;
                PatternLengthInSeconds = EnvelopeBlockMachine.GetPatternLenghtInSeconds(patternName);

                DisableEvents();
                envelopes.DrawLengthInSeconds = PatternLengthInSeconds;
                UpdateGraph();
                EnableEvents();
            }
            else if (e.PropertyName == "ReDrawAllCanvases")
            {
                string patternName = EnvelopeBlockMachine.MachineState.Patterns[envelopPatternIndex].Pattern;
                PatternLengthInSeconds = EnvelopeBlockMachine.GetPatternLenghtInSeconds(patternName);

                DisableEvents();
                envelopes.DrawLengthInSeconds = PatternLengthInSeconds;
                UpdateGraph();
                EnableEvents();
            }
        }

        public void DisableEvents()
        {

            menuItemSnapToTick.Checked -= MenuItemSnapToTick_Checked;
            menuItemSnapToTick.Unchecked -= MenuItemSnapToTick_Unchecked;

            menuItemSnapToBeat.Checked -= MenuItemSnapToBeat_Checked;
            menuItemSnapToBeat.Unchecked -= MenuItemSnapToBeat_Unchecked;

            menuItemDisable.Checked -= MiDisable_Checked;
            menuItemDisable.Unchecked -= MiDisable_Unchecked;
        }

        public void EnableEvents()
        {
            menuItemSnapToTick.Checked += MenuItemSnapToTick_Checked;
            menuItemSnapToTick.Unchecked += MenuItemSnapToTick_Unchecked;

            menuItemSnapToBeat.Checked += MenuItemSnapToBeat_Checked;
            menuItemSnapToBeat.Unchecked += MenuItemSnapToBeat_Unchecked;

            menuItemDisable.Checked += MiDisable_Checked;
            menuItemDisable.Unchecked += MiDisable_Unchecked;
        }

        private void MiDisable_Unchecked(object sender, RoutedEventArgs e)
        {
            DisableEvents();
            EnvelopeBlockMachine.MachineState.Patterns[envelopPatternIndex].PatternDisabled = false;
            EnvelopeBlockMachine.RaisePropertyUpdateMenu(this);
            EnableEvents();
        }

        private void MiDisable_Checked(object sender, RoutedEventArgs e)
        {
            DisableEvents();
            EnvelopeBlockMachine.MachineState.Patterns[envelopPatternIndex].PatternDisabled = true;
            EnvelopeBlockMachine.RaisePropertyUpdateMenu(this);
            EnableEvents();
        }

        private void MenuItemSnapToBeat_Unchecked(object sender, RoutedEventArgs e)
        {
            DisableEvents();
            EnvelopeBlockMachine.MachineState.Patterns[envelopPatternIndex].SnapToBeat = false;
            EnvelopeBlockMachine.RaisePropertyUpdateMenu(this);
            EnableEvents();
        }

        private void MenuItemSnapToBeat_Checked(object sender, RoutedEventArgs e)
        {
            DisableEvents();
            EnvelopeBlockMachine.MachineState.Patterns[envelopPatternIndex].SnapToBeat = true;
            EnvelopeBlockMachine.MachineState.Patterns[envelopPatternIndex].SnapToTick = false;
            EnvelopeBlockMachine.RaisePropertyUpdateMenu(this);
            EnableEvents();
        }

        private void MenuItemSnapToTick_Unchecked(object sender, RoutedEventArgs e)
        {
            DisableEvents();
            EnvelopeBlockMachine.MachineState.Patterns[envelopPatternIndex].SnapToTick = false;
            EnvelopeBlockMachine.RaisePropertyUpdateMenu(this);
            EnableEvents();
        }

        private void MenuItemSnapToTick_Checked(object sender, RoutedEventArgs e)
        {
            DisableEvents();
            EnvelopeBlockMachine.MachineState.Patterns[envelopPatternIndex].SnapToTick = true;
            EnvelopeBlockMachine.MachineState.Patterns[envelopPatternIndex].SnapToBeat = false;
            EnvelopeBlockMachine.RaisePropertyUpdateMenu(this);
            EnableEvents();
        }

        /// <summary>
        /// Handles the actual drawing of the whole graph view...
        /// </summary>
        public void UpdateGraph()
        {
            this.Children.Clear();
            Brush limitsBrush = new SolidColorBrush(Color.FromArgb(0xA0, 10, 10, 10));

            if (LayoutMode == SequencerLayout.Vertical)
            {
                DrawVertical(this);
                DrawEnvelopeLimitsVer(this, limitsBrush);
            }
            else
            {
                DrawHorizontal(this);
                DrawEnvelopeLimitsHor(this, limitsBrush);
            }

            this.Children.Add(envelopes);
            envelopes.Draw();
        }

        private void WaveCanvas_Unloaded(object sender, RoutedEventArgs e)
        {
            //foreach (Task t in taskList)
            //    t.Dispose();

            Children.Clear();

            DisableEvents();
            EnvelopeBlockMachine.PropertyChanged -= EnvelopeBlockMachine_PropertyChanged;

            EnvelopeBlockMachine.host.Machine.PatternAdded -= Machine_PatternAdded;
            EnvelopeBlockMachine.host.Machine.PatternRemoved -= Machine_PatternRemoved;

            Global.Buzz.Song.MachineAdded -= Song_MachineAdded;
            Global.Buzz.Song.MachineRemoved -= Song_MachineRemoved;

            Global.Buzz.PropertyChanged -= Buzz_PropertyChanged;

            ContextMenu = null;
        }

        private void WaveCanvas_Loaded(object sender, RoutedEventArgs e)
        {
            EnvelopeBlockMachine.PropertyChanged += EnvelopeBlockMachine_PropertyChanged;

            EnvelopeBlockMachine.host.Machine.PatternAdded += Machine_PatternAdded;
            EnvelopeBlockMachine.host.Machine.PatternRemoved += Machine_PatternRemoved;

            Global.Buzz.Song.MachineAdded += Song_MachineAdded;
            Global.Buzz.Song.MachineRemoved += Song_MachineRemoved;

            Global.Buzz.PropertyChanged += Buzz_PropertyChanged;

            UpdateGraph();
            EnableEvents();
        }

        /// <summary>
        /// All this is a bit hacky. I wanted to reuse some functions in AudioBlock.cs and that forces some weirdish code. At some point add proper fucntions
        /// that do exactly what is needed...
        /// 
        /// Also Canvas could be separated in to different class to embed multiple waves to window.
        /// </summary>
        /// <param name="canvas"></param>
        public void DrawVertical(Canvas canvas)
        {
            double sampleLength;

            sampleLength = ((double)Global.Buzz.SelectedAudioDriverSampleRate) * PatternLengthInSeconds;

            Brush tickBrush = new SolidColorBrush(Utils.ChangeColorBrightness(Global.Buzz.ThemeColors["SE Pattern Box"], -0.3f));
            tickBrush.Opacity = 0.35;

            DrawTickLinesForVerWave(canvas, sampleLength, tickBrush);
        }

        public void DrawHorizontal(Canvas canvas)
        {
            double sampleLength;

            sampleLength = ((double)Global.Buzz.SelectedAudioDriverSampleRate) * PatternLengthInSeconds;

            Brush tickBrush = new SolidColorBrush(Utils.ChangeColorBrightness(Global.Buzz.ThemeColors["SE Pattern Box"], -0.3f));
            tickBrush.Opacity = 0.35;

            DrawTickLinesForHorWave(canvas, sampleLength, tickBrush);
        }

        private void DrawTickLinesForVerWave(Canvas canvas, double sampleLength, Brush strokeBrush)
        {
            double pixelsPerTick = TickHeight > 0 ? TickHeight : 16;

            int tick = (Global.Buzz.TPB - (Time % Global.Buzz.TPB));
            if (tick == Global.Buzz.TPB)
                tick = 0;

            double patPosTPBPixels = tick * pixelsPerTick;

            double start = patPosTPBPixels + 1;

            if (start < 0)
                start = pixelsPerTick - (-start % pixelsPerTick);

            int ticksPerBeat = EnvelopeBlockMachine.host.MasterInfo.TicksPerBeat;
            int ticking = 0;

            if (pixelsPerTick > 10)
            {
                for (double y = start; y < canvas.Height; y += pixelsPerTick)
                {
                    Line myLine = new Line();

                    myLine.Stroke = strokeBrush;
                    myLine.SnapsToDevicePixels = true;
                    myLine.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
                    myLine.StrokeThickness = 1.0;

                    // if (ticking % ticksPerBeat == 0)
                    //    myLine.StrokeThickness = 1.0;

                    //ticking++;

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
                        myLine.StrokeThickness = 1.0;

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

        private void DrawTickLinesForHorWave(Canvas canvas, double sampleLength, Brush strokeBrush)
        {
            double pixelsPerTick = TickHeight > 0 ? TickHeight : 16;

            int tick = (Global.Buzz.TPB - (Time % Global.Buzz.TPB));
            if (tick == Global.Buzz.TPB)
                tick = 0;

            double patPosTPBPixels = tick * pixelsPerTick;

            double start = patPosTPBPixels + 1;

            if (start < 0)
                start = pixelsPerTick - (-start % pixelsPerTick);

            int ticksPerBeat = EnvelopeBlockMachine.host.MasterInfo.TicksPerBeat;
            int ticking = 0;

            if (pixelsPerTick > 10)
            {
                for (double x = start; x < canvas.Height; x += pixelsPerTick)
                {
                    Line myLine = new Line();

                    myLine.Stroke = strokeBrush;
                    myLine.SnapsToDevicePixels = true;
                    myLine.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
                    myLine.StrokeThickness = 1.0;

                    // if (ticking % ticksPerBeat == 0)
                    //    myLine.StrokeThickness = 1.0;

                    //ticking++;

                    myLine.X1 = x;
                    myLine.Y1 = 0;
                    myLine.X2 = x; 
                    myLine.Y2 = canvas.Height - 1;

                    canvas.Children.Add(myLine);
                }
            }
            else if (pixelsPerTick * ticksPerBeat > 10)
            {
                for (double x = start; x < canvas.Height; x += pixelsPerTick)
                {
                    if (ticking % ticksPerBeat == 0)
                    {
                        Line myLine = new Line();

                        myLine.Stroke = strokeBrush;
                        myLine.SnapsToDevicePixels = true;
                        myLine.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
                        myLine.StrokeThickness = 1.0;

                        myLine.X1 = x;
                        myLine.Y1 = 0;
                        myLine.X2 = x; 
                        myLine.Y2 = canvas.Width - 1;

                        canvas.Children.Add(myLine);
                    }

                    ticking++;
                }
            }
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
            myLine.Opacity = 0.6;
            myLine.Stroke = strokeBrush;
            myLine.SnapsToDevicePixels = true;
            myLine.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
            myLine.StrokeThickness = 1;
            myLine.StrokeDashArray = new DoubleCollection() { 4, 4 };

            y = canvas.Height * (1.0 - (1.0 - EnvelopeBase.ENVELOPE_VIEW_SCALE_ADJUST) / 2.0);

            myLine.X1 = 0;
            myLine.Y1 = y;
            myLine.X2 = canvas.Width;
            myLine.Y2 = y;

            canvas.Children.Add(myLine);
        }
    }
}
