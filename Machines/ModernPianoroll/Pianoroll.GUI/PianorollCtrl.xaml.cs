using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;


namespace Pianoroll.GUI
{
    /// <summary>
    /// Interaction logic for Pianoroll.xaml
    /// </summary>
    public partial class PianorollCtrl : UserControl
    {
        internal Editor editor;
        NoteLayer noteLayer;
        SelectionLayer selectionLayer = new SelectionLayer();
        Pattern pattern;

        public Pattern Pattern
        {
            set
            {
                pattern = value;
                selectedNotes.Clear();
                if (noteLayer != null) noteLayer.InsertNotePanel.Clear();
            }
        }

        public void ThemeChanged()
        {
            //Resources.MergedDictionaries.Clear();
            //Resources.MergedDictionaries.Add(Editor.Theme);
            this.Resources = Editor.Theme;
            UpdateBackground();
            UpdatePatternDimensions();
        }

        public PianorollCtrl()
        {
            this.Resources = Editor.Theme;

            InitializeComponent();
            this.LayoutUpdated += new EventHandler(PianorollCtrl_LayoutUpdated);
            scrollViewer.ScrollChanged += new ScrollChangedEventHandler(scrollViewer_ScrollChanged);
            this.PreviewKeyDown += new KeyEventHandler(PianorollCtrl_PreviewKeyDown);
            this.LostFocus += new RoutedEventHandler(PianorollCtrl_LostFocus);

            canvas.MouseLeftButtonDown += new MouseButtonEventHandler(canvas_MouseLeftButtonDown);
            canvas.MouseMove += new MouseEventHandler(canvas_MouseMove);
            canvas.MouseLeftButtonUp += new MouseButtonEventHandler(canvas_MouseLeftButtonUp);
            canvas.MouseDown += new MouseButtonEventHandler(canvas_MouseDown);

            UpdateBackground();
        }

        void canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
        }

        bool addToSelection = false;

        void canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            addToSelection = Keyboard.Modifiers == ModifierKeys.Control;

            Point p = e.GetPosition(canvas);
            selectionLayer.BeginSelect(p);
            Select(new Int32Rect(), addToSelection);
        }

        void canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (selectionLayer.Selecting)
            {
                Point p = e.GetPosition(canvas);
                Select(DPToLP(selectionLayer.UpdateSelect(p)), addToSelection);
            }
        }


        void canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (selectionLayer.Selecting)
            {
                Point p = e.GetPosition(canvas);
                selectionLayer.EndSelect(p);
            }
        }

        Int32Rect DPToLP(Rect r)
        {
            r.Y -= Canvas.GetTop(canvas.Children[0]);

            int lx1 = (int)Math.Floor(r.X / editor.PatDim.NoteWidth);
            int lx2 = (int)Math.Ceiling((r.X + r.Width) / editor.PatDim.NoteWidth);
            int ly1 = (int)Math.Floor(r.Y * PianorollGlobal.TicksPerBeat / editor.PatDim.BeatHeight);
            int ly2 = (int)Math.Ceiling((r.Y + r.Height) * PianorollGlobal.TicksPerBeat / editor.PatDim.BeatHeight);

            return new Int32Rect(lx1 + editor.PatDim.FirstMidiNote, ly1, lx2 - lx1, ly2 - ly1);
        }

        Rect LPToDP(Int32Rect r)
        {
            double dx1 = (r.X - editor.PatDim.FirstMidiNote) * editor.PatDim.NoteWidth;
            double dx2 = dx1 + r.Width * editor.PatDim.NoteWidth;
            double dy1 = r.Y * editor.PatDim.BeatHeight / PianorollGlobal.TicksPerBeat;
            double dy2 = dy1 + r.Height * editor.PatDim.BeatHeight / PianorollGlobal.TicksPerBeat;

            return new Rect(dx1, dy1 + Canvas.GetTop(canvas.Children[0]), dx2 - dx1, dy2 - dy1);
        }

        Rect NoteVisualBounds(NoteEvent ne)
        {
            return LPToDP(new Int32Rect(ne.Note, ne.Time, 1, ne.Length));
        }

        int YToTime(double y)
        {
            double x = y * PianorollGlobal.TicksPerBeat / editor.PatDim.BeatHeight;
            return (int)Math.Round(x);
        }

        int GetCursorTime()
        {
            return YToTime(scrollViewer.VerticalOffset);
        }

        void SetCursorTime(int t)
        {
            double x = (double)t * editor.PatDim.BeatHeight / PianorollGlobal.TicksPerBeat;
            scrollViewer.ScrollToVerticalOffset(Math.Round(x));
        }

        double SnapToGrid(double y)
        {
            if (editor.GridValue == 0)
                return y;

            y = y * editor.GridValue / editor.PatDim.BeatHeight;
            y = Math.Round(y);
            return y * editor.PatDim.BeatHeight / editor.GridValue;
        }

        internal NoteSet selectedNotes = new NoteSet();

        void Select(Point p, bool add)
        {
            if (!add)
                selectedNotes.Clear();

            Select(new Int32Rect((int)p.X, (int)p.Y, 1, 1), add);
        }

        void Select(Int32Rect r, bool add)
        {
            if (!add)
                selectedNotes.Clear();

            noteLayer.SelectNotes(r, selectedNotes, add);
        }

        internal void Select(NoteEvent ne, bool add)
        {
            if (!add)
                selectedNotes.Clear();

            noteLayer.SelectNote(ne, selectedNotes, add);
        }

        void SelectNotesStartingAt(int t)
        {
            selectedNotes.Clear();
            noteLayer.SelectNotes(t, selectedNotes, false);
        }

        void SelectAdjacentNote(bool next, bool add)
        {
            NoteEvent ne;

            if (!selectedNotes.IsEmpty)
            {
                if (add)
                    ne = selectedNotes.GetFirstOrLastNote(!next);
                else
                    ne = selectedNotes.GetFirstOrLastNote(true);
            }
            else
                ne = NoteEvent.Invalid;

            ne = pattern.GetAdjacentNote(0, ne, next);
            if (!ne.IsInvalid)
            {
                Select(ne, add);
                SetCursorTime(ne.Time);
            }
        }

        internal void DeleteNotes(NoteSet ns)
        {
            noteLayer.DeleteNotes(ns);
            pattern.BeginAction("Delete Notes");
            editor.pattern.DeleteNotes(ns);
            selectedNotes.Remove(ns);
        }

        void PianorollCtrl_LostFocus(object sender, RoutedEventArgs e)
        {
            ResetInsertNotes();
        }

        public void KillFocus()
        {
            ResetInsertNotes();
        }

        public void CursorDown()
        {
            double newy;

            if (editor.GridValue > 0)
                newy = SnapToGrid(scrollViewer.VerticalOffset + (double)editor.PatDim.BeatHeight / editor.GridValue);
            else
                newy = scrollViewer.VerticalOffset + 1;

            if (!noteLayer.InsertNotePanel.IsEmpty)
            {
                double newt = YToTime(newy);
                if (newt > pattern.LengthInTicks)
                {
                    pattern.BeginAction("Grow");
                    pattern.Length = pattern.Length + 1;
                }
            }

            scrollViewer.ScrollToVerticalOffset(newy);

            if (noteLayer.InsertNotePanel.IsEmpty)
                SelectNotesStartingAt(YToTime(newy));
        }

        public void CursorUp()
        {
            double newy;

            if (editor.GridValue > 0)
                newy = SnapToGrid(scrollViewer.VerticalOffset - (double)editor.PatDim.BeatHeight / editor.GridValue);
            else
                newy = scrollViewer.VerticalOffset - 1;

            newy = Math.Max(newy, 0);

            scrollViewer.ScrollToVerticalOffset(newy);

            if (noteLayer.InsertNotePanel.IsEmpty)
                SelectNotesStartingAt(YToTime(newy));
        }

        void PianorollCtrl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyboardDevice.Modifiers == ModifierKeys.None)
            {
                if (e.Key == Key.Down)
                {
                    CursorDown();
                    e.Handled = true;
                }

                if (e.Key == Key.Up)
                {
                    CursorUp();
                    e.Handled = true;
                }

                if (e.Key == Key.PageDown)
                {
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + editor.PatDim.BeatHeight * 2);
                    e.Handled = true;
                }

                if (e.Key == Key.PageUp)
                {
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - editor.PatDim.BeatHeight * 2);
                    e.Handled = true;
                }

                if (e.Key == Key.Left)
                {
                    SelectAdjacentNote(false, false);
                    e.Handled = true;
                }

                if (e.Key == Key.Right)
                {
                    SelectAdjacentNote(true, false);
                    e.Handled = true;
                }

                if (e.Key == Key.Home)
                {
                    scrollViewer.ScrollToVerticalOffset(0);
                    e.Handled = true;
                }

                if (e.Key == Key.End)
                {
                    scrollViewer.ScrollToVerticalOffset(double.MaxValue);
                    e.Handled = true;
                }

                if (e.Key == Key.Delete)
                {
                    DeleteNotes(selectedNotes);
                    e.Handled = true;
                }

                if (e.Key == Key.D1)
                {
                    e.Handled = true;
                    ShowNoteProperties();
                }
            }

            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.A)
                {
                    Select(new Int32Rect(0, 0, Int32.MaxValue, Int32.MaxValue), false);
                    e.Handled = true;
                }

            }

            if (e.KeyboardDevice.Modifiers == ModifierKeys.Shift)
            {
                if (e.Key == Key.Left)
                {
                    SelectAdjacentNote(false, true);
                    e.Handled = true;
                }

                if (e.Key == Key.Right)
                {
                    SelectAdjacentNote(true, true);
                    e.Handled = true;
                }
            }

        }


        public void PianoKeyDown(int k, int v)
        {
            NoteEvent ne = new NoteEvent(GetCursorTime(), 0, k, v);
            noteLayer.InsertNotePanel.AddNote(ne, true);

        }

        public void PianoKeyUp(int k)
        {
            NoteVisual nv = noteLayer.InsertNotePanel.GetNoteVisual(k);

            if (nv != null)
            {
                noteLayer.InsertNotePanel.RemoveNoteVisual(nv);

                if (nv.Note.Length > 0)
                    AddNote(nv.Note);
            }
        }

        void AddNote(NoteEvent note)
        {
            noteLayer.AddNote(note);

            pattern.BeginAction("Add Note");
            // TODO: Find free track
            int track = editor.pattern.GetAvailableTrack(note);
            editor.pattern.AddNote(track, note);
        }

        public void AddRecordedNote(NoteEvent note)
        {
            noteLayer.AddNote(note);
        }


        void UpdateInsertNotes()
        {
            int t = GetCursorTime();

            foreach (NoteVisual nv in noteLayer.InsertNotePanel.NoteVisuals)
            {
                if (t > nv.Note.Time)
                    nv.Note.Length = t - nv.Note.Time;
                else
                    nv.Note.Length = 0;

                nv.InvalidateVisual();

            }

            if (!noteLayer.InsertNotePanel.IsEmpty)
                noteLayer.InsertNotePanel.InvalidateMeasure();
        }

        void ResetInsertNotes()
        {
            if (!noteLayer.InsertNotePanel.IsEmpty)
            {
                noteLayer.InsertNotePanel.Clear();
                noteLayer.InsertNotePanel.InvalidateMeasure();
            }
        }


        void scrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange != 0)
            {
                UpdateInsertNotes();

                UpdateBackground();
            }
        }

        void UpdateBackground()
        {
            if (border == null) return;
            ImageBrush ibr = border.Background as ImageBrush;
            if (ibr != null && ibr.ImageSource != null)
            {
                double spd = -Parallax.GetScrollSpeed(ibr);
                ibr.Viewport = new Rect(0, spd * scrollViewer.VerticalOffset, ibr.ImageSource.Width, ibr.ImageSource.Height);
            }
        }

        void PianorollCtrl_LayoutUpdated(object sender, EventArgs e)
        {
            double y = Math.Floor(scrollViewer.ViewportHeight / 2);
            Canvas.SetTop(canvas.Children[0], y);
            Canvas.SetTop(canvas.Children[1], y);
            canvas.Height = editor.PatDim.Height + scrollViewer.ViewportHeight;

        }

        public void UpdatePatternDimensions()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            canvas.Children.Clear();
            canvas.Children.Add(new PatternBackgroundVisual(editor));

            if (noteLayer == null)
                noteLayer = new NoteLayer(editor);
            else
                noteLayer.Recreate();

            canvas.Children.Add(noteLayer);
            canvas.Width = editor.PatDim.Width;
            canvas.Children.Add(selectionLayer);

            selectedNotes.Clear();

            sw.Stop();

            editor.PianorollMachine.WriteDC(string.Format("UpdatePatternDimensions {0}ms", sw.ElapsedMilliseconds));
        }

        public void NoteClicked(NoteVisual nv, bool play)
        {
            if (!selectedNotes.Contains(nv.Note))
                Select(nv.Note, Keyboard.Modifiers == ModifierKeys.Control);

            editor.PianorollMachine.PlayNoteEvents(selectedNotes, play);
        }


        public void ShowNoteProperties()
        {
            if (selectedNotes.IsEmpty)
                return;

            Rect r = LPToDP(selectedNotes.Bounds);
            Point p = canvas.PointToScreen(new Point(r.X + r.Width + 2, r.Y - 1));

            NotePropertiesWindow hw = new NotePropertiesWindow(selectedNotes.CommonVelocity)
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = p.X,
                Top = p.Y
            };

            new WindowInteropHelper(hw).Owner = ((HwndSource)PresentationSource.FromVisual(editor)).Handle;

            if (!(bool)hw.ShowDialog())
                return;

            pattern.BeginAction("Set Velocity");
            pattern.SetVelocity(0, selectedNotes, hw.Velocity);
            noteLayer.SetVelocity(selectedNotes, hw.Velocity);
            selectedNotes.CommonVelocity = hw.Velocity;

            editor.lastVelocity = hw.Velocity;
        }

        NoteEvent setNoteLengthOriginalEvent;
        Point dragStartPoint;
        NoteVisual dragNoteVisual;

        public enum Mode { Normal, SetNoteLength, MoveSelectedNotes };
        internal Mode mode = Mode.Normal;


        public void BeginSetNoteLength(NoteVisual nv, Point p)
        {
            nv.CaptureMouse();
            dragStartPoint = p;
            dragNoteVisual = nv;
            mode = Mode.SetNoteLength;
            setNoteLengthOriginalEvent = nv.Note;
        }

        public void UpdateSetNoteLength(Point p)
        {
            int oh = setNoteLengthOriginalEvent.Length * editor.PatDim.BeatHeight / PianorollGlobal.TicksPerBeat;

            bool snap = Keyboard.Modifiers == ModifierKeys.Control && editor.GridValue > 0;

            double h = oh + (p.Y - dragStartPoint.Y);
            if (snap) h = SnapToGrid(h);
            int l = (int)Math.Round(h * PianorollGlobal.TicksPerBeat / editor.PatDim.BeatHeight);

            if (snap)
                l = Math.Max(l, PianorollGlobal.TicksPerBeat / editor.GridValue);
            else
                l = Math.Max(l, PianorollGlobal.TicksPerBeat / editor.PatDim.BeatHeight);

            dragNoteVisual.Note.Length = l;
            dragNoteVisual.InvalidateVisual();

            //            canvas.BringIntoView(NoteVisualBounds(dragNoteVisual.Note));
            MakeVisible(NoteVisualBounds(dragNoteVisual.Note));
        }

        public void EndSetNoteLength()
        {
            dragNoteVisual.ReleaseMouseCapture();

            if (dragNoteVisual.Note.Length != setNoteLengthOriginalEvent.Length)
            {
                pattern.BeginAction("Set Note Length");
                pattern.SetLength(0, new NoteEvent[] { setNoteLengthOriginalEvent }, dragNoteVisual.Note.Length);
            }

            mode = Mode.Normal;
            dragNoteVisual = null;
        }

        public void BeginMoveSelectedNotes(NoteVisual nv, Point p)
        {
            nv.CaptureMouse();
            dragStartPoint = p;
            dragNoteVisual = nv;
            mode = Mode.MoveSelectedNotes;
        }

        public void UpdateMoveSelectedNotes(Point p)
        {
        }

        public void EndMoveSelectedNotes()
        {
            dragNoteVisual.ReleaseMouseCapture();
            mode = Mode.Normal;
            dragNoteVisual = null;
        }

        void MakeVisible(Rect r)
        {
            double y = scrollViewer.VerticalOffset;

            double bottom = scrollViewer.VerticalOffset + scrollViewer.ViewportHeight;
            if (r.Bottom > bottom)
                y = r.Bottom - scrollViewer.ViewportHeight;

            //            scrollViewer.CurrentVerticalOffset = y;

            DoubleAnimation vertAnim = new DoubleAnimation();
            vertAnim.From = scrollViewer.VerticalOffset;
            vertAnim.To = y;
            vertAnim.Duration = new Duration(TimeSpan.FromMilliseconds(200));
            Storyboard sb = new Storyboard();
            sb.Children.Add(vertAnim);
            Storyboard.SetTarget(vertAnim, scrollViewer);
            Storyboard.SetTargetProperty(vertAnim, new PropertyPath(AniScrollViewer.CurrentVerticalOffsetProperty));
            sb.Begin();
        }
    }
}
