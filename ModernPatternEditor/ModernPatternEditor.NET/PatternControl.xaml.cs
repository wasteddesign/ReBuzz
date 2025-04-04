using BuzzGUI.Common;
using BuzzGUI.Common.Actions;
using BuzzGUI.Common.Actions.PatternActions;
using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WDE.ModernPatternEditor.Actions;
using WDE.ModernPatternEditor.ColumnRenderer;
using WDE.ModernPatternEditor.MPEStructures;

namespace WDE.ModernPatternEditor
{
    /// <summary>
    /// Interaction logic for PatternControl.xaml
    /// </summary>
    public partial class PatternControl : UserControl
    {
        const int ColumnSetGap = 4;
        const int CursorStep = 1;
        public static readonly int BUZZ_TICKS_PER_BEAT = 4;

        public static double Scale { get; set; }

        IEnumerable<ColumnRenderer.ColumnSetElement> ColumnSetElements
        {
            get
            {
                var sp = patternGrid.Children[0] as StackPanel;
                return sp.Children.Cast<ColumnRenderer.ColumnSetElement>();
            }
        }

        public int ColumnSetElementCount
        {
            get
            {
                var sp = patternGrid.Children[0] as StackPanel;
                return sp.Children.Count;
            }
        }

        ColumnRenderer.ColumnSetElement GetColumnSetElement(int index)
        {
            var sp = patternGrid.Children[0] as StackPanel;
            return (sp.Children[index] as ColumnRenderer.ColumnSetElement);
        }

        PatternVM pattern;
        internal PatternVM Pattern
        {
            get { return pattern; }
            set
            {
                if (pattern != null)
                {
                    pattern.PropertyChanged -= pattern_PropertyChanged;
                }

                pattern = value;

                if (pattern != null)
                {
                    pattern.PropertyChanged += pattern_PropertyChanged;
                }

                CreateAllElements();
                UpdateSelection();

                lastPlayPosition = int.MinValue;
                rowNumberPlayPosRectangle.Visibility = playPosRectangle.Visibility = Visibility.Collapsed;
                patternSV.ScrollToVerticalOffset(pattern != null ? pattern.ScrollPosition : 0.0);
            }
        }

        void pattern_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "ColumnSets":
                    CreateAllElements();
                    break;

                case "BeatCount":
                    UpdateCursor();
                    break;

                case "CursorPosition":
                    UpdateCursor();
                    break;

                case "Selection":
                    UpdateSelection();
                    break;

            }
        }

        public void CreateAllElements()
        {
            // release old
            if (rowNumberGrid.Children.Count > 0)
            {
                if (rowNumberGrid.Children[0] is ColumnRenderer.ColumnSetElement)
                    (rowNumberGrid.Children[0] as ColumnRenderer.ColumnSetElement).ColumnSet = null;
                rowNumberGrid.Children.Clear();
            }

            if (patternGrid.Children.Count > 0)
            {
                foreach (ColumnRenderer.ColumnSetElement e in (patternGrid.Children[0] as StackPanel).Children)
                    e.ColumnSet = null;

                patternGrid.Children.Clear();
            }

            headerSV.Content = null;

            // create new
            if (pattern != null)
            {
                rowNumberGrid.Children.Add(new ColumnRenderer.ColumnSetElement(this.pattern.DefaultRPB) { ColumnSet = pattern.RowNumberColumnSet });

                var sp = new StackPanel() { Orientation = Orientation.Horizontal };

                foreach (var cs in pattern.ColumnSets)
                    sp.Children.Add(new ColumnRenderer.ColumnSetElement(this.pattern.DefaultRPB) { ColumnSet = cs, Margin = new Thickness(0, 0, ColumnSetGap, 0) });

                patternGrid.Children.Add(sp);

                CreateHeaders();

                UpdateCursor();
            }

            this.IsEnabled = pattern != null;
            cursorElement.Visibility = pattern != null ? Visibility.Visible : Visibility.Collapsed;
            mainGrid.Visibility = pattern != null ? Visibility.Visible : Visibility.Collapsed;

        }

        List<Tuple<IParameter, Action<IParameter, int>, Knob, RoutedPropertyChangedEventHandler<double>>> knobParameters = new List<Tuple<IParameter, Action<IParameter, int>, Knob, RoutedPropertyChangedEventHandler<double>>>();

        public void ReleaseHeaders()
        {
            foreach (var p in knobParameters)
            {
                p.Item1.UnsubscribeEvents(0, p.Item2, null);        // TODO: track
                p.Item3.ValueChanged -= p.Item4;
            }

            knobParameters.Clear();
        }

        public void CreateHeaders()
        {
            ReleaseHeaders();

            var setpanel = new StackPanel() { Orientation = Orientation.Horizontal };
            var knobstyle = TryFindResource("ParameterKnobStyle") as Style;

            foreach (ColumnRenderer.ColumnSetElement _e in (patternGrid.Children[0] as StackPanel).Children)
            {
                var e = _e;
                var hlbrush = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
                var hlbrushMuted = TryFindResource("ColumnSetLabelBrush") as SolidColorBrush;

                hlbrushMuted = new SolidColorBrush(hlbrushMuted.Color);
                hlbrushMuted.Opacity = 0.3;

                var setgrid = new Grid() { Width = e.ExtentWidth + ColumnSetGap };
                setgrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                setgrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                setgrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

                var setlabel = new TextBlock() { Text = e.ColumnSet.Label, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = TryFindResource("ColumnSetLabelBrush") as Brush };
                var setvb = new Viewbox { Child = setlabel, Margin = new Thickness(2), StretchDirection = StretchDirection.DownOnly };
                var setborder = new Border { MaxWidth = e.ExtentWidth, Height = 34, Margin = new Thickness(0, 0, ColumnSetGap, 0), Child = setvb, Background = Brushes.Transparent, CornerRadius = new CornerRadius(3, 3, 3, 3) };
                setborder.MouseEnter += (sender, ea) => { setborder.Background = hlbrush; };
                setborder.MouseLeave += (sender, ea) => { setborder.Background = Brushes.Transparent; };
                setborder.MouseLeftButtonDown += (sender, ea) =>
                {
                    if (ea.ClickCount == 1)
                    {   
                        var pcs = (ParameterColumnSet)e.ColumnSet;
                        pcs.Muted = !pcs.Muted;

                        if (pcs.Muted)
                        {
                            setlabel.Foreground = hlbrushMuted;
                        }
                        else
                        {
                            setlabel.Foreground = TryFindResource("ColumnSetLabelBrush") as Brush;
                        }

                        Editor.MPEPatternsDB.SetColumnsMuteFlag(pcs);
                    }
                };

                setgrid.Children.Add(setborder);

                if (PatternEditor.Settings.ColumnLabels)
                {
                    var colpanel = new StackPanel() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, ColumnSetGap, 0) };

                    foreach (var _c in e.ColumnSet.Columns)
                    {
                        var c = _c;
                        var b = new Border() { Width = ColumnRenderer.BeatVisual.GetColumnWidth(c), Height = 80, Background = Brushes.Transparent, CornerRadius = new CornerRadius(3, 3, PatternEditor.Settings.ParameterKnobs ? 3 : 0, PatternEditor.Settings.ParameterKnobs ? 3 : 0) };
                        b.MouseEnter += (sender, ea) => { b.Background = hlbrush; };
                        b.MouseLeave += (sender, ea) => { b.Background = Brushes.Transparent; };
                        b.MouseLeftButtonDown += (sender, ea) =>
                        {
                            if (ea.ClickCount == 1) GoToColumn(c);
                            else if (ea.ClickCount == 2) SelectColumn(c);
                            else if (ea.ClickCount == 3) SelectColumnSet(e.ColumnSet);
                        };
                        var tb = new TextBlock
                        {
                            Text = c.Label,
                            FontSize = 10,
                            VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                            Margin = new Thickness(0, 0, 0, 2),
                            Foreground = TryFindResource("ColumnLabelBrush") as Brush
                        };
                        TextOptions.SetTextFormattingMode(tb, TextFormattingMode.Ideal);
                        //TextOptions.SetTextRenderingMode(tb, TextRenderingMode.Grayscale);
                        tb.LayoutTransform = new RotateTransform(-90);
                        b.Child = tb;
                        colpanel.Children.Add(b);
                    }

                    Grid.SetRow(colpanel, 1);
                    setgrid.Children.Add(colpanel);
                }

                bool anyKnobs = e.ColumnSet.Columns.Any(c =>
                {
                    if (!(c is ParameterColumn)) return false;
                    var pc = c as ParameterColumn;
                    return pc.PatternColumn.Parameter != null && pc.PatternColumn.Parameter.Flags.HasFlag(ParameterFlags.State);
                });

                if (anyKnobs && PatternEditor.Settings.ParameterKnobs)
                {
                    var knobpanel = new StackPanel() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, ColumnSetGap, 0) };

                    foreach (var _c in e.ColumnSet.Columns)
                    {
                        var pc = _c as ParameterColumn;
                        var b = new Border() { Width = ColumnRenderer.BeatVisual.GetColumnWidth(pc), Height = 17, Background = Brushes.Transparent, Padding = new Thickness(1.0) };

                        if (pc != null && pc.PatternColumn.Parameter != null && pc.PatternColumn.Parameter.Flags.HasFlag(ParameterFlags.State))
                        {
                            var param = pc.PatternColumn.Parameter;
                            var knob = new Knob() { Style = knobstyle, Minimum = param.MinValue, Maximum = param.MaxValue };

                            var tttb = new TextBlock() { Margin = new Thickness(4, 0, 4, 0), Foreground=Brushes.Black };
                            var ttb = new Border() { Child = tttb, Background = Brushes.LightYellow, BorderBrush = Brushes.Black, BorderThickness = new Thickness(1.0), Height = 18 };
                            var popup = new Popup() { Child = ttb, PlacementTarget = knob, Placement = PlacementMode.Right, HorizontalOffset = 4, VerticalOffset = -1 };

                            knob.MouseEnter += (sender, ea) => { popup.IsOpen = true; };
                            knob.MouseLeave += (sender, ea) => { popup.IsOpen = false; };

                            knob.MouseLeftButtonDown += (sender, ea) =>
                            {
                                if (ea.ClickCount == 2)
                                    knob.Value = param.DefValue;
                            };

                            int lastvalue = -1;

                            Action<IParameter, int> pvc = (sender, track) =>
                            {
                                if (!knob.Dragging)
                                {
                                    var v = param.GetValue(pc.PatternColumn.Track);
                                    if (v != lastvalue)
                                    {
                                        lastvalue = v;
                                        knob.Value = v;
                                        tttb.Text = param.Name + ": " + param.GetValueDescriptionWithHexValue(v);
                                    }
                                }
                            };

                            pvc(param, pc.PatternColumn.Track); // execute once to initialize
                            param.SubscribeEvents(pc.PatternColumn.Track, pvc, null);

                            RoutedPropertyChangedEventHandler<double> kvc = (sender, rpcea) =>
                            {
                                var oldvalue = param.GetValue(pc.PatternColumn.Track);
                                var newvalue = (int)Math.Round(rpcea.NewValue);
                                if (newvalue != oldvalue)
                                {
                                    param.SetValue(pc.PatternColumn.Track, newvalue);
                                    if (param.Group.Machine.DLL.Info.Version >= 42) param.Group.Machine.SendControlChanges();
                                    tttb.Text = param.Name + ": " + param.GetValueDescriptionWithHexValue(newvalue);
                                }
                            };
                            knob.ValueChanged += kvc;

                            knobParameters.Add(Tuple.Create(param, pvc, knob, kvc));

                            b.Child = knob;
                        }

                        knobpanel.Children.Add(b);
                    }

                    Grid.SetRow(knobpanel, 2);
                    setgrid.Children.Add(knobpanel);

                }

                setpanel.Children.Add(setgrid);
            }

            headerSV.Content = setpanel;
        }

        public ICommand PropertiesCommand { get; private set; }
        public ICommand AboutCommand { get; private set; }
        public ICommand SettingsCommand { get; private set; }
        public ICommand CutCommand { get; private set; }
        public ICommand CopyCommand { get; private set; }
        public ICommand PasteCommand { get; private set; }
        public ICommand UndoCommand { get; private set; }
        public ICommand RedoCommand { get; private set; }
        public ICommand SelectAllCommand { get; private set; }
        public ICommand UnselectCommand { get; private set; }
        public ICommand RandomizeCommand { get; private set; }

        public ICommand DecreaseCommand { get; private set; }
        public ICommand IncreaseCommand { get; private set; }
        public ICommand RotateCommandDown { get; private set; }
        public ICommand RotateCommandUp { get; private set; }

        public ICommand WriteStateCommand { get; private set; }
        public ICommand InterpolateLinearCommand { get; private set; }
        public ICommand InterpolateExpCommand { get; private set; }


        public PatternEditor Editor { get; set; }

        static readonly Key[] HexKeys = { Key.D0, Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D6, Key.D7, Key.D8, Key.D9, Key.A, Key.B, Key.C, Key.D, Key.E, Key.F };
        static readonly Key[] DecKeys = { Key.D0, Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D6, Key.D7, Key.D8, Key.D9 };

        public PatternControl()
        {
            Scale = 1.0;
            InitializeComponent();
            contextMenu.DataContext = this;

            this.DataContextChanged += (sender, e) => { Pattern = e.NewValue as PatternVM; };

            patternSV.ScrollChanged += (sender, e) =>
            {
                if (e.VerticalChange != 0)
                {
                    rowNumberSV.ScrollToVerticalOffset(e.VerticalOffset);
                    if (pattern != null) pattern.ScrollPosition = e.VerticalOffset;
                }
                if (e.HorizontalChange != 0) headerSV.ScrollToHorizontalOffset(e.HorizontalOffset);

                if (e.ViewportHeightChange != 0)
                {
                    UpdateScrollMargins();
                }
            };

            rowNumberSV.PreviewMouseWheel += (sender, e) =>
            {
                e.Handled = true;
            };

            int mouseWheelAcc = 0;

            patternSV.PreviewMouseWheel += (sender, e) =>
            {
                if (pattern?.ColumnSets.Count == 0)
                    return;

                if (PatternEditor.Settings.CursorScrollMode != CursorScrollMode.Standard)
                {
                    mouseWheelAcc += e.Delta;

                    if (mouseWheelAcc >= 120)
                    {
                        mouseWheelAcc -= 120;
                        MoveCursorDelta(0, -4);
                    }
                    else if (e.Delta <= -120)
                    {
                        mouseWheelAcc += 120;
                        MoveCursorDelta(0, 4);
                    }

                    e.Handled = true;
                }
            };

            this.GotKeyboardFocus += (sender, e) =>
            {
                cursorElement.IsActive = true;
                e.Handled = true;
            };

            this.LostKeyboardFocus += (sender, e) =>
            {
                cursorElement.IsActive = false;
                e.Handled = true;
            };

            this.PreviewKeyDown += (sender, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    switch (e.Key)
                    {
                        case Key.Left: e.Handled = true; break;
                        case Key.Right: e.Handled = true; break;
                    }
                }
                if (pattern?.ColumnSets.Count == 0)
                return;

                if (Keyboard.Modifiers == ModifierKeys.None)
                {
                    switch (e.Key)
                    {
                        case Key.Down: MoveCursorDelta(0, CursorStep); e.Handled = true; break;
                        case Key.Up: MoveCursorDelta(0, -CursorStep); e.Handled = true; break;
                        case Key.Right: MoveCursorDelta(1, 0); e.Handled = true; break;
                        case Key.Left: MoveCursorDelta(-1, 0); e.Handled = true; break;
                        case Key.PageDown: MoveCursorDelta(0, 16); e.Handled = true; break;
                        case Key.PageUp: MoveCursorDelta(0, -16); e.Handled = true; break;
                        case Key.Home: Home(); e.Handled = true; break;
                        case Key.End: End(); e.Handled = true; break;
                        case Key.Tab: Tab(); e.Handled = true; break;
                        case Key.Delete: InsertOrDelete(true, true); e.Handled = true; break;
                        case Key.Insert: InsertOrDelete(true, false); e.Handled = true; break;
                    }

                    var col = pattern.GetColumn(pattern.CursorPosition);
                    if (col is ParameterColumn)
                    {
                        IEnumerable<BuzzAction> action = null;
                        bool movecursor = false;
                        bool play = false;

                        var pc = (ParameterColumn)col;

                        if (pc.Type == ColumnRenderer.ColumnType.Note)
                        {
                            if (pattern.CursorPosition.Index == 0)
                            {
                                int scancode = Win32.GetScanCode(e);
                                var map = Editor.SelectedKeyboardMapping;
                                int i = map.GetNoteByScancode(scancode);

                                if (i >= 0)
                                {
                                    //action = EditNote(pc, BuzzNote.FromMIDINote(Math.Min(i + Editor.SelectedRootNote + 12 * pattern.Pattern.Machine.BaseOctave, BuzzNote.ToMIDINote(BuzzNote.Max))));
                                    action = EditNote(pc, BuzzNote.FromMIDINote(Math.Min(i + Editor.SelectedRootNote + 12 * Editor.SelectedMachine.BaseOctave, BuzzNote.ToMIDINote(BuzzNote.Max))));
                                    movecursor = true;
                                    play = true;
                                }
                                else if (map.IsOffScancode(scancode))
                                {
                                    action = EditNote(pc, BuzzNote.Off);
                                    movecursor = true;
                                    play = true;
                                }
                                else if (map.IsPlayScancode(scancode))
                                {
                                    PlayColumnSet(pattern.CursorPosition);
                                    movecursor = true;
                                }
                                else if (map.IsPlayAllScancode(scancode))
                                {
                                    PlayAllColumnSets(pattern.CursorPosition);
                                    movecursor = true;
                                }

                            }
                            else
                            {
                                int i = Array.IndexOf(DecKeys, e.Key);
                                if (i >= 0)
                                {
                                    action = pc.EditDigit(pattern.CursorPosition, i);
                                    movecursor = true;
                                }
                            }

                        }
                        else if (col.Type == ColumnRenderer.ColumnType.HexValue)
                        {
                            int i = Array.IndexOf(HexKeys, e.Key);
                            if (i >= 0)
                            {
                                action = pc.EditDigit(pattern.CursorPosition, i);
                                movecursor = true;
                            }
                        }

                        if (e.Key == Key.OemPeriod)
                        {
                            if (pc.Type == ColumnRenderer.ColumnType.Note)
                                action = EditNote(pc, -1);
                            else
                                action = pc.EditDigit(pattern.CursorPosition, -1);

                            movecursor = true;
                        }

                        if (action != null)
                        {
                            var oldCursorPos = pattern.CursorPosition;

                            if (movecursor)
                                DoEditActions(action, e.Key == Key.OemPeriod);
                            else
                                DoActions(action);

                            if (Editor.PlayNotes && play)
                            {
                                PlayColumnSet(oldCursorPos);
                            }

                        }
                        else
                        {
                            if (movecursor)
                                MoveCursorDelta(Editor.SelectedStepsRight, Editor.SelectedStepsDown);
                        }

                    }

                }
                else if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    switch (e.Key)
                    {
                        case Key.Down: MoveCursorDelta(0, CursorStep, true); e.Handled = true; break;
                        case Key.Up: MoveCursorDelta(0, -CursorStep, true); e.Handled = true; break;
                        case Key.Right: MoveCursorDelta(1, 0, true); e.Handled = true; break;
                        case Key.Left: MoveCursorDelta(-1, 0, true); e.Handled = true; break;
                        case Key.PageDown: MoveCursorDelta(0, 16, true); e.Handled = true; break;
                        case Key.PageUp: MoveCursorDelta(0, -16, true); e.Handled = true; break;
                        case Key.Home: Home(true); e.Handled = true; break;
                        case Key.End: End(true); e.Handled = true; break;
                        case Key.Tab: ShiftTab(); e.Handled = true; break;
                        case Key.Add: ShiftValues(1); e.Handled = true; break;
                        case Key.Subtract: ShiftValues(-1); e.Handled = true; break;
                    }
                }
                else if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (e.Key >= Key.D0 && e.Key <= Key.D9)
                    {
                        int n = e.Key - Key.D0;
                        if (n == 0) n = 10;
                        SetBeatSubdivision(n);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.R)
                    {
                        RamdomizeSelection();
                        e.Handled = true;
                    }
                    else if (e.Key == Key.I)
                    {
                        InterpolateSelection(InterpolationMethod.Linear);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.W)
                    {
                        RollSelection(false);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.T)
                    {
                        WriteState();
                        e.Handled = true;
                    }
                }
                else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
                {
                    if (e.Key >= Key.D0 && e.Key <= Key.D9)
                    {
                        int n = e.Key - Key.D0;
                        if (n == 0) n = 10;
                        SetBeatSubdivision(10 + n);
                    }
                    else if (e.Key == Key.Delete)
                    {
                        InsertOrDelete(false, true);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Insert)
                    {
                        InsertOrDelete(false, false);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.I)
                    {
                        InterpolateSelection(InterpolationMethod.ExpIntp);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.W)
                    {
                        RollSelection(true);
                        e.Handled = true;
                    }
                }
            };

            this.PreviewTextInput += (sender, e) =>
            {
                if (e.Text.Length == 1 && e.Text[0] == '<')
                {
                    Editor.SelectedWaveIndex--;
                }
                else if (e.Text.Length == 1 && e.Text[0] == '>')
                {
                    Editor.SelectedWaveIndex++;
                }
                else if (e.Text.Length == 1 && e.Text[0] != '.' && (int)e.Text[0] >= 32 && (int)e.Text[0] < 128)
                {
                    char ch = e.Text[0];

                    var col = pattern.GetColumn(pattern.CursorPosition);
                    if (col is ParameterColumn && col.Type == ColumnRenderer.ColumnType.Ascii)
                    {
                        var pc = (ParameterColumn)col;

                        bool valid = pc.PatternColumn.Parameter.IsValidAsciiChar((int)ch);

                        if (!valid && ch >= 'a' && ch <= 'z')
                        {
                            ch = (char)(ch + 'A' - 'a');
                            valid = pc.PatternColumn.Parameter.IsValidAsciiChar((int)ch);
                        }
                        else if (!valid && ch >= 'A' && ch <= 'Z')
                        {
                            ch = (char)(ch + 'a' - 'A');
                            valid = pc.PatternColumn.Parameter.IsValidAsciiChar((int)ch);
                        }

                        if (valid)
                        {
                            var action = pc.EditDigit(pattern.CursorPosition, (int)ch);
                            DoEditActions(action, false);
                        }
                    }
                }
            };

            DecreaseCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => pattern.ColumnSets.Count > 0,
                ExecuteDelegate = x => { ShiftValues(-1); }
            };

            IncreaseCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => pattern.ColumnSets.Count > 0,
                ExecuteDelegate = x => { ShiftValues(1); }
            };

            WriteStateCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => pattern.ColumnSets.Count > 0,
                ExecuteDelegate = x => { WriteState(); }
            };

            InterpolateLinearCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => pattern.ColumnSets.Count > 0,
                ExecuteDelegate = x => { InterpolateSelection(InterpolationMethod.Linear); }
            };

            InterpolateExpCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => pattern.ColumnSets.Count > 0,
                ExecuteDelegate = x => { InterpolateSelection(InterpolationMethod.ExpIntp); }
            };

            RotateCommandDown = new SimpleCommand
            {
                CanExecuteDelegate = x => pattern.ColumnSets.Count > 0,
                ExecuteDelegate = x => { RollSelection(true); }
            };


            RotateCommandUp = new SimpleCommand
            {
                CanExecuteDelegate = x => pattern.ColumnSets.Count > 0,
                ExecuteDelegate = x => { RollSelection(true); }
            };

            RandomizeCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => pattern.ColumnSets.Count > 0,
                ExecuteDelegate = x => { RamdomizeSelection(); }
            };

            SettingsCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    SettingsWindow.Show(this, "Modern Pattern Editor");
                }
                //ExecuteDelegate = x => { SettingsWindow.Show(Editor, "Modern Pattern Editor"); }
            };

            AboutCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    MessageBox.Show("Modern Pattern Editor " + Editor.Version + ".\n\nBased on \"Ctrl+Q\" editor by Oskari Tammelin.\n\n(C) 2022 WDE", "Modern Pattern Editor", MessageBoxButton.OK);
                }
            };

            PropertiesCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    Editor.ShowPatternProperties();
                }
            };

            CutCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => pattern != null && pattern.Selection.Active,
                ExecuteDelegate = x =>
                {
                    Selection selection = pattern.Selection;
                    if (!selection.Active)
                        selection = Selection.Start(pattern.CursorPosition).SetEnd(pattern.CursorPosition);

                    DoAction(new CutOrCopyPatternEventsAction(Editor.MPEPatternsDB.GetMPEPattern(pattern.Pattern), selection, PatternEditor.clipboard, true));
                }
            };

            CopyCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => pattern != null && pattern.Selection.Active,
                ExecuteDelegate = x =>
                {
                    Selection selection = pattern.Selection;
                    if (!selection.Active)
                        selection = Selection.Start(pattern.CursorPosition).SetEnd(pattern.CursorPosition);
                    DoAction(new CutOrCopyPatternEventsAction(Editor.MPEPatternsDB.GetMPEPattern(pattern.Pattern), selection, PatternEditor.clipboard, false));
                }
            };

            PasteCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => false,
                ExecuteDelegate = x =>
                {
                    Selection selection = pattern.Selection;
                    if (!selection.Active)
                        selection = Selection.Start(pattern.CursorPosition).SetEnd(pattern.CursorPosition);
                    DoAction(new PastePatternEventsAction(Editor.MPEPatternsDB.GetMPEPattern(pattern.Pattern), selection, PatternEditor.clipboard));
                }
            };

            UndoCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => Editor.EditContext.ActionStack.CanUndo,
                ExecuteDelegate = x => Editor.EditContext.ActionStack.Undo()
            };

            RedoCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => Editor.EditContext.ActionStack.CanRedo,
                ExecuteDelegate = x => Editor.EditContext.ActionStack.Redo()
            };

            SelectAllCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    if (pattern?.ColumnSets.Count == 0)
                        return;

                    if (!pattern.Selection.Equals(Selection.ColumnSet(pattern.CursorPosition)))
                        pattern.Selection = Selection.ColumnSet(pattern.CursorPosition);
                    else
                        pattern.Selection = Selection.All(pattern);
                }
            };

            UnselectCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => pattern != null && pattern.Selection.Active,
                ExecuteDelegate = x => pattern.Selection = Selection.Empty(pattern)
            };

            this.InputBindings.Add(new InputBinding(CutCommand, new KeyGesture(Key.X, ModifierKeys.Control)));
            this.InputBindings.Add(new InputBinding(CopyCommand, new KeyGesture(Key.C, ModifierKeys.Control)));
            this.InputBindings.Add(new InputBinding(PasteCommand, new KeyGesture(Key.V, ModifierKeys.Control)));
            this.InputBindings.Add(new InputBinding(UndoCommand, new KeyGesture(Key.Z, ModifierKeys.Control)));
            this.InputBindings.Add(new InputBinding(RedoCommand, new KeyGesture(Key.Y, ModifierKeys.Control)));
            this.InputBindings.Add(new InputBinding(SelectAllCommand, new KeyGesture(Key.A, ModifierKeys.Control)));
            this.InputBindings.Add(new InputBinding(UnselectCommand, new KeyGesture(Key.U, ModifierKeys.Control)));

            bool enableDragSel = false;

            new Dragger
            {
                Element = patternGrid,
                Gesture = new DragMouseGesture { Button = MouseButton.Left },
                Mode = DraggerMode.Absolute,
                BeginDrag = (p, alt, cc) =>
                {
                    if (pattern?.ColumnSets.Count == 0)
                        return;

                    this.Focus();
                    enableDragSel = true;

                    if (PatternEditor.Settings.CursorScrollMode == CursorScrollMode.Standard)
                    {
                        GoToPoint(p);

                        pattern.Selection = Selection.Start(GetDigitAtPoint(p));

                        if (cc == 2)
                            pattern.Selection = Selection.ColumnSetRow(pattern.CursorPosition);
                        else if (cc >= 3)
                            pattern.Selection = Selection.ColumnSetBeat(pattern.CursorPosition);
                    }
                    else
                    {
                        if (cc == 1)
                        {
                            pattern.Selection = Selection.Start(GetDigitAtPoint(p));
                        }
                        else if (cc == 2)
                        {
                            GoToPoint(p);
                            enableDragSel = false;
                        }

                    }

                },
                Drag = p =>
                {
                    if (enableDragSel)
                    {
                        pattern.Selection = pattern.Selection.SetEnd(GetDigitAtPoint(p));
                    }

                    patternGrid.BringIntoView(new Rect(p, p));
                },
                EndDrag = p =>
                {
                }
            };

            new Dragger
            {
                Element = rowNumberGrid,
                Gesture = new DragMouseGesture { Button = MouseButton.Left },
                Mode = DraggerMode.Absolute,
                BeginDrag = (p, alt, cc) =>
                {
                    if (pattern?.ColumnSets.Count == 0)
                        return;

                    this.Focus();

                    var d = GetDigitAtPoint(p);

                    if (cc == 1)
                    {
                        pattern.CursorPosition = pattern.CursorPosition
                            .SetBeat(d.Beat)
                            .SetRowInBeat(d.RowInBeat)
                            .Constrained;

                        UpdateCursor();
                        enableDragSel = false;
                    }
                    else if (cc == 2)
                    {
                        SelectRow(d);
                        enableDragSel = true;
                    }
                    else if (cc == 3)
                    {
                        SelectAllColumnsOfBeat(d);
                        enableDragSel = true;
                    }
                },
                Drag = p =>
                {
                    if (enableDragSel)
                    {
                        pattern.Selection = pattern.Selection.SetEnd(GetDigitAtPoint(p));
                    }

                    patternGrid.BringIntoView(new Rect(p, p));

                },
                EndDrag = p =>
                {
                }
            };

            this.MouseLeftButtonDown += (sender, e) =>
            {
                Focus();
            };

        }

        private void WriteState()
        {
            Selection s = this.pattern.Selection;

            if (!s.Active)
                s = Selection.Start(pattern.CursorPosition).SetEnd(pattern.CursorPosition);

            Digit columnIterator = s.Bounds.Item1;
            Digit lastColumn = s.Bounds.Item2;

            while (true)
            {
                var digitIterator = columnIterator;
                while (true)
                {
                    var column = digitIterator.ParameterColumn;
                    var par = column.PatternColumn.Parameter;
                    int value = par.GetValue(column.PatternColumn.Track);
                    int time = digitIterator.TimeInBeat + digitIterator.Beat * BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase;
                    MPEPattern mpePattern = Editor.MPEPatternsDB.GetMPEPattern(Pattern.Pattern);
                    MPEPatternColumn mpeColumn = mpePattern.GetColumn(column.PatternColumn);
                    DoAction(new MPESetOrClearEventsAction(Pattern.Pattern, mpeColumn, new[] { new PatternEvent(time, value) }, true));

                    if (digitIterator.Beat * BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase + digitIterator.TimeInBeat >=
                        lastColumn.Beat * BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase + lastColumn.TimeInBeat)
                        break;

                    digitIterator = digitIterator.Down;
                }
                if (columnIterator.Column == lastColumn.Column && columnIterator.ColumnSet == lastColumn.ColumnSet)
                    break;
                columnIterator = columnIterator.Right;
            }
        }

        private void RollSelection(bool down)
        {
            Selection s = this.pattern.Selection;

            if (!s.Active || s.Bounds.Item1.ParameterColumn.GetDigitTime(s.Bounds.Item1) >= s.Bounds.Item2.ParameterColumn.GetDigitTime(s.Bounds.Item2))
                return;

            MPEPattern mpePattern = Editor.MPEPatternsDB.GetMPEPattern(Pattern.Pattern);
            DoAction(new MPERotateAction(mpePattern, s, down));
        }

        private void ShiftValues(int delta)
        {
            Selection s = this.pattern.Selection;

            if (!s.Active)
                s = Selection.Start(pattern.CursorPosition).SetEnd(pattern.CursorPosition);

            MPEPattern mpePattern = Editor.MPEPatternsDB.GetMPEPattern(Pattern.Pattern);
            DoAction(new MPEUpDownAction(mpePattern, s, delta));
        }

        private void RamdomizeSelection()
        {
            Selection s = this.pattern.Selection;

            if (!s.Active)
                s = Selection.Start(pattern.CursorPosition).SetEnd(pattern.CursorPosition);

            var rnd = new Random();

            using (new ActionGroup(Editor.EditContext.ActionStack))
            {
                var columnIterator = s.Bounds.Item1;

                var lastColumn = s.Bounds.Item2;
                while (true)
                {
                    var digitIterator = columnIterator;
                    while (true)
                    {
                        var column = digitIterator.ParameterColumn;
                        var par = column.PatternColumn.Parameter;
                        int value = rnd.Next(par.MinValue, par.MaxValue + 1);
                        int time = digitIterator.TimeInBeat + digitIterator.Beat * BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase;
                        MPEPattern mpePattern = Editor.MPEPatternsDB.GetMPEPattern(Pattern.Pattern);
                        MPEPatternColumn mpeColumn = mpePattern.GetColumn(column.PatternColumn);
                        DoAction(new MPESetOrClearEventsAction(Pattern.Pattern, mpeColumn, new[] { new PatternEvent(time, value) }, true));

                        if (digitIterator.Beat * BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase + digitIterator.TimeInBeat >=
                            lastColumn.Beat * BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase + lastColumn.TimeInBeat)
                            break;
                        digitIterator = digitIterator.Down;
                    }
                    if (columnIterator.Column == lastColumn.Column && columnIterator.ColumnSet == lastColumn.ColumnSet)
                        break;
                    columnIterator = columnIterator.Right;
                }
            }
        }

        enum InterpolationMethod
        {
            Linear,
            ExpIntp
        }

        double ExpIntp(double x, double y, double a)
        {

            double lx = Math.Log(x);
            double ly = Math.Log(y);
            return Math.Exp(lx + a * (ly - lx));
        }

        double Interpolate(double a, double v1, double v2, bool expintp)
        {
            if (expintp && ((v1 > 0 && v2 > 0) || (v1 < 0 && v2 < 0)))
            {
                return (ExpIntp(v1, v2, a) - v1) / (v2 - v1);
            }
            else
            {
                return a;
            }
        }
        private void InterpolateSelection(InterpolationMethod method)
        {
            Selection s = this.pattern.Selection;

            if (!s.Active)
                return;

            using (new ActionGroup(Editor.EditContext.ActionStack))
            {
                var columnIterator = s.Bounds.Item1;
                var lastColumn = s.Bounds.Item2;

                if (columnIterator.Beat == lastColumn.Beat && columnIterator.RowInBeat == lastColumn.RowInBeat)
                    return;

                while (true)
                {
                    var digitIterator = columnIterator;
                    var column = digitIterator.ParameterColumn;
                    var firstDigit = digitIterator.NearestRow(columnIterator.TimeInBeat);
                    var lastDigit = new Digit(firstDigit.PatternVM, firstDigit.ColumnSet, firstDigit.Column, lastColumn.Beat, lastColumn.RowInBeat, lastColumn.Index).NearestRow(lastColumn.TimeInBeat);
                    int firstValue = column.FetchBeat(firstDigit.Beat).Rows[firstDigit.RowInBeat].Value;
                    int lastValue = column.FetchBeat(lastDigit.Beat).Rows[lastDigit.RowInBeat].Value;
                    int firstDigitTime = column.GetDigitTime(firstDigit);
                    int lastDigitTime = column.GetDigitTime(lastDigit);

                    digitIterator = digitIterator.Down; // Start with next

                    if (firstValue != -1 && lastValue != -1)
                    {
                        while (true)
                        {
                            if (method == InterpolationMethod.ExpIntp && (firstValue == 0 || lastValue == 0))
                                break;

                            if (digitIterator.Beat * BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase + digitIterator.TimeInBeat >=
                                lastDigit.Beat * BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase + lastDigit.TimeInBeat)
                                break;

                            int time = digitIterator.TimeInBeat + digitIterator.Beat * BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase;
                            double a = (time - firstDigitTime) / (double)(lastDigitTime - firstDigitTime);
                            double iValue = Interpolate(a, firstValue, lastValue, method == InterpolationMethod.ExpIntp);
                            int value = (int)(firstValue + ((lastValue - firstValue) + 1) * iValue);

                            MPEPattern mpePattern = column.Set.Pattern.Editor.MPEPatternsDB.GetMPEPattern(column.Set.Pattern.Pattern);
                            MPEPatternColumn mpeColumn = mpePattern.GetColumn(column.PatternColumn);
                            DoAction(new MPESetOrClearEventsAction(Pattern.Pattern, mpeColumn, new[] { new PatternEvent(time, value) }, true));

                            digitIterator = digitIterator.Down;
                        }
                    }
                    if (columnIterator.Column == lastColumn.Column && columnIterator.ColumnSet == lastColumn.ColumnSet)
                        break;
                    columnIterator = columnIterator.Right;
                }
            }
        }

        private void InsertOrDelete(bool row, bool delete)
        {
            var cursorPos = pattern.CursorPosition;
            if (!row)
            {
                DoAction(new MPEInsertOrDeleteAction(pattern.Editor.MPEPatternsDB.GetMPEPattern(pattern.Pattern), cursorPos, delete));
            }
            else
            {
                using (new ActionGroup(Editor.EditContext.ActionStack))
                {
                    var fisrtColumn = cursorPos.FirstColumn.NearestRow(cursorPos.TimeInBeat);
                    int lastColumn = cursorPos.LastColumn.Column;
                    while (true)
                    {
                        DoAction(new MPEInsertOrDeleteAction(pattern.Editor.MPEPatternsDB.GetMPEPattern(pattern.Pattern), fisrtColumn, delete));
                        if (fisrtColumn.Column >= lastColumn)
                            break;
                        fisrtColumn = fisrtColumn.NextColumn.NearestRow(cursorPos.TimeInBeat);
                    }
                }
            }
        }

        void SetBeatSubdivision(int n)
        {
            var cb = pattern.Selection.ColumnsAndBeats;
            if (!cb.Any()) cb = LinqExtensions.Return(pattern.CursorPosition.ColumnAndBeat);

            using (new ActionGroup(Editor.EditContext.ActionStack))
            {
                // Include MPEPattern?
                DoAction(new MPESetBeatSubdivisionAction(pattern.Editor.MPEPatternsDB.GetMPEPattern(pattern.Pattern), cb.Select(i => Tuple.Create(i, n))));
                var sel = pattern.Selection.SetEnd(pattern.Selection.Bounds.Item2);
                if (!sel.Active)
                    sel = Selection.Start(pattern.CursorPosition).SetEnd(pattern.CursorPosition);
                DoAction(new MPEQuantizeBeatsAction(pattern.Editor.MPEPatternsDB.GetMPEPattern(pattern.Pattern), sel));
            }
        }

        internal void InsertChord(int[] buzzNotes, int[] steppings)
        {
            Selection s = this.pattern.Selection;
            bool once = false;
            if (!s.Active)
            {
                Digit lastDigit = pattern.CursorPosition.LastColumnSet.LastColumn.LastBeat.LastRowInBeat;
                /*
					new Digit(
					pattern, pattern.ColumnSets.Count - 1,
					pattern.ColumnSets.Last().Columns.Count - 1,
					pattern.ColumnSets.Last().Columns.Last().DigitCount,
					pattern.ColumnSets.Last().Columns.Last().FetchBeat(pattern.ColumnSets.Last().Columns.Last().DigitCount).Rows.Count - 1,
					0);
				*/
                s = Selection.Start(pattern.CursorPosition).SetEnd(lastDigit);
                once = true;
            }

            Digit columnIterator = s.Bounds.Item1;
            Digit lastColumn = s.Bounds.Item2;

            // Find first track and note column
            while (columnIterator.ParameterColumn.Type != ColumnRenderer.ColumnType.Note || columnIterator.ParameterColumn.PatternColumn.Parameter.Group.Type != ParameterGroupType.Track)
            {
                if (columnIterator.Column == lastColumn.Column && columnIterator.ColumnSet == lastColumn.ColumnSet)
                    return; // No proper note column found

                columnIterator = columnIterator.Right;
            }

            var digitIterator = columnIterator;
            var firstChordDigit = columnIterator;
            int chordIndex = 0;
            int stepCount = 0;

            using (new ActionGroup(Editor.EditContext.ActionStack))
            {
                while (true)
                {
                    var column = digitIterator.ParameterColumn;
                    var par = column.PatternColumn.Parameter;
                    if (par.Type == ParameterType.Note)
                    {
                        int value = buzzNotes[chordIndex % buzzNotes.Length];
                        int time = digitIterator.TimeInBeat + digitIterator.Beat * BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase;
                        MPEPattern mpePattern = Editor.MPEPatternsDB.GetMPEPattern(Pattern.Pattern);
                        MPEPatternColumn mpeColumn = mpePattern.GetColumn(column.PatternColumn);
                        DoAction(new MPESetOrClearEventsAction(Pattern.Pattern, mpeColumn, new[] { new PatternEvent(time, value) }, true));
                    }

                    chordIndex++;
                    stepCount++;
                    int nextStepping = steppings[stepCount % steppings.Length];

                    if (digitIterator.ColumnSet == lastColumn.ColumnSet)
                    {
                        if (firstChordDigit.Beat == digitIterator.Beat && firstChordDigit.RowInBeat == digitIterator.RowInBeat)
                        {
                            if (once)
                                break;
                            digitIterator = digitIterator.Down;
                            firstChordDigit = digitIterator;
                            chordIndex = 0;
                        }

                        digitIterator = digitIterator.SetColumn(columnIterator.Column).SetColumnSet(columnIterator.ColumnSet);
                    }
                    else
                    {
                        digitIterator = digitIterator.NextColumnSet;
                    }

                    /*
					if (chordIndex >= buzzNotes.Count())
					{
						chordIndex = 0;

						if (nextStepping == 0)
							digitIterator = digitIterator.Down;

						digitIterator = digitIterator.SetColumn(columnIterator.Column).SetColumnSet(columnIterator.ColumnSet);
					}
					*/
                    if (once && chordIndex >= buzzNotes.Length)
                        break;

                    else if (digitIterator.Beat * BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase + digitIterator.TimeInBeat >=
                        lastColumn.Beat * BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase + lastColumn.TimeInBeat)
                        break;

                    digitIterator = digitIterator.Offset(0, nextStepping);
                }
            }
        }

        IEnumerable<BuzzAction> EditNote(ParameterColumn pc, int value)
        {
            var action = pc.EditDigit(pattern.CursorPosition, value);

            var nextColumnPos = pattern.CursorPosition.NextColumn;
            var npc = pattern.GetColumn(nextColumnPos) as ParameterColumn;
            var beat = pc.FetchBeat(pattern.CursorPosition.Beat);
            var nextBeat = npc.FetchBeat(nextColumnPos.Beat);

            if (npc.PatternColumn.Parameter.Flags.HasFlag(ParameterFlags.Wave) && beat.Rows.Count == nextBeat.Rows.Count)
            {
                if (value == BuzzNote.Off || value < 0)
                    return action.Concat(npc.EditValue(nextColumnPos, -1));
                else
                    return action.Concat(npc.EditValue(nextColumnPos, Editor.SelectedWave != null ? Editor.SelectedWave.Index + 1 : 1)); // Mtk crashes if selected wave == 0
            }
            else
            {
                return action;
            }
        }

        public void Release()
        {
            ReleaseHeaders();
        }

        public void UpdateScrollMargins()
        {
            if (PatternEditor.Settings.CursorScrollMode == CursorScrollMode.CenterWithMargins)
            {
                var m = Math.Floor(patternSV.ViewportHeight / 2);
                rowNumberSVGrid.Margin = patternSVGrid.Margin = new Thickness(0, m, 0, patternSV.ViewportHeight - m);
            }
            else
            {
                rowNumberSVGrid.Margin = patternSVGrid.Margin = new Thickness(0.0);
            }
        }


        Rect GetDigitRect(Digit d)
        {
            var cse = GetColumnSetElement(d.ColumnSet);
            var r = cse.GetDigitRect(d);
            r.Offset(ColumnSetElements.Take(d.ColumnSet).Select(cs => cs.ExtentWidth + ColumnSetGap).Sum(), 0);
            return r;
        }

        Rect GetFieldRect(Digit d)
        {
            var cse = GetColumnSetElement(d.ColumnSet);
            var r = cse.GetFieldRect(d);
            r.Offset(ColumnSetElements.Take(d.ColumnSet).Select(cs => cs.ExtentWidth + ColumnSetGap).Sum(), 0);
            return r;
        }

        Digit GetDigitAtPoint(Point p)
        {
            int cs = Math.Min(ColumnSetElements.FindIndex(0.0, (w, e) => w + e.ExtentWidth + ColumnSetGap, w => p.X < w), ColumnSetElementCount - 1);
            p.X -= ColumnSetElements.Take(cs).Sum(e => e.ExtentWidth + ColumnSetGap);
            var d = GetColumnSetElement(cs).GetDigitAtPoint(pattern, p);
            return d.SetColumnSet(cs);
        }


        void GoToPoint(Point p)
        {
            MoveCursor(GetDigitAtPoint(p));
        }

        public void UpdateCursor()
        {
            if (pattern == null || pattern.ColumnSets.Count == 0) return;

            var p = Pattern.CursorPosition;
            var r = GetDigitRect(p);
            r.Inflate(1, 0);    // make cursor a little wider
            var col = pattern.GetColumn(p);
            if (col.Type == ColumnRenderer.ColumnType.Note && p.Index == 0)
            {
                r = GetFieldRect(p);
                r.Inflate(-1, 0);
            }

            cursorElement.Rect = r;

            MakeFieldVisible(p);

            if (PatternEditor.Settings.CursorRowHighlight)
            {
                double y = r.Top;
                rowNumberCursorRowRectangle.Height = cursorRowRectangle.Height = r.Height;
                rowNumberCursorRowRectangle.Margin = cursorRowRectangle.Margin = new Thickness(0, y, 0, 0);
                rowNumberCursorRowRectangle.Visibility = cursorRowRectangle.Visibility = Visibility.Visible;
            }
            else
            {
                rowNumberCursorRowRectangle.Visibility = cursorRowRectangle.Visibility = Visibility.Collapsed;
            }

            if (PatternEditor.Settings.CursorScrollMode == CursorScrollMode.Center)
                patternSV.ScrollToVerticalOffset(r.Top - Math.Floor(patternSV.ViewportHeight / 2 + r.Height / 2));
            else if (PatternEditor.Settings.CursorScrollMode == CursorScrollMode.CenterWithMargins)
                patternSV.ScrollToVerticalOffset(r.Top);

            UpdateStatusBar();
        }

        void UpdateStatusBar()
        {
            var p = Pattern.CursorPosition;

            var cs = pattern.GetColumnSet(p);
            Editor.StatusBarItem1 = cs.Label;

            var vt = pattern.GetValueType(p);
            if (vt != ColumnRenderer.BeatValueType.NoValue)
            {
                var v = pattern.GetValue(p);
                var vs = pattern.GetValueString(p);
                Editor.StatusBarItem2 = vs + " (" + v.ToString() + ") " + p.ParameterColumn.PatternColumn.Parameter.DescribeValue(v);
            }
            else
            {
                Editor.StatusBarItem2 = null;
            }

            var c = pattern.GetColumn(p);
            Editor.StatusBarItem3 = !string.IsNullOrEmpty(c.Description) ? c.Description : c.Label;
        }

        void MakeFieldVisible(Digit d)
        {
            var cr = GetFieldRect(d);
            patternGrid.BringIntoView(cr);
        }

        public void MoveCursor(Digit p, bool select = false)
        {
            p = p.Constrained;

            pattern.CursorPosition = p;
            UpdateCursor();

            if (select)
                pattern.Selection = pattern.Selection.SetEnd(p);
            else
                pattern.Selection = Selection.Start(p);
        }

        internal void MoveCursorDelta(int dx, int dy, bool select = false)
        {
            MoveCursor(pattern.CursorPosition.Offset(dx, dy, select), select);
        }

        void Home(bool select = false)
        {
            MoveCursor(pattern.CursorPosition.Home(), select);
        }

        void End(bool select = false)
        {
            MoveCursor(pattern.CursorPosition.End(), select);
        }

        void Tab()
        {
            Digit p = pattern.CursorPosition;
            p = p.Tab(false);
            MoveCursor(p);
        }

        void ShiftTab()
        {
            Digit p = pattern.CursorPosition;
            p = p.Tab(true);
            MoveCursor(p);
        }

        void UpdateSelection()
        {
            if (pattern == null)
            {
                selectionLayer.Rect = Rect.Empty;
                return;
            }

            var selection = pattern.Selection;

            if (selection.Active)
            {
                var bounds = selection.Bounds;

                var r = GetFieldRect(bounds.Item1);
                r.Union(GetFieldRect(bounds.Item2));
                selectionLayer.Rect = r;

            }
            else
            {
                selectionLayer.Rect = Rect.Empty;
            }
        }

        void GoToColumn(ColumnRenderer.IColumn c)
        {
            var d = pattern.CursorPosition;
            var x = pattern.GetColumnDigit(c);
            d = d.SetColumnSet(x.ColumnSet).SetColumn(x.Column);
            MoveCursor(d);
        }

        void SelectColumn(ColumnRenderer.IColumn c)
        {
            pattern.Selection = Selection.Column(pattern.GetColumnDigit(c));
        }

        void SelectColumnSet(ColumnRenderer.IColumnSet cs)
        {
            pattern.Selection = Selection.ColumnSet(pattern.GetColumnSetDigit(cs));
        }

        void SelectRow(Digit d)
        {
            pattern.Selection = Selection.Row(d);
        }

        void SelectAllColumnsOfBeat(Digit d)
        {
            pattern.Selection = Selection.AllColumnsOfBeat(d);
        }

        public void DoAction(BuzzAction action)
        {
            Editor.EditContext.ActionStack.Do(action);
        }

        public void DoActions(IEnumerable<BuzzAction> actions)
        {
            using (new ActionGroup(Editor.EditContext.ActionStack))
            {
                foreach (var a in actions)
                    Editor.EditContext.ActionStack.Do(a);
            }
        }

        void DoEditActions(IEnumerable<BuzzAction> actions, bool editWasClear)
        {
            using (new ActionGroup(Editor.EditContext.ActionStack))
            {
                foreach (var a in actions)
                    Editor.EditContext.ActionStack.Do(a);

                var pc = pattern.GetColumn(pattern.CursorPosition) as ParameterColumn;

                if (PatternEditor.Settings.EditStayOnRow && pc.Type != ColumnRenderer.ColumnType.Note && !editWasClear)
                {
                    if (!pattern.CursorPosition.IsLastIndex)
                        Editor.EditContext.ActionStack.Do(new Actions.MoveCursorAction(pattern, pattern.CursorPosition.Offset(1, 0)));
                    else
                        Editor.EditContext.ActionStack.Do(new Actions.MoveCursorAction(pattern, pattern.CursorPosition.FirstIndex.Offset(Editor.SelectedStepsRight, Editor.SelectedStepsDown, true)));
                }
                else
                {
                    Editor.EditContext.ActionStack.Do(new Actions.MoveCursorAction(pattern, pattern.CursorPosition.Offset(Editor.SelectedStepsRight, Editor.SelectedStepsDown, true)));
                }
            }
        }

        int lastPlayPosition = int.MinValue;
        public void TimerUpdate()
        {
            if (pattern == null) return;
            int p = pattern.Pattern.PlayPosition;
            if (p == lastPlayPosition) return;

            if (!IsUserVisible(this)) return;

            if (p != int.MinValue)
            {
                double patternHeight = pattern.Pattern.Length * pattern.DefaultRPB / (double)MPEPatternColumn.BUZZ_TICKS_PER_BEAT * ColumnRenderer.Font.LineHeight;
                double y = (p / (double)PatternEvent.TimeBase) * pattern.DefaultRPB / (double)MPEPatternColumn.BUZZ_TICKS_PER_BEAT * ColumnRenderer.Font.LineHeight;

                double heightDiff = (y + ColumnRenderer.Font.LineHeight - patternHeight);

                if (heightDiff > 0 && ColumnRenderer.Font.LineHeight - heightDiff >= 0)
                    rowNumberPlayPosRectangle.Height = playPosRectangle.Height = (ColumnRenderer.Font.LineHeight - heightDiff) * Scale;
                else
                    rowNumberPlayPosRectangle.Height = playPosRectangle.Height = ColumnRenderer.Font.LineHeight * Scale;

                rowNumberPlayPosRectangle.Margin = playPosRectangle.Margin = new Thickness(0, y * Scale, 0, 0);
                rowNumberPlayPosRectangle.Visibility = playPosRectangle.Visibility = Visibility.Visible;

                if (PatternEditor.Settings.FollowPlayPositioninPattern)
                {
                    patternSV.ScrollToVerticalOffset(y * Scale - patternSV.ActualHeight / 2.0);
                }
            }
            else
            {
                rowNumberPlayPosRectangle.Visibility = playPosRectangle.Visibility = Visibility.Collapsed;
            }

            lastPlayPosition = p;
        }

        public static bool IsUserVisible(UIElement element)
        {
            if (!element.IsVisible)
                return false;
            var container = VisualTreeHelper.GetParent(element) as FrameworkElement;
            if (container == null) throw new ArgumentNullException("container");

            Rect bounds = element.TransformToAncestor(container).TransformBounds(new Rect(0.0, 0.0, element.RenderSize.Width, element.RenderSize.Height));
            Rect rect = new Rect(0.0, 0.0, container.ActualWidth, container.ActualHeight);
            return rect.IntersectsWith(bounds);
        }

        void PlayColumnSet(Digit d)
        {
            var col = pattern.GetColumn(d);
            if (col is ParameterColumn)
            {
                var pc = (ParameterColumn)col;
                pc.Set.SendCCsAtTime(pc.GetDigitTime(d));
            }
        }

        void PlayAllColumnSets(Digit d)
        {
            var col = pattern.GetColumn(d);
            if (!(col is ParameterColumn)) return;
            var pc = (ParameterColumn)col;
            var time = pc.GetDigitTime(d);

            foreach (var cs in pattern.ColumnSets)
                cs.SendCCsAtTime(time);

        }

    }
}
