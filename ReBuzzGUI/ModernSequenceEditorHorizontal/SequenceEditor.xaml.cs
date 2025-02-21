using BuzzGUI.Common;
using BuzzGUI.Common.Actions;
using BuzzGUI.Common.Actions.MachineActions;
using BuzzGUI.Common.Actions.PatternActions;
using BuzzGUI.Common.Actions.SequenceActions;
using BuzzGUI.Common.Actions.SongActions;
using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.Common.Templates;
using BuzzGUI.Interfaces;
using BuzzGUI.SequenceEditor.Actions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WDE.ModernSequenceEditorHorizontal.Actions;
using BuzzSeq = BuzzGUI.SequenceEditor;

//using static WDE.ModernSequenceEditorHorizontal.TrackHeaderControl;

namespace WDE.ModernSequenceEditorHorizontal
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class SequenceEditor : UserControl, INotifyPropertyChanged
    {
        ISong song;
        public ISong Song
        {
            get { return song; }
            set
            {
                if (song != null)
                {
                    song.SequenceAdded -= song_SequenceAdded;
                    song.SequenceRemoved -= song_SequenceRemoved;
                    song.SequenceChanged -= song_SequenceChanged;
                    song.PropertyChanged -= song_PropertyChanged;
                    song.MachineAdded -= song_MachineAdded;
                    song.MachineRemoved -= song_MachineRemoved;
                    song.Buzz.PropertyChanged -= Buzz_PropertyChanged;
                    song.Buzz.PatternEditorActivated -= Buzz_PatternEditorActivated;
                    //song.ConnectionAdded -= Song_ConnectionAdded;
                    //song.ConnectionRemoved -= Song_ConnectionRemoved;
                }

                PatternElement.InvalidateResources();

                song = value;

                if (song != null)
                {
                    song.SequenceAdded += song_SequenceAdded;
                    song.SequenceRemoved += song_SequenceRemoved;
                    song.SequenceChanged += song_SequenceChanged;
                    song.PropertyChanged += song_PropertyChanged;
                    song.MachineAdded += song_MachineAdded;
                    song.MachineRemoved += song_MachineRemoved;
                    song.Buzz.PropertyChanged += Buzz_PropertyChanged;
                    song.Buzz.PatternEditorActivated += Buzz_PatternEditorActivated;
                    //song.ConnectionAdded += Song_ConnectionAdded;
                    //song.ConnectionRemoved += Song_ConnectionRemoved;

                    if (song.Associations.ContainsKey("ModernSequenceEditorHorizontalViewSettings"))
                    {
                        viewSettings = (ViewSettings)song.Associations["ModernSequenceEditorHorizontalViewSettings"];
                    }
                    else
                    {
                        viewSettings = new ViewSettings(this);
                        //viewSettings.TimeSignatureList.Changed += TimeSignatureListChanged;
                        song.Associations["ModernSequenceEditorHorizontalViewSettings"] = viewSettings;
                    }

                    if (BuzzSeq.SequenceEditor.ViewSettings.PatternAssociations != null)
                    {
                        BuzzSeq.SequenceEditor.ViewSettings.TimeSignatureList.Changed += TimeSignatureListChanged;
                    }

                    trackSV.ScrollToHorizontalOffset(0);
                    trackSV.ScrollToVerticalOffset(0);
                    AddAllSequences();
                    UpdateWidth();
                    cursorElement.Time = 0;
                    cursorElement.Row = 0;
                    cursorElement.IsActive = false;
                    selectionLayer.KillSelection();
                    PropertyChanged.RaiseAll(this);
                }
            }
        }

        void song_MachineAdded(IMachine m)
        {
            PropertyChanged.Raise(this, "MachineList");

            m.PropertyChanged += Machine_PropertyChanged;
        }

        void song_MachineRemoved(IMachine m)
        {
            PropertyChanged.Raise(this, "MachineList");

            m.PropertyChanged -= Machine_PropertyChanged;
        }

        void song_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "SongEnd":
                    UpdateWidth();
                    break;

                case "LoopStart":
                    UpdateWidth();
                    break;

                case "LoopEnd":
                    UpdateWidth();
                    break;

                case "PlayPosition":
                    PlayPositionChanged();
                    break;
            }
        }

        DispatcherTimer dtPatternNotifier = new DispatcherTimer();
        int tsNotifySeqIndex = 0;
        int tsNotifyEventIndex = 0;

        void Buzz_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "ActiveView":
                    if (Buzz.ActiveView != BuzzView.PatternView)
                        ClearEditContext();
                    break;
                case "BPM":
                    timeLineElement.InvalidateVisual();
                    break;
                case "TPB":
                    timeLineElement.InvalidateVisual();
                    UpdateBackgroundMarkers();
                    break;
                case "Playing":
                    /*
                    if (!Buzz.Playing && Settings.AutoUpdateEventHints)
                    {
                        tsNotifySeqIndex = 0;
                        tsNotifyEventIndex = 0;
                        dtPatternNotifier.Interval = TimeSpan.FromMilliseconds(50);
                        dtPatternNotifier.Tick += (sender2, e2) =>
                        {
                            if (tsNotifySeqIndex < trackStack.Children.Count)
                            {
                                var tc = (trackStack.Children[tsNotifySeqIndex] as TrackControl);
                                if (tsNotifyEventIndex < tc.Sequence.Events.Count)
                                {
                                    tc.UpdateNoteHints(tsNotifyEventIndex);
                                    tsNotifyEventIndex++;
                                    dtPatternNotifier.Start(); // Ensure the count start from 0;
                                }
                                else
                                {
                                    tsNotifyEventIndex = 0;
                                    tsNotifySeqIndex++;
                                }
                            }
                            else
                            {
                                dtPatternNotifier.Stop();
                            }
                        };
                        dtPatternNotifier.Start();
                    }
                    else
                    {
                        dtPatternNotifier.Stop();
                    }
                    */
                    break;
            }
        }

        internal void PatternBottomDragStart(TrackControl tc, SequenceEvent se, ISequence sequence, PatternResizeMode mode)
        {
            int column = trackStack.Children.IndexOf(tc);
            PatResizeHelper.StartResize(tc, se, SelectedSequence, column, mode, resizeRect);
            Mouse.Capture(trackViewGrid);
        }

        void Machine_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var m = sender as IMachine;

            if (e.PropertyName == "Patterns")
            {
                // remove PatternAssociations
                // viewSettings.PatternAssociations.Remove(k => k.Machine == m && !m.Patterns.Contains(k));
            }
        }

        void Buzz_PatternEditorActivated()
        {
            ClearEditContext();
        }

        void AddAllSequences()
        {
            trackStack.Children.Clear();
            trackHeaderStack.Children.Clear();
            for (int i = 0; i < song.Sequences.Count; i++) AddSequence(i);
            TrackCountChanged();
        }

        void AddSequence(int i)
        {
            trackStack.Children.Insert(i, new TrackControl(this) { ViewSettings = viewSettings, Height = viewSettings.TrackHeight, HorizontalAlignment = HorizontalAlignment.Left, Sequence = song.Sequences[i] });
            trackHeaderStack.Children.Insert(i, new TrackHeaderControl(this) { ViewSettings = viewSettings, Height = viewSettings.TrackHeight, HorizontalAlignment = HorizontalAlignment.Left, Resources = this.Resources, Sequence = song.Sequences[i] });
        }

        void song_SequenceAdded(int i)
        {
            AddSequence(i);
            TrackCountChanged();
        }

        void song_SequenceRemoved(int i)
        {
            var trackControl = trackStack.Children[i] as TrackControl;
            trackControl.Sequence = null;

            trackStack.Children.RemoveAt(i);
            (trackHeaderStack.Children[i] as TrackHeaderControl).Sequence = null;
            trackHeaderStack.Children.RemoveAt(i);
            TrackCountChanged();
        }

        void song_SequenceChanged(int i)
        {
            (trackStack.Children[i] as TrackControl).Sequence = song.Sequences[i];
            (trackHeaderStack.Children[i] as TrackHeaderControl).Sequence = song.Sequences[i];
        }

        void TrackCountChanged()
        {
            viewSettings.TrackCount = trackStack.Children.Count;

            selectionLayer.KillSelection();
            clipboard.Clear();

            if (cursorElement.Row >= viewSettings.TrackCount)
                cursorElement.Row = viewSettings.TrackCount - 1;
        }

        void SetMarkerPosition(MarkerControl m, int p)
        {
            Canvas.SetLeft(m, p * viewSettings.TickWidth - Math.Floor(playPosMarker.ActualWidth / 2));
        }

        void PlayPositionChanged()
        {
            SetMarkerPosition(playPosMarker, song.PlayPosition);
        }

        void UpdateWidth()
        {
            viewSettings.SongEnd = song.SongEnd;
            SetMarkerPosition(songEndMarker, song.SongEnd);

            SetMarkerPosition(loopStartMarker, song.LoopStart);
            loopStartMarker.Visibility = song.LoopStart != 0 ? Visibility.Visible : Visibility.Hidden;

            SetMarkerPosition(loopEndMarker, song.LoopEnd);

            int maxt = Math.Max(song.SongEnd, CursorBottomTime);
            double w = viewSettings.TickWidth * maxt + 1;
            /*
            if (PatResizeHelper.Resizing)
            {
                double resRectH = Canvas.GetTop(resizeRect) + resizeRect.Height;
                w = resRectH > w ? resRectH : w;
            }
            */

            timeLineElement.Width = w;
            trackStack.Width = w;
            markerCanvas.Width = w;
            bgMarkerCanvas.Width = w;
            //fgMarkerCanvas.Height = h;

            UpdateBackgroundMarkers();

            selectionLayer.UpdateVisual();

            timeLineElement.InvalidateVisual();
        }

        void UpdateBackgroundMarkers()
        {
            bgMarkerCanvas.Children.Clear();
            fgMarkerCanvas.Children.Clear();
            bgMarkerCanvas.Visibility = Visibility.Collapsed;
            fgMarkerCanvas.Visibility = Visibility.Collapsed;
            Canvas target = Settings.BGMarkerToForeground == false ? bgMarkerCanvas : fgMarkerCanvas;
            target.Visibility = Visibility.Visible;

            if (Settings.BackgroundMarker == BackgroundMarkerMode.Line)
            {
                double x = 1;

                Brush bgLineBrush = TryFindResource("SeqEdBackgroundMarkerLineBrush") as Brush;
                if (bgLineBrush == null)
                    bgLineBrush = new SolidColorBrush(Color.FromArgb(0x55, 0x00, 0x00, 0x00));

                double contentWidth = trackSV.ExtentWidth < trackSV.ActualWidth ? trackSV.ActualWidth : trackSV.ExtentWidth + 1;
                while (x < contentWidth)
                {
                    Line line = new Line() { X1 = x, Y1 = 0, X2 = x, Y2 = Math.Max(trackStack.ActualHeight, this.ActualHeight), Stroke = bgLineBrush, StrokeThickness = 1 };
                    line.SnapsToDevicePixels = true;
                    line.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
                    target.Children.Add(line);

                    x += viewSettings.TickWidth * Buzz.TPB * (double)Settings.BGMarkerPerBeat;
                }
            }
            else if (Settings.BackgroundMarker == BackgroundMarkerMode.Rectangle)
            {
                double x = 1;
                int canDraw = 0;

                Brush bgRectBrush = TryFindResource("SeqEdBackgroundMarkerRectBrush") as Brush;
                if (bgRectBrush == null)
                    bgRectBrush = new SolidColorBrush(Color.FromArgb(0x1a, 0x00, 0x00, 0x00));

                double contentWidth = trackSV.ExtentWidth < trackSV.ActualWidth ? trackSV.ActualWidth : trackSV.ExtentWidth + 1;
                while (x < contentWidth)
                {
                    if (canDraw % 2 == 1)
                    {
                        //int time = (int)(y / SequenceEditor.ViewSettings.TickHeight);
                        //double height = SequenceEditor.ViewSettings.TimeSignatureList.GetBarLengthAt(time) * SequenceEditor.ViewSettings.TickHeight;
                        double width = viewSettings.TickWidth * Buzz.TPB * (double)Settings.BGMarkerPerBeat;
                        Rectangle rect = new Rectangle() { Width = width, Height = Math.Max(trackStack.ActualHeight, this.ActualHeight), Fill = bgRectBrush };
                        Canvas.SetLeft(rect, x);
                        Canvas.SetTop(rect, 0);
                        rect.SnapsToDevicePixels = true;
                        rect.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
                        target.Children.Add(rect);
                    }
                    x += viewSettings.TickWidth * Buzz.TPB * (double)Settings.BGMarkerPerBeat;
                    canDraw++;
                }
            }

            if (Settings.VerticalBackgroundMarker)
            {
                target = bgMarkerCanvas;
                target.Visibility = Visibility.Visible;
                double y = viewSettings.TrackHeight;

                Brush bgLineBrush = TryFindResource("SeqEdBackgroundMarkerLineBrush") as Brush;
                if (bgLineBrush == null)
                    bgLineBrush = new SolidColorBrush(Color.FromArgb(0x55, 0x00, 0x00, 0x00));

                double contentHeigth = Math.Max(trackStack.ActualHeight, this.ActualHeight);
                double contentWidth = trackSV.ExtentWidth < trackSV.ActualWidth ? trackSV.ActualWidth : trackSV.ExtentWidth + 1;

                while (y < contentHeigth)
                {
                    Line line = new Line() { X1 = 0, Y1 = y, X2 = contentWidth, Y2 = y, Stroke = bgLineBrush, StrokeThickness = 1 };
                    line.SnapsToDevicePixels = true;
                    line.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
                    target.Children.Add(line);
                    y += viewSettings.TrackHeight;
                }
            }
        }

        TrackHeaderControl SelectedTrackHeader
        {
            get
            {
                if (trackHeaderStack.Children.Count == 0 || cursorElement.Row < 0 || cursorElement.Row >= trackHeaderStack.Children.Count) return null;
                return trackHeaderStack.Children[cursorElement.Row] as TrackHeaderControl;
            }
        }

        ISequence SelectedSequence { get { return SelectedTrackHeader != null ? SelectedTrackHeader.Sequence : null; } }

        int CursorRow { get { return cursorElement.Row; } }
        int CursorTime { get { return cursorElement.Time; } }
        int CursorSpan { get { return BuzzSeq.SequenceEditor.ViewSettings.TimeSignatureList.GetBarLengthAt(cursorElement.Time); } }
        int CursorBottomTime { get { return Settings.OldStyleSetEndMarkers ? CursorTime : CursorTime + CursorSpan; } }

        IPattern GetPatternAt(int time, int row)
        {
            if (row >= song.Sequences.Count || row < 0) return null;
            return song.Sequences[row].Events.Where(e => time >= e.Key && time < e.Key + e.Value.Span).Select(e => e.Value.Pattern).FirstOrDefault();
        }

        SequenceEvent GetSequenceEventAt(int time, int row)
        {
            SequenceEvent se = null;
            if (row >= song.Sequences.Count || row < 0) return null;
            se = song.Sequences[row].Events.Where(e => time >= e.Key && time < e.Key + e.Value.Span).FirstOrDefault().Value;

            if (se == null)
                se = song.Sequences[row].Events.Where(e => time >= e.Key && time < e.Key + viewSettings.NonPlayPattenSpan).FirstOrDefault().Value;

            return se;
        }

        IPattern CursorPattern { get { return GetPatternAt(cursorElement.Time, cursorElement.Row); } }

        static ViewSettings viewSettings;
        public static ViewSettings ViewSettings
        {
            get
            {
                return viewSettings;
            }
        }

        public void SetVisibility(bool visible)
        {
            // only hide the grid that contains PatternElements to keep layout working
            trackViewGrid.Visibility = visible ? Visibility.Visible : Visibility.Hidden;
        }

        public void PlayCursor()
        {
            song.PlayPosition = CursorTime;
            song.Buzz.Playing = true;
        }

        public IBuzz Buzz { get; set; }
        public BuzzSeq.SequenceEditor MainSeqenceEditor { get; }
        public bool IntegraetedToBuzz { get; }
        public bool AllowNewWindow { get; }
        public ResourceDictionary ResourceDictionary { get; private set; }
        SequenceClipboard clipboard = new SequenceClipboard();

        void GeneralSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "WPFIdealFontMetrics":
                    PropertyChanged.Raise(this, "TextFormattingMode");
                    break;
            }
        }

        public static HorizontalSequenceEditorSettings Settings = new HorizontalSequenceEditorSettings();
        public HorizontalSequenceEditorSettings BindableSettings { get { return Settings; } }

        void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "PatternBoxLook":
                    PatternElement.InvalidateResources();
                    foreach (TrackControl tc in trackStack.Children) tc.EventsChanged();
                    break;

                case "PatternBoxColors":
                    PatternElement.InvalidateResources();
                    foreach (TrackControl tc in trackStack.Children) tc.EventsChanged();
                    break;

                case "TimelineNumbers":
                    //SetTimeLineColumnWidth();
                    break;
                /*
			case "HideEditor":
				if (Settings.HideEditor)
					resizeGrid.RowDefinitions[0].Height = new GridLength(0);
				else if (resizeGrid.RowDefinitions[0].Height.Value == 0)
					resizeGrid.RowDefinitions[0].Height = new GridLength(resizeGrid.RowDefinitions[0].MaxHeight);

				break;
				*/
                case "BGMarkerPerBeat":
                    UpdateBackgroundMarkers();
                    break;
                case "BackgroundMarker":
                    UpdateBackgroundMarkers();
                    break;
                case "VerticalBackgroundMarker":
                    UpdateBackgroundMarkers();
                    break;
                case "PatternNameBackground":
                    PatternElement.InvalidateResources();
                    foreach (TrackControl tc in trackStack.Children) tc.EventsChanged();
                    break;
                case "TrackHeight":
                    UpdateBackgroundMarkers();
                    PatternElement.InvalidateResources();
                    foreach (TrackControl tc in trackStack.Children) { tc.EventsChanged(); tc.Height = viewSettings.TrackHeight; }
                    foreach (TrackHeaderControl tc in trackHeaderStack.Children) tc.Height = (int)Settings.TrackHeight;
                    cursorElement.Update();
                    selectionLayer.KillSelection();
                    break;
            }
        }

        void SetTimeLineColumnWidth()
        {
            if (Settings.TimelineNumbers == TimelineNumberModes.Tick || Settings.TimelineNumbers == TimelineNumberModes.Bar)
                TimeLineColumn.Width = new GridLength(50);
            else if (Settings.TimelineNumbers == TimelineNumberModes.Time)
                TimeLineColumn.Width = new GridLength(60);
            else
                TimeLineColumn.Width = new GridLength(84);
            timeLineElement.InvalidateVisual();
        }

        public void UpdateTrackStackVisuals()
        {
            PatternElement.InvalidateResources();
            foreach (TrackControl tc in trackStack.Children) tc.EventsChanged();
        }

        public ICommand AddTrackCommand { get; private set; }
        public ICommand DeleteTrackCommand { get; private set; }
        public ICommand VUMeterTrackCommand { get; private set; }
        public ICommand MoveTrackUpCommand { get; private set; }
        public ICommand MoveTrackDownCommand { get; private set; }
        public ICommand SetStartCommand { get; private set; }
        public ICommand SetEndCommand { get; private set; }
        public ICommand SetTimeSignatureCommand { get; private set; }
        public ICommand SelectPatternCommand { get; private set; }
        public ICommand InsertAllCommand { get; private set; }
        public ICommand DeleteAllCommand { get; private set; }
        public ICommand CutCommand { get; private set; }
        public ICommand CopyCommand { get; private set; }
        public ICommand PasteCommand { get; private set; }
        public ICommand UndoCommand { get; private set; }
        public ICommand RedoCommand { get; private set; }
        public ICommand SettingsCommand { get; private set; }
        public ICommand SetPatternColorCommand { get; private set; }
        public ICommand ExportTrackMIDICommand { get; private set; }
        public ICommand ExportSongMIDICommand { get; private set; }
        public ICommand OpenNewWindowCommand { get; private set; }
        public ICommand RenameCommand { get; private set; }
        public ICommand CreatePatternsCommand { get; private set; }

        DispatcherTimer DispatcherTimerHold { get; set; }
        HoldDragHelper HoldSequenceEventHelper { get; set; }

        public bool ControlPressed { get; }

        string settingsHeader = "Modern Sequence Editor Horizontal";
        public SequenceEditor(IBuzz buzz, ResourceDictionary rd, bool integraetedToBuzz = false)
        {
            this.DataContext = this;
            Buzz = buzz;
            MainSeqenceEditor = BuzzSeq.SequenceEditor.SequenceEditorInstance;
            MainSeqenceEditor.PropertyChanged += MainSeq_PropertyChanged;

            Global.GeneralSettings.PropertyChanged += new PropertyChangedEventHandler(GeneralSettings_PropertyChanged);
            Settings.PropertyChanged += Settings_PropertyChanged;
            SettingsWindow.AddSettings(settingsHeader, Settings);
            if (SettingsWindow.IsWindowVisible)
            {
                Global.Buzz.IsSettingsWindowVisible = false;
                Global.Buzz.IsSettingsWindowVisible = true;
            }

            IntegraetedToBuzz = integraetedToBuzz;
            AllowNewWindow = !IntegraetedToBuzz;
            if (IntegraetedToBuzz)
            {
                Buzz.OpenSong += Buzz_OpenSong;
                Buzz.SaveSong += Buzz_SaveSong;
            }
            ResourceDictionary = rd;

            if (rd != null) this.Resources.MergedDictionaries.Add(rd);
            InitializeComponent();

            PropertyChanged.Raise(this, "AllowNewWindow");

            //timelineSV.Margin = new Thickness(0, 0, 0, SystemParameters.HorizontalScrollBarHeight);
            timelineSV.PreviewMouseDown += (sender, e) =>
            {
                if (e.ChangedButton == MouseButton.Right)
                {
                    if (e.ClickCount == 1)
                    {
                        SelectRow(Mouse.GetPosition(timeLineElement));
                        UpdateMenu();
                    }
                }
            };

            Settings_PropertyChanged(this, new PropertyChangedEventArgs("TimelineNumbers")); // Generate event to update timeline column width

            loopStartMarker.rectangle.Style = TryFindResource("LoopStartMarkerRectangleStyle") as Style;
            loopEndMarker.rectangle.Style = TryFindResource("LoopEndMarkerRectangleStyle") as Style;
            songEndMarker.rectangle.Style = TryFindResource("SongEndMarkerRectangleStyle") as Style;
            playPosMarker.rectangle.Style = TryFindResource("PlayPositionMarkerRectangleStyle") as Style;

            trackViewGrid.Visibility = Visibility.Collapsed;

            zoomSlider.PreviewMouseRightButtonDown += (sender, e) =>
            {
                zoomSlider.Value = 2;
                e.Handled = true;
            };

            DispatcherTimerHold = new DispatcherTimer();
            DispatcherTimerHold.Interval = TimeSpan.FromMilliseconds(500);
            DispatcherTimerHold.Tick += (sender, e) =>
            {
                DispatcherTimerHold.Stop();

                Point p = Mouse.GetPosition(trackStack);
                int row = GetRowAt(p.Y);
                int time = GetTimeAt(p.X);
                int snapTime = BuzzSeq.SequenceEditor.ViewSettings.TimeSignatureList.Snap(time, int.MaxValue);

                while (snapTime >= 0)
                {
                    snapTime = BuzzSeq.SequenceEditor.ViewSettings.TimeSignatureList.Snap(snapTime, int.MaxValue);
                    if (SelectedSequence.Events.ContainsKey(snapTime))
                        break;

                    snapTime--;
                }

                if (snapTime >= 0)
                {
                    HoldSequenceEventHelper.Reset(SelectedSequence, row, Song);
                    HoldSequenceEventHelper.IsHolding = true;
                    HoldSequenceEventHelper.SetDraggedSequenceEvent(snapTime);
                    HoldSequenceEventHelper.SetDragOffset(time - snapTime);
                    Mouse.OverrideCursor = Cursors.Hand;
                    selectionLayer.EndSelect();
                }
            };

            HoldSequenceEventHelper = new HoldDragHelper();
            PatResizeHelper = new PatternResizeHelper();

            // Animate PatResizeHelper
            var myDoubleAnimation = new DoubleAnimation();
            myDoubleAnimation.From = 0.0;
            myDoubleAnimation.To = 1000.0;
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(60));
            myDoubleAnimation.AutoReverse = true;
            Storyboard myStoryboard = new Storyboard();
            myStoryboard.Children.Add(myDoubleAnimation);
            Storyboard.SetTargetName(myDoubleAnimation, resizeRect.Name);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Rectangle.StrokeDashOffsetProperty));
            myStoryboard.Begin(resizeRect);

            trackSV.ScrollChanged += (sender, e) =>
            {
                timelineSV.ScrollToHorizontalOffset(e.HorizontalOffset);
                markerSV.ScrollToHorizontalOffset(e.HorizontalOffset);
                trackHeaderSV.ScrollToVerticalOffset(e.VerticalOffset);
            };

            timelineSV.PreviewMouseWheel += (sender, e) =>
            {
                if (!(Keyboard.Modifiers == ModifierKeys.Control))
                {
                    trackSV.ScrollToVerticalOffset(trackSV.VerticalOffset + e.Delta / -7.0);
                    e.Handled = true;
                }
            };

            this.PreviewMouseWheel += (sender, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    double direction = 1.0;
                    if (Settings.InvMouseWheelZoom)
                        direction = -1.0;
                    zoomSlider.Value = zoomSlider.Value + direction * e.Delta / 240.0;
                    e.Handled = true;
                }
            };

            this.GotKeyboardFocus += (sender, e) =>
            {
                Buzz.EditContext = viewSettings.EditContext;
                buzz.NewSequenceEditorActivated();
                cursorElement.IsActive = true;
                e.Handled = true;
            };

            this.LostKeyboardFocus += (sender, e) =>
            {
                //Buzz.EditContext = null;
                cursorElement.IsActive = false;
                e.Handled = true;
            };

            cbSteps.Items.Add(1);
            cbSteps.Items.Add(2);
            cbSteps.Items.Add(4);
            int step = 8;
            do
            {
                cbSteps.Items.Add(step);
                step += 4;
            } while (step < 96);
            cbSteps.Items.Add(128);
            cbSteps.Items.Add(192);
            cbSteps.Items.Add(256);

            cbSteps.SelectedItem = 16;
            cbSteps.SelectionChanged += CbSteps_SelectionChanged;

            this.PreviewKeyDown += (sender, e) =>
            {
                if (SelectedSequence == null) return;

                if (HoldSequenceEventHelper.IsHolding)
                {
                    if (Settings.EventDragCloneKey == SequenceEventCloneKey.Ctrl && (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl))
                    {
                        HoldSequenceEventHelper.Clone = true;
                    }
                    else if (Settings.EventDragCloneKey == SequenceEventCloneKey.Shift && (e.Key == Key.LeftShift || e.Key == Key.RightShift))
                    {
                        HoldSequenceEventHelper.Clone = true;
                    }
                    else if (Settings.EventDragCloneKey == SequenceEventCloneKey.Alt && (e.Key == Key.LeftAlt || e.Key == Key.RightAlt))
                    {
                        HoldSequenceEventHelper.Clone = true;
                    }
                }

                if (e.Key == Key.LeftCtrl || e.Key == Key.LeftCtrl)
                {
                    PropertyChanged.Raise(this, "ControlPressed");
                }

                if (Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    if (e.Key == Key.Right)
                    {
                        MoveCursorDelta(1, 0, int.MaxValue, Keyboard.Modifiers == ModifierKeys.Shift);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Left)
                    {
                        MoveCursorDelta(-1, 0, int.MaxValue, Keyboard.Modifiers == ModifierKeys.Shift);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Down)
                    {
                        MoveCursorDelta(0, 1, int.MaxValue, Keyboard.Modifiers == ModifierKeys.Shift);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Up)
                    {
                        MoveCursorDelta(0, -1, int.MaxValue, Keyboard.Modifiers == ModifierKeys.Shift);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.PageDown)
                    {
                        MoveCursorDelta(0, 16, int.MaxValue, Keyboard.Modifiers == ModifierKeys.Shift);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.PageUp)
                    {
                        MoveCursorDelta(0, -16, int.MaxValue, Keyboard.Modifiers == ModifierKeys.Shift);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Home)
                    {
                        if (CursorTime > 0)
                            MoveCursorDelta(0, -int.MaxValue, int.MaxValue, Keyboard.Modifiers == ModifierKeys.Shift);
                        else
                            MoveCursorDelta(-int.MaxValue, 0, int.MaxValue, Keyboard.Modifiers == ModifierKeys.Shift);

                        e.Handled = true;
                    }
                    else if (e.Key == Key.End)
                    {
                        if (CursorTime != BuzzSeq.SequenceEditor.ViewSettings.LastCellTime)
                            MoveCursorDelta(0, int.MaxValue, song.SongEnd, Keyboard.Modifiers == ModifierKeys.Shift);
                        else
                            MoveCursorDelta(int.MaxValue, 0, song.SongEnd, Keyboard.Modifiers == ModifierKeys.Shift);

                        e.Handled = true;
                    }
                }

                if (Keyboard.Modifiers == ModifierKeys.None)
                {
                    if (e.Key == Key.Insert)
                    {
                        Do(new InsertOrDeleteAction(SelectedSequence, CursorTime, CursorSpan, true));
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Delete)
                    {
                        Do(new InsertOrDeleteAction(SelectedSequence, CursorTime, CursorSpan, false));
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Return)
                    {
                        if (SelectPatternCommand.CanExecute(null)) SelectPatternCommand.Execute(null);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Back)
                    {
                        int oldct = CursorTime;
                        MoveCursorDelta(-1, 0, int.MaxValue, false);
                        if (CursorTime < oldct) Do(new ClearAction(SelectedSequence, CursorTime, CursorSpan));
                        e.Handled = true;
                    }
                }

                else if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    if (e.Key == Key.Return)
                    {
                        song.Buzz.ActiveView = BuzzView.SequenceView;
                        e.Handled = true;
                    }
                }
                else if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (e.Key == Key.M)
                    {
                        SelectedSequence.Machine.IsMuted ^= true;
                        e.Handled = true;
                    }
                    else if (e.Key == Key.L)
                    {
                        SelectedSequence.Machine.IsSoloed ^= true;
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Return)
                    {
                        mainGrid.ContextMenu.DataContext = this;
                        mainGrid.ContextMenu.IsOpen = true;
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Up)
                    {
                        if (MoveTrackUpCommand.CanExecute(null)) MoveTrackUpCommand.Execute(null);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Down)
                    {
                        if (MoveTrackDownCommand.CanExecute(null)) MoveTrackDownCommand.Execute(null);
                        e.Handled = true;
                    }

                }
            };

            PreviewKeyUp += (sender, e) =>
            {
                if (e.Key == Key.LeftCtrl || e.Key == Key.LeftCtrl)
                {
                    PropertyChanged.Raise(this, "ControlPressed");
                }
            };

            this.PreviewTextInput += (sender, e) =>
            {
                if (e.Text.Length == 1 && SelectedTrackHeader != null)
                {
                    var p = SelectedTrackHeader.GetPatternByChar(e.Text[0]);
                    if (p != null)
                    {
                        Do(new SetEventAction(SelectedSequence, CursorTime, new SequenceEvent(SequenceEventType.PlayPattern, p)));
                        MoveCursor(CursorTime + CursorPattern.Length, CursorRow);
                    }
                    else if (e.Text[0] == ',')
                    {
                        Do(new SetEventAction(SelectedSequence, CursorTime, new SequenceEvent(SequenceEventType.Break)));
                        MoveCursorDelta(1, 0, int.MaxValue, false);
                    }
                    else if (e.Text[0] == '-')
                    {
                        Do(new SetEventAction(SelectedSequence, CursorTime, new SequenceEvent(SequenceEventType.Mute)));
                        MoveCursorDelta(1, 0, int.MaxValue, false);
                    }
                    else if (e.Text[0] == '_')
                    {
                        Do(new SetEventAction(SelectedSequence, CursorTime, new SequenceEvent(SequenceEventType.Thru)));
                        MoveCursorDelta(1, 0, int.MaxValue, false);
                    }
                    else if (e.Text[0] == '.')
                    {
                        Do(new ClearAction(SelectedSequence, CursorTime, CursorSpan));
                        MoveCursorDelta(1, 0, int.MaxValue, false);
                    }
                }
            };


            AddTrackCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = machine => { Do(new AddSequenceAction(machine as IMachine)); }
            };

            DeleteTrackCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => SelectedSequence != null,
                ExecuteDelegate = x =>
                {
                    //if (MessageBox.Show("Sure?", "Delete Track " + SelectedSequence.Machine.Name, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        Do(new DeleteSequenceAction(SelectedSequence));
                        MoveCursor(CursorTime, 0);
                    }
                    Focus();
                }
            };

            MoveTrackUpCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => SelectedSequence != null && CursorRow > 0,
                ExecuteDelegate = x =>
                {
                    Do(new SwapSequencesAction(SelectedSequence, song.Sequences[CursorRow - 1]));
                    MoveCursorDelta(0, -1, int.MaxValue, false);
                }
            };

            MoveTrackDownCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => song != null && SelectedSequence != null && CursorRow < song.Sequences.Count - 1,
                ExecuteDelegate = x =>
                {
                    Do(new SwapSequencesAction(SelectedSequence, song.Sequences[CursorRow + 1]));
                    MoveCursorDelta(0, 1, int.MaxValue, false);
                }
            };

            SetStartCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    Do(new SetMarkerAction(song, SongMarkers.LoopStart, CursorTime));
                }
            };

            SetEndCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    int t = CursorBottomTime;

                    using (new ActionGroup(viewSettings.EditContext.ActionStack))
                    {
                        if (song.LoopEnd != t)
                            Do(new SetMarkerAction(song, SongMarkers.LoopEnd, t));
                        else
                            Do(new SetMarkerAction(song, SongMarkers.SongEnd, t));
                    }

                    timeLineElement.InvalidateVisual();

                }
            };

            SetTimeSignatureCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => CursorTime < viewSettings.SongEnd,
                ExecuteDelegate = x =>
                {
                    Point p = cursorCanvas.PointToScreen(new Point(Canvas.GetLeft(cursorElement), Canvas.GetTop(cursorElement)));

                    StepEditWindow hw = new StepEditWindow(CursorSpan)
                    {
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Left = p.X,
                        Top = p.Y
                    };

                    new WindowInteropHelper(hw).Owner = ((HwndSource)PresentationSource.FromVisual(this)).Handle;

                    if ((bool)hw.ShowDialog())
                    {
                        Do(new SetTimeSignatureAction(BuzzSeq.SequenceEditor.ViewSettings.TimeSignatureList, CursorTime, hw.Step));
                        cursorElement.Update();
                        cursorElement.SetBlinkAnimation(true, false);
                    }

                    this.Focus();
                }
            };

            SelectPatternCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => SelectedSequence != null,
                ExecuteDelegate = noswitch =>
                {
                    var p = CursorPattern;
                    if (p != null)
                        song.Buzz.SetPatternEditorPattern(p);
                    else if (SelectedSequence != null)
                        song.Buzz.SetPatternEditorMachine(SelectedSequence.Machine);

                    if (noswitch == null || !(bool)noswitch)
                        song.Buzz.ActivatePatternEditor();
                    else
                        this.Focus();

                }
            };

            InsertAllCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => SelectedSequence != null,
                ExecuteDelegate = x =>
                {
                    using (new ActionGroup(viewSettings.EditContext.ActionStack))
                    {
                        Do(new TSLInsertOrDeleteAction(BuzzSeq.SequenceEditor.ViewSettings.TimeSignatureList, CursorTime, CursorSpan, true));

                        foreach (var s in song.Sequences)
                            Do(new InsertOrDeleteAction(s, CursorTime, CursorSpan, true));

                        if (song.SongEnd >= CursorBottomTime) Do(new SetMarkerAction(song, SongMarkers.SongEnd, song.SongEnd + CursorSpan));
                        if (song.LoopEnd >= CursorBottomTime) Do(new SetMarkerAction(song, SongMarkers.LoopEnd, song.LoopEnd + CursorSpan));
                        if (song.LoopStart >= CursorBottomTime) Do(new SetMarkerAction(song, SongMarkers.LoopStart, song.LoopStart + CursorSpan));
                    }

                    cursorElement.Update();
                }
            };

            DeleteAllCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => SelectedSequence != null,
                ExecuteDelegate = x =>
                {
                    using (new ActionGroup(viewSettings.EditContext.ActionStack))
                    {
                        Do(new TSLInsertOrDeleteAction(BuzzSeq.SequenceEditor.ViewSettings.TimeSignatureList, CursorTime, CursorSpan, false));

                        foreach (var s in song.Sequences)
                            Do(new InsertOrDeleteAction(s, CursorTime, CursorSpan, false));

                        if (song.LoopEnd > CursorBottomTime) Do(new SetMarkerAction(song, SongMarkers.LoopEnd, song.LoopEnd - CursorSpan));
                        if (song.SongEnd > CursorBottomTime) Do(new SetMarkerAction(song, SongMarkers.SongEnd, song.SongEnd - CursorSpan));
                        if (song.LoopStart > CursorBottomTime) Do(new SetMarkerAction(song, SongMarkers.LoopStart, song.LoopStart - CursorSpan));
                    }

                    cursorElement.Update();
                }
            };

            CutCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => selectionLayer.SelectionNotEmpty,
                ExecuteDelegate = x =>
                {
                    Do(new CutOrCopySequenceEventsAction(song, selectionLayer.Rect, clipboard, true));
                }
            };

            CopyCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => selectionLayer.SelectionNotEmpty,
                ExecuteDelegate = x =>
                {
                    Do(new CutOrCopySequenceEventsAction(song, selectionLayer.Rect, clipboard, false));
                }
            };

            PasteCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => clipboard.ContainsData && CursorRow == clipboard.FirstTrack,
                ExecuteDelegate = x =>
                {
                    Do(new PasteSequenceEventsAction(song, CursorTime, clipboard));
                }
            };

            UndoCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => viewSettings.EditContext.ActionStack.CanUndo,
                ExecuteDelegate = x => viewSettings.EditContext.ActionStack.Undo()
            };

            RedoCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => viewSettings.EditContext.ActionStack.CanRedo,
                ExecuteDelegate = x => viewSettings.EditContext.ActionStack.Redo()
            };

            SettingsCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    SettingsWindow.Show(this, "Modern Sequence Editor");
                }
            };

            VUMeterTrackCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = machineConnection => { Do(new ChangeVUMeterTargetAction(SelectedTrackHeader, machineConnection as IMachineConnection)); }
            };

            SetPatternColorCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = index =>
                {
                    if (CursorPattern == null) return;

                    var pa = BuzzSeq.SequenceEditor.ViewSettings.PatternAssociations;
                    if (!pa.ContainsKey(CursorPattern))
                        pa[CursorPattern] = new BuzzSeq.PatternEx();

                    pa[CursorPattern].ColorIndex = (int)index;

                    // foreach (TrackControl tc in trackStack.Children) tc.EventsChanged();

                    // This guy will send event to update view
                    MainSeqenceEditor.UpdatePatternBoxes();
                }
            };

            ExportTrackMIDICommand = new SimpleCommand
            {
                CanExecuteDelegate = x => SelectedSequence != null,
                ExecuteDelegate = x => MIDIExporter.ExportMIDI(SelectedSequence)
            };

            ExportSongMIDICommand = new SimpleCommand
            {
                CanExecuteDelegate = x => Song != null,
                ExecuteDelegate = x => MIDIExporter.ExportMIDI(Song)
            };

            //OpenNewWindowCommand = new SimpleCommand
            //{
            //    CanExecuteDelegate = x => true,
            //    ExecuteDelegate = x =>
            //    {
            //        PropertyChanged.Raise(this, "OpenNewWindow");
            //    }
            //};

            RenameCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => SelectedSequence != null,
                ExecuteDelegate = d =>
                {
                    if (SelectedSequence != null)
                    {
                        Point p = this.PointToScreen(Mouse.GetPosition(this));
                        SelectedSequence.Machine.ShowDialog(MachineDialog.Rename, (int)p.X, (int)p.Y);
                    }
                }
            };

            CreatePatternsCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => SelectedSequence != null,
                ExecuteDelegate = num =>
                {
                    int.TryParse(num as string, out int numPatterns);

                    using (new ActionGroup(viewSettings.EditContext.ActionStack))
                    {
                        int time = CursorTime;

                        bool insert = Keyboard.Modifiers == ModifierKeys.Control;

                        // 1
                        if (!insert)
                            Do(new TSLInsertOrDeleteAction(BuzzSeq.SequenceEditor.ViewSettings.TimeSignatureList, numPatterns * Settings.PatternLength, CursorSpan, false)); // Delete
                        Do(new TSLInsertOrDeleteAction(BuzzSeq.SequenceEditor.ViewSettings.TimeSignatureList, numPatterns * Settings.PatternLength, CursorSpan, true)); // Insert

                        // 2
                        if (!insert)
                            Do(new InsertOrDeleteAction(SelectedSequence, CursorTime, numPatterns * Settings.PatternLength, false)); // Delete
                        Do(new InsertOrDeleteAction(SelectedSequence, CursorTime, numPatterns * Settings.PatternLength, true)); // Insert

                        // Add new events
                        for (int i = 0; i < numPatterns; i++)
                        {
                            string pname = SelectedSequence.Machine.GetNewPatternName();
                            Do(new CreatePatternAction(SelectedSequence.Machine, pname, Settings.PatternLength));
                            Do(new SetEventAction(SelectedSequence, time, new SequenceEvent(SequenceEventType.PlayPattern, SelectedSequence.Machine.Patterns.First(p => p.Name == pname))));
                            time += Settings.PatternLength;
                        }

                        //Do(new SetMarkerAction(song, SongMarkers.SongEnd, song.SongEnd + numPatterns * Settings.PatternLength));
                    }

                    cursorElement.Update();
                }
            };

            this.InputBindings.Add(new InputBinding(DeleteTrackCommand, new KeyGesture(Key.Delete, ModifierKeys.Control)));
            this.InputBindings.Add(new InputBinding(SetStartCommand, new KeyGesture(Key.B, ModifierKeys.Control)));
            this.InputBindings.Add(new InputBinding(SetEndCommand, new KeyGesture(Key.E, ModifierKeys.Control)));
            this.InputBindings.Add(new InputBinding(SetTimeSignatureCommand, new KeyGesture(Key.T, ModifierKeys.Control)));
            this.InputBindings.Add(new InputBinding(InsertAllCommand, new KeyGesture(Key.I, ModifierKeys.Control)));
            this.InputBindings.Add(new InputBinding(DeleteAllCommand, new KeyGesture(Key.D, ModifierKeys.Control)));
            this.InputBindings.Add(new InputBinding(CutCommand, new KeyGesture(Key.X, ModifierKeys.Control)));
            this.InputBindings.Add(new InputBinding(CopyCommand, new KeyGesture(Key.C, ModifierKeys.Control)));
            this.InputBindings.Add(new InputBinding(PasteCommand, new KeyGesture(Key.V, ModifierKeys.Control)));
            this.InputBindings.Add(new InputBinding(UndoCommand, new KeyGesture(Key.Z, ModifierKeys.Control)));
            this.InputBindings.Add(new InputBinding(RedoCommand, new KeyGesture(Key.Y, ModifierKeys.Control)));

            new Dragger
            {
                Element = trackViewGrid,
                Gesture = new DragMouseGesture { Button = MouseButton.Left },
                Mode = DraggerMode.Absolute,
                BeginDrag = (p, alt, cc) =>
                {
                    trackViewGrid.Focus();
                    int row = GetRowAt(p.Y);
                    int time = GetTimeAt(p.X);
                    MoveCursor(time, row);
                    selectionLayer.BeginSelect(new Point(time, row));

                    HoldSequenceEventHelper.IsHolding = false;
                    if (GetSequenceEventAt(time, row) != null)
                        DispatcherTimerHold.Start();
                },
                Drag = p =>
                {
                    DispatcherTimerHold.Stop();

                    int row = GetRowAt(p.Y);
                    int time = GetTimeAt(p.X);

                    if (HoldSequenceEventHelper.IsHolding)
                    {
                        HoldSequenceEventHelper.Update(time, row);
                        Point po = Mouse.GetPosition(trackSV);
                        if (po.Y > trackSV.ActualHeight - SystemParameters.HorizontalScrollBarHeight && PatResizeHelper.DelayCompleted)
                        {
                            trackSV.ScrollToVerticalOffset(trackSV.VerticalOffset + viewSettings.TickWidth * Settings.ResizeSnap);
                            UpdateWidth();
                            PatResizeHelper.StartDelay();
                        }
                        else if (po.Y < 0 && PatResizeHelper.DelayCompleted)
                        {

                            trackSV.ScrollToVerticalOffset(trackSV.VerticalOffset - viewSettings.TickWidth * Settings.ResizeSnap);
                            UpdateWidth();
                            PatResizeHelper.StartDelay();
                        }
                    }
                    else
                    {
                        selectionLayer.UpdateSelect(new Point(time, row));
                    }
                },
                EndDrag = p =>
                {
                    selectionLayer.EndSelect();
                    DispatcherTimerHold.Stop();
                    HoldSequenceEventHelper.IsHolding = false;
                    Mouse.OverrideCursor = null;
                }
            };

            trackViewGrid.MouseLeftButtonDown += (sender, e) =>
            {
                if (CursorRow < 0) return;

                if (e.ClickCount == 2 && Keyboard.Modifiers != ModifierKeys.Shift && Keyboard.Modifiers != ModifierKeys.Control)
                {
                    if (CursorPattern != null)
                    {
                        Buzz.ActiveView = BuzzView.PatternView;
                        Buzz.ActivatePatternEditor();
                    }
                    if (CursorPattern == null)
                    {
                        using (new ActionGroup(viewSettings.EditContext.ActionStack))
                        {
                            string pname = SelectedSequence.Machine.GetNewPatternName();
                            Do(new CreatePatternAction(SelectedSequence.Machine, pname, Settings.PatternLength));
                            Do(new SetEventAction(SelectedSequence, CursorTime, new SequenceEvent(SequenceEventType.PlayPattern, SelectedSequence.Machine.Patterns.First(p => p.Name == pname))));
                        }
                    }
                    else if (!Settings.AutoSelectPattern)
                    {
                        if (SelectPatternCommand.CanExecute(null)) SelectPatternCommand.Execute(null);
                    }

                    e.Handled = true;
                }
            };

            trackViewGrid.MouseRightButtonDown += (sender, e) =>
            {

                var p = e.GetPosition(trackViewGrid);
                trackViewGrid.Focus();
                int row = GetRowAt(p.Y);
                int time = GetTimeAt(p.X);
                MoveCursor(time, row);

                UpdateMenu();
            };

            trackViewGrid.PreviewMouseMove += (sender, e) =>
            {
                if (PatResizeHelper.Resizing)
                {
                    if (Mouse.LeftButton == MouseButtonState.Pressed)
                    {
                        PatResizeHelper.Update(resizeRect, e.GetPosition(trackViewGrid));
                        Mouse.OverrideCursor = Cursors.SizeWE;

                        // Scroll down if mouse goes below visible area. Not working.
                        Point p = e.GetPosition(trackSV);
                        if (p.Y > trackSV.ActualHeight - SystemParameters.HorizontalScrollBarHeight && PatResizeHelper.DelayCompleted)
                        {

                            trackSV.ScrollToVerticalOffset(trackSV.VerticalOffset + viewSettings.TickWidth * Settings.ResizeSnap);
                            UpdateWidth();
                            PatResizeHelper.StartDelay();
                        }
                        else if (p.Y < 0 && PatResizeHelper.DelayCompleted)
                        {

                            trackSV.ScrollToVerticalOffset(trackSV.VerticalOffset - viewSettings.TickWidth * Settings.ResizeSnap);
                            UpdateWidth();
                            PatResizeHelper.StartDelay();
                        }

                        double w = Canvas.GetLeft(resizeRect) + resizeRect.ActualWidth;
                        int time = GetTimeAt(w);
                        if (time > Song.SongEnd)
                            Song.SongEnd = time;
                    }
                    else
                    {
                        Mouse.OverrideCursor = null;
                        PatResizeHelper.Stop();
                    }
                }
            };

            trackViewGrid.PreviewMouseLeftButtonUp += (sender, e) =>
            {
                if (PatResizeHelper.Resizing)
                {
                    Mouse.OverrideCursor = null;
                    trackViewGrid.ReleaseMouseCapture();
                    PatResizeHelper.Stop();

                    if (PatResizeHelper.PatternResizeMode == PatternResizeMode.Right)
                    {
                        Do(new SetLengthAction(PatResizeHelper.SequenceEvent.Pattern, (int)(resizeRect.Width / ViewSettings.TickWidth)));
                    }
                    else if (PatResizeHelper.PatternResizeMode == PatternResizeMode.Left)
                    {
                        int time = PatResizeHelper.OriginalSnapTime;
                        int newTime = (int)(Canvas.GetLeft(resizeRect) / ViewSettings.TickWidth);
                        newTime = newTime < 0 ? 0 : newTime;

                        SequenceEditor.ViewSettings.EditContext.ActionStack.BeginActionGroup();
                        Do(new ClearAction(PatResizeHelper.TrackControl.Sequence, time, PatResizeHelper.SequenceEvent.Span));
                        Do(new SetEventAction(PatResizeHelper.TrackControl.Sequence, newTime, PatResizeHelper.SequenceEvent));
                        Do(new SetLengthAction(PatResizeHelper.SequenceEvent.Pattern, (int)(resizeRect.Width / ViewSettings.TickWidth)));
                        SequenceEditor.ViewSettings.EditContext.ActionStack.EndActionGroup();
                    }
                }
            };


            this.SizeChanged += (sender, e) =>
            {
                double mh = Math.Max(0, mainGrid.RowDefinitions[0].ActualHeight + mainGrid.RowDefinitions[1].ActualHeight - SystemParameters.HorizontalScrollBarHeight - 6);
                markerSV.Height = mh;
                songEndMarker.Height = mh;
                loopStartMarker.Height = mh;
                loopEndMarker.Height = mh;
                playPosMarker.Height = mh;

                mh = Math.Max(0, mainGrid.RowDefinitions[1].ActualHeight - 6.0);
                trackSV.Height = mh;
                trackHeaderSV.Height = mh;

                double mw = Math.Max(0, mainGrid.ColumnDefinitions[1].ActualWidth - SystemParameters.VerticalScrollBarWidth - 10);
                markerSV.Width = mw;
                
                //markerSV.Margin = new Thickness(0, 0, 0, 0);
                markerCanvas.Width = mw;

                UpdateBackgroundMarkers();

                //songEndMarker.Width = mw;
                //songEndMarker.rectangle.Width = mw;
                //songEndMarker.Height = 1;
                //loopStartMarker.Width = mw;
                //loopStartMarker.rectangle.Width = mw;
                //loopStartMarker.Height = 1;
                //loopEndMarker.Width = mw;
                //loopEndMarker.rectangle.Width = mw;
                //loopEndMarker.Height = 1;
                //playPosMarker.Width = mw;
                //playPosMarker.rectangle.Width = mw;
                //playPosMarker.Height = 1;
            };

            resizeGrid.SizeChanged += (sender, e) =>
            {
                Settings.Save();
            };

            zoomSlider.ValueChanged += (sender, e) =>
            {
                if (timer != null)
                    timer.Stop();

                timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.3) };
                timer.Start();
                timer.Tick += (sender2, args) =>
                {
                    timer.Stop();
                    UpdateZoomSlider();
                };
            };

            patternWnd = new Window();

            timelineSV.PreviewMouseLeftButtonDown += (sender, e) =>
            {
                int t = (int)(e.GetPosition(timeLineElement).X / viewSettings.TickWidth);
                song.PlayPosition = BuzzSeq.SequenceEditor.ViewSettings.TimeSignatureList.Snap(t, int.MaxValue);
            };

            patternListBox.RemoveHandler(MouseLeftButtonDownEvent, new MouseButtonEventHandler(patternListBox_MouseLeftButtonDown));
            patternListBox.AddHandler(MouseLeftButtonDownEvent, new MouseButtonEventHandler(patternListBox_MouseLeftButtonDown), true);

            patternListBox.MouseRightButtonDown += (sender, e) =>
            {
                UpdateMenu();
            };

            patternListBox.MouseEnter += (sender, e) =>
            {
                var location = this.PointToScreen(e.GetPosition(this));
                patternWnd.WindowStyle = WindowStyle.None;
                patternWnd.ShowInTaskbar = false;
                patternWnd.ResizeMode = ResizeMode.NoResize;
            };

            patternListBox.MouseMove += (sender, e) =>
            {
                if (CursorRow < 0) return;

                var item = ItemsControl.ContainerFromElement(patternListBox, e.OriginalSource as DependencyObject) as ListBoxItem;
                if (item != null)
                {
                    PatternListItem pli = (PatternListItem)item.Content;

                    Point location = Win32Mouse.GetScreenPosition();
                    location.X /= WPFExtensions.PixelsPerDip;
                    location.Y /= WPFExtensions.PixelsPerDip;
                    patternWnd.Left = location.X - pli.Pattern.Length * ViewSettings.TickWidth - 10;
                    patternWnd.Top = location.Y;

                    if (patternWndCurrentPli != pli)
                    {
                        patternWndCurrentPli = pli;
                        patternWnd.Height = ViewSettings.TrackHeight;
                        patternWnd.Width = pli.Pattern.Length * ViewSettings.TickWidth;

                        PatternElement pe = new PatternElement((TrackControl)trackStack.Children[CursorRow], 0, new SequenceEvent(SequenceEventType.PlayPattern, pli.Pattern, pli.Pattern.Length), viewSettings);
                        patternWnd.Content = pe;
                        if (patternHelperDictionary.ContainsKey(pli.Pattern))
                        {
                            PatternPlayMode ppm = patternHelperDictionary[pli.Pattern];
                            if (ppm != PatternPlayMode.Stop)
                                pe.PlayAnimation(ppm == PatternPlayMode.Looping);
                        }

                        patternWnd.Topmost = true;

                    }

                    patternWnd.Show();
                    trackViewGrid.Focus();
                }
            };

            patternListBox.MouseLeave += (sender, e) =>
            {
                patternWnd.Hide();
            };
        }

        private void CbSteps_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var cb = (ComboBox)sender;

            Do(new SetTimeSignatureAction(BuzzSeq.SequenceEditor.ViewSettings.TimeSignatureList, 0, (int)cb.SelectedValue));
            cursorElement.Update();
            cursorElement.SetBlinkAnimation(true, false);

            this.Focus();
        }

        private void MainSeq_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "CursorMoved")
            {
                int time = MainSeqenceEditor.CursorElement.Time;
                int row = MainSeqenceEditor.CursorElement.Row;
                MoveCursor(time, row);
            }
            else if (e.PropertyName == "PatternAssociations")
            {
                foreach (TrackControl tc in trackStack.Children) tc.EventsChanged();
            }
            else if (e.PropertyName == "TimeSignatureListInit")
            {
                BuzzSeq.SequenceEditor.ViewSettings.TimeSignatureList.Changed += TimeSignatureListChanged;
                TimeSignatureListChanged();
            }
            else if (e.PropertyName == "TimeSignatureListDelete")
            {
                BuzzSeq.SequenceEditor.ViewSettings.TimeSignatureList.Changed -= TimeSignatureListChanged;
            }
        }

        internal void UpdateMenu()
        {
            PropertyChanged.Raise(this, "EnableColorMenu");
            PropertyChanged.Raise(this, "EnableVUMeterMenu");
            PropertyChanged.Raise(this, "MachineConnectionList");
            PropertyChanged.Raise(this, "IsCursorVisible");
        }

        private IPattern FindPlayingSoloPattern()
        {
            foreach (IMachine mac in Song.Machines)
                foreach (IPattern pat in mac.Patterns)
                    if (pat.IsPlayingSolo)
                        return pat;

            return null;
        }

        private void RaisePatternPlayChangedEvent(IPattern pat, PatternPlayMode mode)
        {
            PropertyChanged.Raise(new PatternPlayEvent(pat, mode), "PatternPlayMode");
        }

        Window patternWnd;
        PatternListItem patternWndCurrentPli;
        Dictionary<IPattern, PatternPlayMode> patternHelperDictionary = new Dictionary<IPattern, PatternPlayMode>();

        private void SendNoteOffs(IPattern pat)
        {
            pat.Machine.SendMIDIControlChange(0x7b, pat.Machine.MIDIInputChannel, 0); // Midi all notes off

            foreach (var col in pat.Columns)
            {
                if (col.Parameter.Type == ParameterType.Note)
                {
                    col.Parameter.SetValue(col.Track, BuzzNote.Off);
                    if (col.Machine.DLL.Info.Version >= 42)
                        col.Machine.SendControlChanges();
                }
            }
        }

        internal void RaisePatternPlayEvent()
        {
            PropertyChanged.Raise(this, "PatternPlayEvent");
        }

        DispatcherTimer timer;

        public void Release()
        {
            Global.GeneralSettings.PropertyChanged -= GeneralSettings_PropertyChanged;
            Settings.PropertyChanged -= Settings_PropertyChanged;

            cbSteps.SelectionChanged -= CbSteps_SelectionChanged;

            patternListBox.RemoveHandler(MouseLeftButtonDownEvent, new MouseButtonEventHandler(patternListBox_MouseLeftButtonDown));

            if (IntegraetedToBuzz)
            {
                Buzz.OpenSong -= Buzz_OpenSong;
                Buzz.SaveSong -= Buzz_SaveSong;
            }

            BuzzSeq.SequenceEditor.SequenceEditorInstance.PropertyChanged -= MainSeq_PropertyChanged;
            BuzzSeq.SequenceEditor.ViewSettings.TimeSignatureList.Changed -= TimeSignatureListChanged;

            SettingsWindow.RemoveSettings(settingsHeader);
        }

        int DataVersion = 2;

        void Buzz_OpenSong(IOpenSong os)
        {
            Stream s = os.GetSubSection("ModernSequenceEditorHorizontal");
            if (s == null) return;
            var br = new BinaryReader(s);
            int ver = br.ReadInt32();
            if (ver > DataVersion) return;

            zoomSlider.Value = br.ReadDouble();
            //viewSettings.TimeSignatureList = new TimeSignatureList(br);
            //BuzzGUI.SequenceEditor.SequenceEditor.ViewSettings.TimeSignatureList.Changed += TimeSignatureListChanged;

            if (ver >= 2)
            {
                // Depricated
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    string mname = br.ReadString();
                    string pname = br.ReadString();
                    int ci = br.ReadInt32();

                    //var mac = song.Machines.Where(m => m.Name == mname).FirstOrDefault();
                    //if (mac != null)
                    //{
                    //    var pat = mac.Patterns.FirstOrDefault(p => p.Name == pname);
                    //    if (pat != null) viewSettings.PatternAssociations[pat] = new PatternEx() { ColorIndex = ci };
                    //}
                }

                count = br.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    // ToDo
                    int index = br.ReadInt32();
                    int type = br.ReadInt32();
                }
            }

            UpdateZoomSlider();
            timeLineElement.InvalidateVisual();
            UpdateWidth();
        }

        public void SetViewSettings(TimeSignatureList tsl, double zoom, Tuple<string, string, int>[] patternAsso, Tuple<int, string, string>[] vus)
        {
            zoomSlider.Value = zoom;

            BuzzSeq.SequenceEditor.ViewSettings.TimeSignatureList.Changed += TimeSignatureListChanged;
            /*
            for (int i = 0; i < patternAsso.Length; i++)
            {
                string mname = patternAsso[i].Item1;
                string pname = patternAsso[i].Item2;
                int ci = patternAsso[i].Item3;

                var mac = song.Machines.Where(m => m.Name == mname).FirstOrDefault();
                if (mac != null)
                {
                    var pat = mac.Patterns.FirstOrDefault(p => p.Name == pname);
                    if (pat != null) viewSettings.PatternAssociations[pat] = new PatternEx() { ColorIndex = ci };
                }
            }
            */
            for (int i = 0; i < vus.Length; i++)
            {
                int index = vus[i].Item1;
                string machineFrom = vus[i].Item2;
                string machineTo = vus[i].Item3;

                if (index < song.Sequences.Count)
                {
                    ISequence seq = song.Sequences[index];

                    var mac = song.Machines.Where(m => m.Name == machineFrom).FirstOrDefault();
                    if (mac != null && seq != null)
                    {
                        var conn = mac.Outputs.Where(c => c.Destination.Name == machineTo).FirstOrDefault();
                        if (conn != null)
                        {
                            TrackHeaderControl thc = null;
                            foreach (TrackHeaderControl thc2 in trackHeaderStack.Children)
                            {
                                if (thc2.Sequence == seq)
                                {
                                    thc = thc2;
                                    break;
                                }
                            }

                            if (thc != null)
                            {
                                viewSettings.VUMeterMachineConnection[seq] = conn;
                                thc.SelectedConnection = conn;
                            }
                        }
                    }
                }
            }
        }

        void Buzz_SaveSong(ISaveSong ss)
        {
            Stream s = ss.CreateSubSection("ModernSequenceEditorHorizontal");
            var bw = new BinaryWriter(s);
            bw.Write(DataVersion);
            bw.Write(zoomSlider.Value);
            bw.Write(0);
            //viewSettings.TimeSignatureList.Write(bw);

            // Depricated
            bw.Write(0);
            /*
            bw.Write(viewSettings.PatternAssociations.Count);
            foreach (var pa in viewSettings.PatternAssociations)
            {
                bw.Write(pa.Key.Machine.Name);
                bw.Write(pa.Key.Name);
                bw.Write(pa.Value.ColorIndex);
            }
            */

            bw.Write(viewSettings.VUMeterMachineConnection.Count());
            int index = 0;
            foreach (var vu in viewSettings.VUMeterMachineConnection)
            {
                // Todo
                bw.Write(index);
                bw.Write(0); // Type, 
                index++;
            }
        }

        void UpdateZoomSlider()
        {
            ViewSettings.TickWidth = zoomSlider.Value;
            UpdateWidth();
            foreach (TrackControl tc in trackStack.Children)
                tc.EventsChanged();

            cursorElement.Update();
        }

        void MoveCursor(int time, int row)
        {
            if (time == cursorElement.Time && row == cursorElement.Row)
                return;

            cursorElement.Row = Math.Min(row, trackHeaderStack.Children.Count - 1);
            cursorElement.Time = BuzzSeq.SequenceEditor.ViewSettings.TimeSignatureList.Snap(time, int.MaxValue);
            UpdateWidth();
            cursorElement.BringIntoView();
            UpdateSelectedRow();

            for (int i = 0; i < patternListBox.Items.Count; i++)
            {
                if ((patternListBox.Items[i] as PatternListItem).Pattern == CursorPattern)
                {
                    patternListBox.SelectedIndex = i;
                    break;
                }
            }

            MainSeqenceEditor.MoveCursorMain(cursorElement.Time, cursorElement.Row);
        }

        void MoveCursorDelta(int dx, int dy, int maxx, bool select)
        {
            if (select)
            {
                if (!selectionLayer.Selecting)
                    selectionLayer.BeginSelect(new Point(CursorTime, CursorRow));
            }
            else
            {
                selectionLayer.KillSelection();
            }

            cursorElement.Move(dx, dy, maxx);
            UpdateWidth();
            UpdateSelectedRow();

            if (select)
                selectionLayer.UpdateSelect(new Point(CursorTime, CursorRow));

            MainSeqenceEditor.MoveCursorMain(cursorElement.Time, cursorElement.Row);
        }

        public void UpdateSelectedRow()
        {
            if (cursorElement.Row < 0 || cursorElement.Row >= trackHeaderStack.Children.Count) return;

            for (int i = 0; i < trackHeaderStack.Children.Count; i++)
                (trackHeaderStack.Children[i] as TrackHeaderControl).IsSelected = i == cursorElement.Row;

            patternListBox.SetBinding(ListBox.ItemsSourceProperty, new Binding("PatternList") { Source = SelectedTrackHeader });

            if (Settings.AutoSelectPattern && SelectPatternCommand.CanExecute(null))
                SelectPatternCommand.Execute(true);

            var m = SelectedSequence.Machine;

            if (m != null && m.DLL.Info.Type == MachineType.Generator || m.IsControlMachine)
                Buzz.MIDIFocusMachine = m;
        }

        private void patternListBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (CursorRow < 0) return;

            if (e.ClickCount == 2 && Keyboard.Modifiers != ModifierKeys.Shift && Keyboard.Modifiers != ModifierKeys.Control)
            {
                ListBox lb = (ListBox)sender;

                if (lb.SelectedItems.Count == 1)
                {
                    PatternListItem pli = (PatternListItem)lb.SelectedItem;
                    Do(new SetEventAction(SelectedSequence, CursorTime, new SequenceEvent(SequenceEventType.PlayPattern, pli.Pattern)));
                    MoveCursor(CursorTime + CursorPattern.Length, CursorRow);
                    trackViewGrid.Focus();
                }
                e.Handled = true;
            }
            else if (e.ClickCount == 1 && Keyboard.Modifiers == ModifierKeys.Shift && Settings.ClickPlayPattern)
            {
                PatternListItem pli = null;

                var item = ItemsControl.ContainerFromElement(patternListBox, e.OriginalSource as DependencyObject) as ListBoxItem;
                if (item != null)
                {
                    pli = (PatternListItem)item.Content;
                }

                if (pli != null)
                {
                    IPattern pat = pli.Pattern;

                    int bar = Settings.ClickPlayPatternSyncToTick;
                    var time = song.LoopStart + ((song.PlayPosition - song.LoopStart + bar) / bar * bar % (song.LoopEnd - song.LoopStart));

                    SequenceEventType seqType = SequenceEventType.PlayPattern;
                    int column = 0;
                    foreach (var seq in Song.Sequences)
                    {
                        if (seq.Machine == pat.Machine)
                        {
                            UpdatePatternAmin(seq, pat, seqType, time, PatternPlayMode.Play);
                        }
                        column++;
                    }
                }
                e.Handled = true;
            }
            else if (e.ClickCount == 1 && Keyboard.Modifiers == ModifierKeys.Control && Settings.ClickPlayPattern)
            {
                PatternListItem pli = null;

                var item = ItemsControl.ContainerFromElement(patternListBox, e.OriginalSource as DependencyObject) as ListBoxItem;
                if (item != null)
                {
                    pli = (PatternListItem)item.Content;
                }

                if (pli != null)
                {
                    IPattern pat = pli.Pattern;

                    int bar = Settings.ClickPlayPatternSyncToTick;
                    var time = song.LoopStart + ((song.PlayPosition - song.LoopStart + bar) / bar * bar % (song.LoopEnd - song.LoopStart));

                    SequenceEventType seqType = SequenceEventType.PlayPattern;
                    int column = 0;
                    foreach (var seq in Song.Sequences)
                    {
                        if (seq.Machine == pat.Machine)
                        {
                            UpdatePatternAmin(seq, pat, seqType, time, PatternPlayMode.Looping);
                        }
                        column++;
                    }
                }
                e.Handled = true;
            }
        }

        internal void UpdatePatternAmin(ISequence seq, IPattern pat, SequenceEventType seqType, int time, PatternPlayMode ppm)
        {
            if (seq.PlayingPattern == pat && FindPlayingSoloPattern() == null)
            {
                seqType = SequenceEventType.Break;
                SendNoteOffs(pat);
                seq.TriggerEvent(song.PlayPosition + 1, new SequenceEvent(seqType), false);
                RaisePatternPlayChangedEvent(pat, PatternPlayMode.Stop);
                patternHelperDictionary.Remove(pat);
            }
            else
            {
                if (!Buzz.Playing)
                {
                    pat.IsPlayingSolo = !pat.IsPlayingSolo;
                    RaisePatternPlayChangedEvent(pat, ppm);
                    patternHelperDictionary[pat] = ppm;
                }
                else
                {
                    IPattern soloPattern = FindPlayingSoloPattern();
                    if (pat == soloPattern)
                    {
                        pat.IsPlayingSolo = false;
                        patternHelperDictionary.Remove(pat);
                        RaisePatternPlayChangedEvent(pat, PatternPlayMode.Stop);
                    }
                    else if (soloPattern != null)
                    {
                        pat.IsPlayingSolo = true;
                        patternHelperDictionary.Remove(soloPattern);
                        RaisePatternPlayChangedEvent(pat, PatternPlayMode.Stop);
                        RaisePatternPlayChangedEvent(soloPattern, ppm);
                    }
                    else
                    {
                        seq.TriggerEvent(time, new SequenceEvent(seqType, pat), ppm == PatternPlayMode.Looping);
                        RaisePatternPlayChangedEvent(pat, ppm);
                        patternHelperDictionary[pat] = ppm;
                    }
                }
            }
        }

        int GetTimeAt(double y)
        {
            return Math.Max(0, (int)(y / viewSettings.TickWidth));
        }

        int GetRowAt(double y)
        {
            return Math.Max(0, Math.Min(trackStack.Children.Count - 1, (int)(y / viewSettings.TrackHeight)));
        }

        internal void SelectRow(TrackHeaderControl tc)
        {
            MoveCursor(CursorTime, trackHeaderStack.Children.IndexOf(tc));
        }

        internal void SelectRow(Point p)
        {
            trackViewGrid.Focus();
            int row = cursorElement.Row;
            int time = GetTimeAt(p.X);
            MoveCursor(time, row);
        }

        public IEnumerable<MenuItemVM> MachineList { get { return song.Machines.Select(m => new MenuItemVM() { Text = m.Name, Command = AddTrackCommand, CommandParameter = m }).OrderBy(m => m.Text); } }

        public IEnumerable<MenuItemVM> MachineConnectionList
        {
            get
            {
                if (SelectedTrackHeader != null)
                    return SelectedTrackHeader.Sequence.Machine.Outputs.Select(m => new MenuItemVM() { Text = m.Destination.Name, Command = VUMeterTrackCommand, CommandParameter = m }).OrderBy(m => m.Text);
                else
                    return null;
            }
        }

        public double ZoomLevel
        {
            get { return viewSettings != null ? viewSettings.TickWidth : 2; }
            set
            {
                viewSettings.TickWidth = value < zoomSlider.Minimum ? zoomSlider.Minimum : value;
                PropertyChanged.Raise(this, "ZoomLevel");
                zoomSlider.ToolTip = "Zoom Level: " + String.Format("{0:0.00}", value);
            }
        }

        void Do(IAction a)
        {
            try
            {
                ViewSettings.EditContext.ActionStack.Do(a);
            }
            catch { }
        }

        void TimeSignatureListChanged()
        {
            cbSteps.SelectionChanged -= CbSteps_SelectionChanged;
            cbSteps.SelectedValue = BuzzSeq.SequenceEditor.ViewSettings.TimeSignatureList.GetBarLengthAt(0);
            cbSteps.SelectionChanged += CbSteps_SelectionChanged;
            timeLineElement.InvalidateVisual();
        }

        void ClearEditContext()
        {
            if (Buzz.EditContext == viewSettings.EditContext)
            {
                Buzz.EditContext = null;
            }
        }

        public class ColorMenuVM
        {
            public string Name { get; set; }
            public Brush Brush { get; set; }
            public ICommand Command { get; set; }
            public object CommandParameter { get; set; }
            public bool IsSeparator { get; set; }
        }

        List<ColorMenuVM> colorMenuItems;
        public IEnumerable<ColorMenuVM> ColorMenuItems
        {
            get
            {
                if (colorMenuItems == null)
                {
                    colorMenuItems = new List<ColorMenuVM>();
                    if (PatternElement.PatternBrushes != null)
                    {
                        colorMenuItems = PatternElement.PatternBrushes.Select((b, index) => new ColorMenuVM()
                        {
                            Name = b.Item1,
                            Brush = b.Item2.Brush,
                            Command = SetPatternColorCommand,
                            CommandParameter = index,
                        }).Concat(new[]
                        {
                            new ColorMenuVM() { IsSeparator = true },
                            new ColorMenuVM() { Name = "Default", Command = SetPatternColorCommand, CommandParameter = -1 }
                        }).ToList();
                    }
                }
                return colorMenuItems;
            }
        }

        public bool EnableColorMenu { get { return CursorPattern != null; } }

        public bool IsCursorVisible
        {
            get
            {
                bool ret = true;
                Point p = new Point(Canvas.GetLeft(cursorElement), Canvas.GetTop(cursorElement));
                if (p.X < 0 || p.X + cursorElement.Width - trackSV.ContentHorizontalOffset > trackSV.ActualWidth)
                    ret = false;
                if (p.Y < 0 || p.Y + cursorElement.Height - trackSV.ContentVerticalOffset > trackSV.ActualHeight)
                    ret = false;

                return ret;
            }
        }

        public bool EnableVUMeterMenu { get { return (SelectedSequence != null && SelectedSequence.Machine.Outputs.Count > 1); } }


        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        private void AddTrackButtonClick(object sender, RoutedEventArgs e)
        {
            addTrackButtonContextMenu.PlacementTarget = this;
            addTrackButtonContextMenu.IsOpen = true;
        }

        public TextFormattingMode TextFormattingMode { get { return Global.GeneralSettings.WPFIdealFontMetrics ? TextFormattingMode.Ideal : TextFormattingMode.Display; } }

        public PatternResizeHelper PatResizeHelper { get; private set; }
    }
}
