using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Pianoroll.GUI
{
    public class NoteVisual : UIElement
    {
        public NoteEvent Note;
        Editor editor;
        bool selected = false;
        bool newnote = false;

        public bool Selected
        {
            get { return selected; }
            set
            {
                if (value != selected)
                {
                    selected = value;
                    InvalidateVisual();
                }
            }
        }

        public int Velocity
        {
            get { return Note.Velocity; }
            set
            {
                if (value != Note.Velocity)
                {
                    Note.Velocity = value;
                    InvalidateVisual();
                }
            }
        }

        public NoteVisual(NoteEvent ne, Editor editor, bool newnote)
        {
            Note = ne;
            this.editor = editor;
            this.newnote = newnote;
        }

        static Brush[] brush = new Brush[3];
        static Brush[] borderBrush = new Brush[3];
        static Brush[] highlightBrush = new Brush[3];
        static Brush[] shadowBrush = new Brush[3];


        static Typeface font = new Typeface("Verdana");
        static CultureInfo cultureInfo = CultureInfo.GetCultureInfo("en-us");


        protected override void OnRender(DrawingContext dc)
        {
            if (Note.Length <= 0)
                return;

            if (brush[0] == null)
            {
                brush[0] = (Brush)editor.FindResource("NoteBrush");
                borderBrush[0] = (Brush)editor.FindResource("NoteBorderBrush");
                highlightBrush[0] = (Brush)editor.FindResource("NoteHighlightBrush");
                shadowBrush[0] = (Brush)editor.FindResource("NoteShadowBrush");

                brush[1] = (Brush)editor.FindResource("NewNoteBrush");
                borderBrush[1] = (Brush)editor.FindResource("NewNoteBorderBrush");
                highlightBrush[1] = (Brush)editor.FindResource("NewNoteHighlightBrush");
                shadowBrush[1] = (Brush)editor.FindResource("NewNoteShadowBrush");

                brush[2] = (Brush)editor.FindResource("SelNoteBrush");
                borderBrush[2] = (Brush)editor.FindResource("SelNoteBorderBrush");
                highlightBrush[2] = (Brush)editor.FindResource("SelNoteHighlightBrush");
                shadowBrush[2] = (Brush)editor.FindResource("SelNoteShadowBrush");

                for (int i = 0; i < brush.Length; i++)
                {
                    brush[i].Freeze();
                    borderBrush[i].Freeze();
                    highlightBrush[i].Freeze();
                    shadowBrush[i].Freeze();
                }
            }

            PatternDimensions pd = editor.PatDim;

            double h = Math.Max(1, Note.Length * pd.BeatHeight / PianorollGlobal.TicksPerBeat);

            if (h > 0)
            {
                int bi = newnote ? 1 : 0;
                if (selected)
                    bi = 2;

                dc.DrawRectangle(borderBrush[bi], null, new Rect(0, 0, pd.NoteWidth + 1, h + 1));
                if (h > 1) dc.DrawRectangle(brush[bi], null, new Rect(1, 1, pd.NoteWidth - 1, h - 1));

                dc.DrawRectangle(highlightBrush[bi], null, new Rect(1, 1, pd.NoteWidth - 2, 1));
                if (h > 3) dc.DrawRectangle(highlightBrush[bi], null, new Rect(1, 2, 1, h - 3));

                dc.DrawRectangle(shadowBrush[bi], null, new Rect(2, h - 1, pd.NoteWidth - 2, 1));
                if (h > 3) dc.DrawRectangle(shadowBrush[bi], null, new Rect(pd.NoteWidth - 1, 2, 1, h - 3));


                FormattedText ft = new FormattedText(
                    Note.Velocity.ToString(),
                    cultureInfo,
                    FlowDirection.LeftToRight,
                    font, pd.NoteFontSize, Brushes.Black);


                ft.MaxTextWidth = pd.NoteWidth;
                ft.TextAlignment = TextAlignment.Center;
                ft.MaxTextHeight = h;

                dc.DrawText(ft, new Point(0, 0));
            }

        }

        protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            editor.pianoroll.NoteClicked(this, true);

            Point p = e.GetPosition(this);

            if (Mouse.OverrideCursor == Cursors.SizeNS)
                editor.pianoroll.BeginSetNoteLength(this, p);
            else if (Mouse.OverrideCursor == Cursors.Hand)
                editor.pianoroll.BeginMoveSelectedNotes(this, p);

            e.Handled = true;

        }

        protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            Point p = e.GetPosition(this);

            if (editor.pianoroll.mode == PianorollCtrl.Mode.SetNoteLength)
            {
                editor.pianoroll.UpdateSetNoteLength(p);
            }
            else if (editor.pianoroll.mode == PianorollCtrl.Mode.MoveSelectedNotes)
            {
                editor.pianoroll.UpdateMoveSelectedNotes(p);
            }
            else
            {
                double h = Note.Length * editor.PatDim.BeatHeight / PianorollGlobal.TicksPerBeat;

                if (p.Y >= h - 3)
                    Mouse.OverrideCursor = Cursors.SizeNS;
                else
                    Mouse.OverrideCursor = Cursors.Hand;
            }
            
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (editor.pianoroll.mode == PianorollCtrl.Mode.SetNoteLength)
                editor.pianoroll.EndSetNoteLength();
            else if (editor.pianoroll.mode == PianorollCtrl.Mode.MoveSelectedNotes)
                editor.pianoroll.EndMoveSelectedNotes();

            editor.pianoroll.NoteClicked(this, false);
            base.OnMouseLeftButtonUp(e);
        }


        protected override void OnMouseLeave(MouseEventArgs e)
        {
            Mouse.OverrideCursor = null;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                if (Selected)
                    editor.pianoroll.DeleteNotes(editor.pianoroll.selectedNotes);
                else
                    editor.pianoroll.DeleteNotes(new NoteSet(Note));

                e.Handled = true;
            }

            base.OnMouseDown(e);
        }
    }
}
