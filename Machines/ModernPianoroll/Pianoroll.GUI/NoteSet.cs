using System.Windows;

namespace Pianoroll.GUI
{
    public class NoteSet : IEnumerable<NoteEvent>
    {
        List<NoteEvent> notes;

        public NoteSet()
        {
            notes = new List<NoteEvent>();
        }

        public NoteSet(NoteEvent ne)
        {
            notes = new List<NoteEvent>();
            notes.Add(ne);
        }

        public NoteSet(NoteSet x)
        {
            notes = new List<NoteEvent>(x.notes);
        }

        public bool IsEmpty { get { return notes.Count == 0; } }

        public void Clear()
        {
            notes.Clear();
        }

        public void Add(NoteEvent ne)
        {
            notes.Add(ne);
        }

        public bool Contains(NoteEvent ne)
        {
            return notes.Contains(ne);
        }

        public void Remove(NoteSet x)
        {
            // NOTE: O(n^2), could be too slow
            List<NoteEvent> newlist = new List<NoteEvent>();

            foreach (NoteEvent e in notes)
            {
                if (!x.Contains(e))
                    newlist.Add(e);
            }

            notes = newlist;
        }

        public NoteEvent GetFirstOrLastNote(bool first)
        {
            if (IsEmpty)
                return NoteEvent.Invalid;

            int bestt = first ? Int32.MaxValue : Int32.MinValue;
            int bestk = bestt;
            NoteEvent beste = NoteEvent.Invalid;

            foreach (NoteEvent ne in notes)
            {
                if ((first && ne.Time < bestt || (ne.Time == bestt && ne.Note < bestk)) || (!first && ne.Time > bestt || (ne.Time == bestt && ne.Note > bestk)))
                {
                    bestt = ne.Time;
                    bestk = ne.Note;
                    beste = ne;
                }
            }

            return beste;
        }

        public Int32Rect Bounds
        {
            get
            {
                if (IsEmpty)
                    return new Int32Rect();

                int x1 = Int32.MaxValue, x2 = Int32.MinValue, y1 = Int32.MaxValue, y2 = Int32.MinValue;

                foreach (NoteEvent ne in notes)
                {
                    x1 = Math.Min(x1, ne.Note);
                    x2 = Math.Max(x2, ne.Note);
                    y1 = Math.Min(y1, ne.Time);
                    y2 = Math.Max(y2, ne.Time + ne.Length);
                }

                return new Int32Rect(x1, y1, x2 - x1 + 1, y2 - y1);
            }
        }

        public int CommonVelocity
        {
            get
            {
                int x = -1;

                foreach (NoteEvent ne in notes)
                {
                    if (x > 0 && ne.Velocity != x)
                        return -1;
                    else
                        x = ne.Velocity;
                }

                return x;
            }

            set
            {
                for (int i = 0; i < notes.Count; i++)
                {
                    NoteEvent x = notes[i];
                    x.Velocity = value;
                    notes[i] = x;
                }
            }
        }

        public IEnumerator<NoteEvent> GetEnumerator() { return notes.GetEnumerator(); }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return notes.GetEnumerator(); }

    }
}
