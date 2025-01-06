using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Common.Templates;
using BuzzGUI.Interfaces;
using ModernSequenceEditor.Interfaces;
using Sanford.Multimedia.Midi;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WDE.ModernSequenceEditorHorizontal
{
    public class PatternElement : Canvas, INotifyPropertyChanged
    {
        static readonly int MaxCacheableWidth = WPFExtensions.DPI == 96 ? 1024 : 0;

        internal SequenceEvent se;
        ViewSettings viewSettings;
        TrackControl tc;
        internal int time;
        Canvas macCanvas;
        Canvas eventHintCanvas;
        //Canvas patternPlayCanvas;
        Brush patternEventHintBrush;
        Brush patternEventHintBrushOff;
        Brush patternEventHintParam;
        Brush patternEventBorder;
        Brush trFill;
        Brush trStroke;

        public PatternElement(TrackControl tc, int time, SequenceEvent se, ViewSettings vs)
        {
            this.tc = tc;
            this.time = time;
            this.se = se;
            viewSettings = vs;
            /*
			this.IsVisibleChanged += (sender, e) =>
			{
				if (IsVisible)
				{
					if (renderPending)
					{
						renderPending = false;
						InvalidateVisual();
					}
				}
			};
			*/

            Loaded += (sender, e) =>
            {
                SequenceEditor.Settings.PropertyChanged += Settings_PropertyChanged;
                this.MouseRightButtonDown += PatternElement_MouseRightButtonDown;
                if (this.se.Type == SequenceEventType.PlayPattern)
                    this.se.Pattern.PatternChanged += Pattern_PatternChanged;

                tc.Editor.PropertyChanged += Editor_PropertyChanged;

                DrawElements();
            };

            Unloaded -= (sender, e) =>
            {
                Release();
            };

            patternEventHintBrush = tc.TryFindResource("SeqEdPatternEventNote") as SolidColorBrush;
            if (patternEventHintBrush == null)
                patternEventHintBrush = Brushes.LightGreen;

            patternEventHintBrushOff = tc.TryFindResource("SeqEdPatternEventNoteOff") as SolidColorBrush;
            if (patternEventHintBrushOff == null)
                patternEventHintBrushOff = Brushes.Gray;

            patternEventHintParam = tc.TryFindResource("SeqEdPatternEventParam") as SolidColorBrush;
            if (patternEventHintParam == null)
                patternEventHintParam = Brushes.Orange;

            patternEventBorder = tc.TryFindResource("SeqEdPatternEventBorder") as SolidColorBrush;
            if (patternEventBorder == null)
                patternEventBorder = Brushes.Black;

            trFill = tc.TryFindResource("PatternBoxBgRectFillBrush") as SolidColorBrush;
            trStroke = tc.TryFindResource("PatternBoxBgRectStrokeBrush") as SolidColorBrush;
            trFill = trFill != null ? trFill : new SolidColorBrush(Color.FromArgb(0x90, 0xe0, 0xe0, 0xff));
            trStroke = trStroke != null ? trStroke : new SolidColorBrush(Color.FromArgb(0x40, 0x33, 0x33, 0x33));

            patternEventHintBrush.Freeze();
            patternEventHintBrushOff.Freeze();
            patternEventHintParam.Freeze();
            patternEventBorder.Freeze();

            this.ClipToBounds = true;
        }

        private void Pattern_PatternChanged(IPatternColumn obj)
        {
            UpdatePatternEvents();
        }

        private void Editor_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "PatternPlayMode")
            {
                var ppe = (PatternPlayEvent)sender;
                if (ppe.pat == se.Pattern || (se.Pattern != null && se.Pattern.IsPlayingSolo))
                    if (ppe.mode == PatternPlayMode.Looping || ppe.mode == PatternPlayMode.Play)
                        PlayAnimation(ppe.mode == PatternPlayMode.Looping);
            }
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "PatternBoxEventHint" || e.PropertyName == "EventToolTip" || e.PropertyName == "PatternEventWidth")
            {
                UpdatePatternEvents();
                InvalidateVisual();
            }
        }

        private void PatternElement_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (macCanvas != null && macCanvas.ContextMenu != null)
            {
                macCanvas.ContextMenu.IsOpen = true;
                e.Handled = true;
            }
        }

        static BrushSet[] brushSet = new BrushSet[3];
        static Brush[] borderBrush = new Brush[1];
        static SolidColorBrush textBrush;

        public static Tuple<string, BrushSet>[] PatternBrushes;

        static Typeface font = new Typeface("Segoe UI");
        private bool patternAnimationStarted;
        private bool looping;
        private Line dragLeft;
        private Line dragRight;

        public void SetDragHitTestVisibility(bool visible)
        {
            if (dragLeft != null && dragRight != null)
            {
                dragLeft.IsHitTestVisible = dragRight.IsHitTestVisible = visible;

                if (visible)
                {
                    if (dragLeft.IsMouseDirectlyOver || dragRight.IsMouseDirectlyOver)
                    {
                        Mouse.OverrideCursor = Cursors.SizeWE;
                    }
                }
                else
                {
                    Mouse.OverrideCursor = null;
                }
            }
        }

        public static void InvalidateResources()
        {
            brushSet[0] = null;
            PatternVisualCache.Clear();
        }

        //protected override void OnRender(DrawingContext dc)
        private void DrawElements()
        {
            //DebugConsole.WriteLine("PatternElement.OnRender " + IsVisible.ToString());
            /*
			if (!IsVisible)
			{
				renderPending = true;
				return;
			}
			*/

            if (brushSet[0] == null)
            {
                brushSet[0] = new BrushSet(tc.TryFindResource("PatternBoxBrush") as SolidColorBrush);
                brushSet[1] = new BrushSet(tc.TryFindResource("BreakBoxBrush") as SolidColorBrush);
                brushSet[2] = new BrushSet(tc.TryFindResource("MuteBoxBrush") as SolidColorBrush);

                // Don't know why these are not found above.
                if (brushSet[1].Brush == null || brushSet[1].Brush.Color == Color.FromArgb(0, 0xff, 0xff, 0xff))
                    brushSet[1] = new BrushSet(new SolidColorBrush(Global.Buzz.ThemeColors["SE Break Box"]));

                if (brushSet[2].Brush == null || brushSet[2].Brush.Color == Color.FromArgb(0, 0xff, 0xff, 0xff))
                    brushSet[2] = new BrushSet(new SolidColorBrush(Global.Buzz.ThemeColors["SE Mute Box"]));

                borderBrush[0] = tc.TryFindResource("PatternBorderBrush") as Brush;
                textBrush = tc.TryFindResource("PatternTextBrush") as SolidColorBrush;
                if (textBrush.Color == Color.FromArgb(0, 0xff, 0xff, 0xff))
                    textBrush = new SolidColorBrush(Global.Buzz.ThemeColors["SE Text"]);

                if (textBrush.CanFreeze) textBrush.Freeze();

                for (int i = 0; i < borderBrush.Length; i++)
                {
                    if (borderBrush[i] != null && borderBrush[i].CanFreeze) borderBrush[i].Freeze();
                }

                var pc = tc.TryFindResource("PatternColors") as NamedColor[];
                if (pc != null) PatternBrushes = pc.Select(nc => Tuple.Create(nc.Name, new BrushSet(new SolidColorBrush(nc.Color)))).ToArray();
            }

            int span = se.Pattern != null ? se.Span : viewSettings.NonPlayPattenSpan;
            string text = se.Pattern != null ? se.Pattern.Name : "";
            double h = viewSettings.TrackHeight;
            double w = span * viewSettings.TickWidth;
            w = Math.Max(w, 10);

            int spanReal = se.Pattern != null ? se.Pattern.Length : viewSettings.NonPlayPattenSpan;
            double w2 = spanReal * viewSettings.TickWidth;
            w2 = Math.Max(w, 10);

            string cktext = "";

            int bi = 0;
            switch (se.Type)
            {
                case SequenceEventType.PlayPattern: bi = 0; cktext = text; break;
                case SequenceEventType.Break: bi = 1; cktext = "<break>"; break;
                case SequenceEventType.Mute: bi = 2; cktext = "<mute>"; break;
                case SequenceEventType.Thru: bi = 2; cktext = "<thru>"; break;
            }

            BrushSet bs;
            int ci = -1;

            if (se.Type == SequenceEventType.PlayPattern && PatternBrushes != null)
            {
                BuzzGUI.SequenceEditor.PatternEx pex = null;

                if (BuzzGUI.SequenceEditor.SequenceEditor.ViewSettings.PatternAssociations.TryGetValue(se.Pattern, out pex))
                    ci = pex.ColorIndex % PatternBrushes.Length;

                if (ci < 0 && SequenceEditor.Settings.PatternBoxColors == PatternBoxColorModes.Pattern)
                    ci = tc.Sequence.Machine.Patterns.IndexOf(se.Pattern) % PatternBrushes.Length;
            }

            if (ci >= 0)
                bs = PatternBrushes[ci].Item2;
            else
                bs = brushSet[bi];

            Children.Clear();

            PatternVisual childVisual = new PatternVisual(w, h, cktext, font, textBrush, borderBrush[0], bs.Brush, bs.HighlightBrush, bs.ShadowBrush);
            Children.Add(new VisualHost { Visual = childVisual });

            eventHintCanvas = new Canvas() { Width = w - 2, Height = h - 2 - 16, Margin = new Thickness(1, 1 + 16, 1, 1), ClipToBounds = true, SnapsToDevicePixels = true };
            BitmapCache bitmapCache = new BitmapCache();
            bitmapCache.RenderAtScale = 1;
            eventHintCanvas.CacheMode = bitmapCache;
            Children.Add(eventHintCanvas);

            this.CacheMode = bitmapCache;

            UpdatePatternEvents();

            // Check if machine can draw to pattern
            IModernSequencerMachineInterface mac = se.Pattern != null ? se.Pattern.Machine.ManagedMachine as IModernSequencerMachineInterface : null;

            if (mac != null)
            {
                macCanvas = mac.PrepareCanvasForSequencer(se.Pattern, SequencerLayout.Horizontal, viewSettings.TickWidth, time, w, h);
                if (macCanvas != null)
                {
                    macCanvas.CacheMode = bitmapCache;

                    if (tc.Editor.ResourceDictionary != null && macCanvas.ContextMenu != null)
                        macCanvas.ContextMenu.Resources.MergedDictionaries.Add(tc.Editor.ResourceDictionary);

                    Children.Add(macCanvas);
                }
            }

            rectPlayAnim = new Rectangle() { Width = w - 2, Height = 0, Margin = new Thickness(1, 1, 1, 1), ClipToBounds = true, SnapsToDevicePixels = true };
            rectPlayAnim.IsHitTestVisible = false;

            this.Children.Add(rectPlayAnim);
            TextBlock tb = new TextBlock() { Margin = new Thickness(6, 1, 2, 1), Foreground = textBrush, FontFamily = new FontFamily("Segoe UI"), FontSize = 12, Text = cktext, Width = w - 10, FlowDirection = FlowDirection.LeftToRight };
            tb.IsHitTestVisible = false;

            if (SequenceEditor.Settings.PatternNameBackground)
            {
                Rectangle textRect = new Rectangle() { Margin = new Thickness(4, 2, 2, 1), Fill = trFill, Stroke = trStroke, Width = w - 8, Height = 15, IsHitTestVisible = false };
                this.Children.Add(textRect);
            }

            this.Children.Add(tb);

            this.IsHitTestVisible = true;
            this.Background = Brushes.Transparent;
            this.Width = w;
            this.Height = h;
            this.MouseLeftButtonDown += (sender, e) =>
            {
                if (e.ClickCount == 1 && Keyboard.Modifiers == ModifierKeys.Shift && SequenceEditor.Settings.ClickPlayPattern)
                {
                    var pat = se.Pattern;
                    var seq = tc.Sequence;

                    if (seq != null && pat != null)
                    {
                        ISong song = tc.Editor.Song;
                        int bar = SequenceEditor.Settings.ClickPlayPatternSyncToTick;
                        var time = song.LoopStart + ((song.PlayPosition - song.LoopStart + bar) / bar * bar % (song.LoopEnd - song.LoopStart));

                        SequenceEventType seqType = SequenceEventType.PlayPattern;
                        tc.Editor.UpdatePatternAmin(seq, pat, seqType, time, PatternPlayMode.Play);
                    }

                    e.Handled = true;
                }
                else if (e.ClickCount == 1 && Keyboard.Modifiers == ModifierKeys.Control && SequenceEditor.Settings.ClickPlayPattern)
                {
                    var pat = se.Pattern;
                    var seq = tc.Sequence;

                    if (seq != null && pat != null)
                    {
                        ISong song = tc.Editor.Song;
                        int bar = SequenceEditor.Settings.ClickPlayPatternSyncToTick;
                        var time = song.LoopStart + ((song.PlayPosition - song.LoopStart + bar) / bar * bar % (song.LoopEnd - song.LoopStart));

                        SequenceEventType seqType = SequenceEventType.PlayPattern;
                        tc.Editor.UpdatePatternAmin(seq, pat, seqType, time, PatternPlayMode.Looping);
                    }

                    e.Handled = true;
                }
            };

            dragLeft = new Line() { X1 = 4, Y1 = 4, X2 = 4, Y2 = h - 4, Stroke = Brushes.Transparent, StrokeThickness = 8, IsHitTestVisible = false };
            dragLeft.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
            this.Children.Add(dragLeft);
            dragRight = new Line() { X1 = w2 - 4, Y1 = 4, X2 = w2 - 4, Y2 = h - 4, Stroke = Brushes.Transparent, StrokeThickness = 8, IsHitTestVisible = false };
            dragRight.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
            this.Children.Add(dragRight);

            //SetDragHitTestVisibility(tc.Editor.KbHook.ControlPressed);
            SetDragHitTestVisibility(Keyboard.Modifiers == ModifierKeys.Control);

            dragRight.MouseEnter += (sender, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                    Mouse.OverrideCursor = Cursors.SizeWE;
            };
            dragRight.MouseLeave += (sender, e) =>
            {
                Mouse.OverrideCursor = null;
            };
            dragRight.PreviewMouseLeftButtonDown += (sender, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    this.PropertyChanged.Raise(this, "DragBottom");
                    e.Handled = true;
                }
            };


            dragLeft.MouseEnter += (sender, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                    Mouse.OverrideCursor = Cursors.SizeWE;
            };
            dragLeft.MouseLeave += (sender, e) =>
            {
                Mouse.OverrideCursor = null;
            };
            dragLeft.PreviewMouseLeftButtonDown += (sender, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    this.PropertyChanged.Raise(this, "DragTop");
                    e.Handled = true;
                }
            };

        }

        private void DragBottom_MouseEvent(object sender, MouseEventArgs e)
        {
            if (macCanvas != null)
            {
                macCanvas.RaiseEvent(e);
            }
        }

        DispatcherTimer dtAnimTimer;
        Rectangle rectPlayAnim;

        public event PropertyChangedEventHandler PropertyChanged;

        internal void StopAnimation()
        {
            if (dtAnimTimer != null)
                dtAnimTimer.Stop();

            if (rectPlayAnim != null)
                rectPlayAnim.Height = 0;
        }

        internal void PlayAnimation(bool loop)
        {
            patternAnimationStarted = false;
            looping = loop;

            rectPlayAnim.Fill = GetLGBForPatternPlay(loop);

            if (dtAnimTimer != null)
                dtAnimTimer.Stop();

            dtAnimTimer = new DispatcherTimer();
            dtAnimTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000 / 30);
            dtAnimTimer.Tick += (sender, e) =>
            {
                int span = se.Pattern != null ? se.Span : 10;
                double h = viewSettings.TrackHeight - 2;
                double w = span * viewSettings.TickWidth;

                if (tc.Sequence.PlayingPattern == se.Pattern || se.Pattern.IsPlayingSolo)
                {
                    patternAnimationStarted = true;
                    rectPlayAnim.Width = w;
                    if (se.Pattern.PlayPosition > 0)
                        rectPlayAnim.Width = (se.Pattern.PlayPosition / (double)PatternEvent.TimeBase) * viewSettings.TickWidth;
                }
                else if (patternAnimationStarted)
                {
                    patternAnimationStarted = false;
                    dtAnimTimer.Stop();
                    rectPlayAnim.Width = 0;
                }
            };

            dtAnimTimer.Start();
        }

        private LinearGradientBrush GetLGBForPatternPlay(bool loop)
        {
            LinearGradientBrush playLGB;

            if (loop)
            {
                playLGB = tc.TryFindResource("ClickPlayPatternLooping") as LinearGradientBrush;
                if (playLGB == null)
                {
                    playLGB = new LinearGradientBrush();
                    playLGB.StartPoint = new Point(0, 0);
                    playLGB.EndPoint = new Point(0, 1);

                    GradientStop transparentGS = new GradientStop();
                    transparentGS.Color = Color.FromArgb(0x01, 0x6C, 0xA8, 0xDA);
                    transparentGS.Offset = 0.0;
                    playLGB.GradientStops.Add(transparentGS);

                    transparentGS = new GradientStop();
                    transparentGS.Color = Color.FromArgb(0x1f, 0x6C, 0xA8, 0xEA);
                    transparentGS.Offset = 0.65;
                    playLGB.GradientStops.Add(transparentGS);

                    GradientStop greenGS = new GradientStop();
                    greenGS.Color = Color.FromArgb(0xef, 0x6C, 0xA8, 0xFA);
                    greenGS.Offset = 1.0;
                    playLGB.GradientStops.Add(greenGS);
                }
            }
            else
            {
                playLGB = tc.TryFindResource("ClickPlayPattern") as LinearGradientBrush;
                if (playLGB == null)
                {
                    playLGB = new LinearGradientBrush();
                    playLGB.StartPoint = new Point(0, 0);
                    playLGB.EndPoint = new Point(0, 1);

                    GradientStop transparentGS = new GradientStop();
                    transparentGS.Color = Color.FromArgb(0x01, 0x37, 0xD2, 0x12);
                    transparentGS.Offset = 0.0;
                    playLGB.GradientStops.Add(transparentGS);

                    transparentGS = new GradientStop();
                    transparentGS.Color = Color.FromArgb(0x1f, 0x37, 0xD2, 0x12);
                    transparentGS.Offset = 0.65;
                    playLGB.GradientStops.Add(transparentGS);

                    GradientStop greenGS = new GradientStop();
                    greenGS.Color = Color.FromArgb(0xef, 0x37, 0xD2, 0x12);
                    greenGS.Offset = 1.0;
                    playLGB.GradientStops.Add(greenGS);
                }
            }

            return playLGB;
        }

        internal void UpdatePatternEvents()
        {
            if (se.Pattern != null && eventHintCanvas != null)
            {
                int span = se.Pattern != null ? se.Pattern.Length : 4;
                double h = viewSettings.TrackHeight;
                double w = span * viewSettings.TickWidth;

                if (eventHintCanvas != null)
                {
                    eventHintCanvas.Children.Clear();
                    DrawPatternEvents(eventHintCanvas, se.Pattern, w, h, patternEventHintBrush, patternEventHintBrushOff, patternEventHintParam, patternEventBorder);
                }
            }
        }

        private void DrawPatternEvents(Canvas eventHintCanvas, IPattern pat, double w, double h, Brush color, Brush offColor, Brush paramColor, Brush patternEventBorder)
        {
            double lineHeight = 4;
            double lineWidth = w / (double)pat.Length;
            double marginPercent = 0.1;
            double marginTop = h * marginPercent / 2.0;
            double drawHeight = h * (1 - marginPercent);
            double drawWidth = w;
            //double previousY = double.MinValue;
            //double MinYDistance = 2;

            if (lineWidth < SequenceEditor.Settings.PatternEventWidth)
                lineWidth = SequenceEditor.Settings.PatternEventWidth;

            string toolTip;

            PatternBoxEventHintType hintType = SequenceEditor.Settings.PatternBoxEventHint;

            if (hintType == PatternBoxEventHintType.Detail)
            {
                var pes = pat.PatternEditorMachineEvents.ToArray();
                int columnCount = pes.Length;

                int i = 0;
                foreach (var pec in pes)
                {
                    foreach (PatternEvent pe in pec.Events)
                    {
                        toolTip = "";

                        Brush col = color;
                        if (pec.Parameter.Type == ParameterType.Note && pe.Value == Note.Off)
                        {
                            col = offColor;
                            toolTip = "Off";
                        }
                        else if (pec.Parameter.Type == ParameterType.Note)
                        {
                            toolTip = BuzzNote.TryToString(pe.Value);
                        }
                        else if (pec.Parameter.Type != ParameterType.Note)
                        {
                            col = paramColor;
                            toolTip = pe.Value.ToString("X");
                        }

                        int time = pe.Time;
                        double relativeX = (pe.Time / (double)PatternEvent.TimeBase) / (double)pat.Length;
                        double relativeY = i / (double)columnCount;

                        double X1 = relativeX * drawWidth - 1; 
                        double Y1 = marginTop + relativeY * drawHeight;

                        Rectangle eventRect = new Rectangle()
                        {
                            Width = lineWidth,
                            Height = lineHeight,
                            Fill = col,
                            SnapsToDevicePixels = true
                        };

                        if (SequenceEditor.Settings.EventToolTip)
                            eventRect.ToolTip = toolTip;

                        if (lineWidth > 3)
                            eventRect.Stroke = patternEventBorder;

                        Canvas.SetLeft(eventRect, X1);
                        Canvas.SetTop(eventRect, Y1);
                        eventRect.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
                        eventHintCanvas.Children.Add(eventRect);
                    }
                    i++;
                }
            }
            else if (hintType == PatternBoxEventHintType.Note)
            {
                // Count Note columns

                var pes = pat.PatternEditorMachineEvents.Where(e => e.Parameter.Type == ParameterType.Note);
                int noteCols = pes.Count();

                lineHeight = drawHeight / (double)noteCols;
                double lineWidthCenter = 0.1 * lineHeight / 2.0;
                lineHeight *= 0.9;

                int noteColNum = 0;

                foreach (var pec in pes)
                {
                    foreach (PatternEvent pe in pec.Events)
                    {
                        toolTip = "";

                        Brush col = pe.Value != Note.Off ? color : offColor;

                        if (pe.Value == Note.Off)
                        {
                            toolTip = "Off";
                        }
                        else
                        {
                            toolTip = BuzzNote.TryToString(pe.Value);
                        }

                        int time = pe.Time;
                        double relativeY = noteColNum / (double)noteCols; 
                        double relativeX = (pe.Time / (double)PatternEvent.TimeBase) / (double)pat.Length;

                        double X1 = relativeX * drawWidth - 1;
                        double Y1 = marginTop + lineWidthCenter + relativeY * drawHeight;

                        Rectangle eventRect = new Rectangle()
                        {
                            Width = lineWidth,
                            Height = lineHeight,
                            Fill = col,
                            SnapsToDevicePixels = true
                        };

                        if (SequenceEditor.Settings.EventToolTip)
                            eventRect.ToolTip = toolTip;

                        if (lineWidth > 3)
                            eventRect.Stroke = patternEventBorder;

                        Canvas.SetLeft(eventRect, X1);
                        Canvas.SetTop(eventRect, Y1);
                        eventRect.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
                        eventHintCanvas.Children.Add(eventRect);
                    }
                    noteColNum++;
                }

            }
            else if (hintType == PatternBoxEventHintType.Midi)
            {
                const int TimeBase = 960;
                var me = pat.PatternEditorMachineMIDIEvents;

                // Count Note columns
                int noteCols = pat.Machine.TrackCount;

                if (noteCols <= 0)
                    return;

                lineHeight = drawHeight / (double)noteCols;
                double lineWidthCenter = 0.1 * lineHeight / 2.0;
                lineHeight *= 0.9;

                List<Tuple<int, ChannelMessage>> events = new List<Tuple<int, ChannelMessage>>();

                for (int i = 0; i < me.Length / 2; i++)
                {
                    Tuple<int, ChannelMessage> item = new Tuple<int, ChannelMessage>(me[2 * i + 0], new ChannelMessage(me[2 * i + 1]));
                    events.Add(item);
                }

                int[] noteColumns = new int[noteCols];
                int noteColumnPos = 0;

                foreach (var eventTuple in events)
                {
                    int midiTime = eventTuple.Item1;
                    ChannelMessage val = eventTuple.Item2;
                    toolTip = "";

                    // Move noteColumnPos as far left as possible
                    for (int i = 0; i < noteCols; i++)
                        if (noteColumns[i] == 0)
                        {
                            noteColumnPos = i;
                            break;
                        }

                    Brush col = val.Command != ChannelCommand.NoteOff ? color : offColor;
                    if (val.Command != ChannelCommand.NoteOn && val.Command != ChannelCommand.NoteOff)
                        col = paramColor;

                    double relativeX = (midiTime / (double)TimeBase) / (double)pat.Length;

                    if (val.Command == ChannelCommand.NoteOn)
                    {
                        bool found = false;
                        for (int i = 0; i < noteCols; i++)
                            if (noteColumns[(noteColumnPos + i) % noteCols] == 0)
                            {
                                noteColumnPos = (noteColumnPos + i) % noteCols;
                                found = true;
                                break;
                            }

                        if (!found)
                            noteColumnPos = (noteColumnPos + 1) % noteCols;

                        noteColumns[noteColumnPos] = val.Data1;

                        toolTip = BuzzNote.TryToString(BuzzNote.FromMIDINote(val.Data1));
                    }
                    else if (val.Command == ChannelCommand.NoteOff)
                    {
                        bool found = false;
                        for (int i = 0; i < noteCols; i++)
                            if (noteColumns[i] == val.Data1)
                            {
                                noteColumnPos = i;
                                found = true;
                                noteColumns[i] = 0;
                                break;
                            }

                        if (!found) // Don't draw note off if parent was not found
                            continue;

                        toolTip = "Off";
                    }
                    else
                    {

                    }

                    double relativeY = noteColumnPos / (double)noteCols;

                    double X1 = relativeX * drawWidth - 1; 
                    double Y1 = marginTop + lineWidthCenter + relativeY * drawHeight;

                    Rectangle eventRect = new Rectangle()
                    {
                        Width = lineWidth,
                        Height = lineHeight,
                        Fill = col,
                        SnapsToDevicePixels = true
                    };

                    if (SequenceEditor.Settings.EventToolTip)
                        eventRect.ToolTip = toolTip;

                    if (lineWidth > 3)
                        eventRect.Stroke = patternEventBorder;

                    Canvas.SetLeft(eventRect, X1);
                    Canvas.SetTop(eventRect, Y1);
                    eventRect.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
                    eventHintCanvas.Children.Add(eventRect);

                    //previousY = Y1;
                    //}
                }
            }
            else if (hintType == PatternBoxEventHintType.MidiSimple)
            {
                const int TimeBase = 960;
                var me = pat.PatternEditorMachineMIDIEvents;

                toolTip = "";

                Dictionary<int, ChannelMessage> events = new Dictionary<int, ChannelMessage>();

                for (int i = 0; i < me.Length / 2; i++)
                    events[me[2 * i + 0]] = new ChannelMessage(me[2 * i + 1]);

                lineHeight = drawHeight;

                foreach (int midiTime in events.Keys)
                {
                    ChannelMessage val = events[midiTime];

                    Brush col = val.Command != ChannelCommand.NoteOff ? color : offColor;
                    if (val.Command != ChannelCommand.NoteOn && val.Command != ChannelCommand.NoteOff)
                        col = paramColor;

                    if (val.Command == ChannelCommand.NoteOn)
                    {
                        toolTip = BuzzNote.TryToString(BuzzNote.FromMIDINote(val.Data1));
                    }
                    else if (val.Command == ChannelCommand.NoteOff)
                    {
                        toolTip = "Off";
                    }

                    double relativeX = (midiTime / (double)TimeBase) / (double)pat.Length;

                    double X1 = relativeX * drawWidth - 1; 
                    double Y1 = marginTop;

                    Rectangle eventRect = new Rectangle()
                    {
                        Width = lineWidth,
                        Height = lineHeight,
                        Fill = col,
                        SnapsToDevicePixels = true
                    };

                    if (SequenceEditor.Settings.EventToolTip)
                        eventRect.ToolTip = toolTip;

                    if (lineWidth > 3)
                        eventRect.Stroke = patternEventBorder;

                    Canvas.SetLeft(eventRect, X1);
                    Canvas.SetTop(eventRect, Y1);
                    eventRect.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
                    eventHintCanvas.Children.Add(eventRect);
                }
            }
        }

        internal void Release()
        {
            SequenceEditor.Settings.PropertyChanged -= Settings_PropertyChanged;
            this.MouseRightButtonDown -= PatternElement_MouseRightButtonDown;
            tc.Editor.PropertyChanged -= Editor_PropertyChanged;

            if (this.se.Type == SequenceEventType.PlayPattern)
                this.se.Pattern.PatternChanged -= Pattern_PatternChanged;

            tc.Editor.PropertyChanged -= Editor_PropertyChanged;
            Children.Clear();
            this.tc = null;
            this.viewSettings = null;

            eventHintCanvas = null;
            macCanvas = null;
        }

        //PatternVisual childVisual;

        //protected override int VisualChildrenCount { get { return childVisual != null ? 1 : 0; } }
        //protected override Visual GetVisualChild(int index) { if (childVisual == null) throw new ArgumentOutOfRangeException(); else return childVisual; }
    }

    public enum PatternPlayMode { Stop, Play, Looping };
    public class PatternPlayEvent : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public IPattern pat;
        public PatternPlayMode mode;

        public PatternPlayEvent(IPattern p, PatternPlayMode m)
        {
            pat = p;
            mode = m;
        }
    }
}
