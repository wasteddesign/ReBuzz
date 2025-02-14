using System.Windows;
using System.Windows.Media;

namespace Pianoroll.GUI
{
    internal class NotePanel : FrameworkElement
    {
        List<NoteVisual> children;
        Editor ped;
        int firstBeat;

        public IEnumerable<NoteVisual> NoteVisuals { get { return children; } }

        public bool IsEmpty { get { return children.Count == 0; } }

        public NotePanel(Editor ped, int fb)
        {
            this.ped = ped;
            this.firstBeat = fb;
            children = new List<NoteVisual>();

        }

        public NoteVisual GetNoteVisual(int key)
        {
            foreach (NoteVisual nv in children)
                if (nv.Note.Note == key)
                    return nv;

            return null;
        }

        public void RemoveNoteVisual(NoteVisual nv)
        {
            children.Remove(nv);
            RemoveVisualChild(nv);
        }

        public void Clear()
        {
            foreach (NoteVisual nv in children)
                RemoveVisualChild(nv);

            children.Clear();
        }

        public void AddNote(NoteEvent ne, bool newnote)
        {
            foreach (NoteVisual n in children)
            {
                if (ne.Time == n.Note.Time && ne.Note == n.Note.Note)
                {
                    children.Remove(n);
                    RemoveVisualChild(n);
                    break;
                }

            }

            NoteVisual nv = new NoteVisual(ne, ped, newnote);
            children.Add(nv);
            AddVisualChild(nv);
        }

        public void SelectNotes(Int32Rect rect, NoteSet notes, bool add)
        {
            foreach (NoteVisual n in children)
            {
                if (n.Note.InRect(rect))
                {
                    notes.Add(n.Note);
                    n.Selected = true;
                }
                else if (!add)
                {
                    n.Selected = false;
                }
            }
        }

        public void SelectNotes(int t, NoteSet notes, bool add)
        {
            foreach (NoteVisual n in children)
            {
                if (n.Note.Time == t)
                {
                    notes.Add(n.Note);
                    n.Selected = true;
                }
                else if (!add)
                {
                    n.Selected = false;
                }
            }
        }

        public void SelectNote(NoteEvent ne, NoteSet notes, bool add)
        {
            foreach (NoteVisual n in children)
            {
                if (n.Note.Equals(ne))
                {
                    notes.Add(n.Note);
                    n.Selected = true;
                }
                else if (!add)
                {
                    n.Selected = false;
                }
            }
        }

        public void DeleteNote(NoteEvent ne)
        {
            foreach (NoteVisual n in children)
            {
                if (n.Note.Equals(ne))
                {
                    children.Remove(n);
                    RemoveVisualChild(n);
                    break;
                }
            }
        }

        public void SetVelocity(NoteEvent ne, int velocity)
        {
            foreach (NoteVisual n in children)
            {
                if (n.Note.Equals(ne))
                {
                    n.Velocity = velocity;
                    break;
                }
            }
        }

        protected override int VisualChildrenCount
        {
            get { return children.Count; }
        }

        protected override Visual GetVisualChild(int index)
        {
            if (index < 0 || index >= children.Count)
                throw new ArgumentOutOfRangeException();

            return (Visual)children[index];
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            Size childSize = new Size(double.PositiveInfinity, double.PositiveInfinity);

            foreach (UIElement child in children)
                child.Measure(childSize);

            return new Size();
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach (NoteVisual note in children)
            {
                double x = (note.Note.Note - ped.PatDim.FirstMidiNote) * ped.PatDim.NoteWidth;
                double y = note.Note.Time * ped.PatDim.BeatHeight / PianorollGlobal.TicksPerBeat;
                y -= firstBeat * ped.PatDim.BeatHeight;

                note.Arrange(new Rect(new Point(x, y), note.DesiredSize));
            }

            return finalSize;
        }
    }
}
