using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.Windows;
using pc = Wintellect.PowerCollections;

namespace WDE.ModernSequenceEditor
{
    public class Clipboard
    {
        List<pc.OrderedDictionary<int, EventRef>> data;
        int firstTrack;
        int span;

        public bool ContainsData { get { return data != null; } }
        public int FirstTrack { get { return firstTrack; } }
        public int RowCount { get { return data.Count; } }
        public int Span { get { return span; } }

        public Clipboard()
        {
        }

        public Clipboard(Clipboard c)
        {
            Clone(c);
        }

        public void Clone(Clipboard c)
        {
            if (c.data != null)
            {
                data = new List<pc.OrderedDictionary<int, EventRef>>(c.data.Count);
                for (int i = 0; i < data.Count; i++)
                {
                    data[i] = new pc.OrderedDictionary<int, EventRef>();
                    foreach (var e in c.data[i]) data[i][e.Key] = e.Value;
                }
            }

            firstTrack = c.firstTrack;
            span = c.span;
        }

        public void Clear()
        {
            data = null;
        }

        public void Cut(ISong song, Rect r)
        {
            CutOrCopy(song, r, true);
        }

        public void Copy(ISong song, Rect r)
        {
            CutOrCopy(song, r, false);
        }

        public void Paste(ISong song, int time)
        {
            if (!ContainsData) return;

            for (int t = 0; t < data.Count; t++)
            {
                if (t + firstTrack >= song.Sequences.Count) return;
                song.Sequences[t + firstTrack].Clear(time, span);
                foreach (var e in data[t]) e.Value.Set(song.Sequences[t + firstTrack], time + e.Key);
            }

        }

        void CutOrCopy(ISong song, Rect r, bool cut)
        {
            firstTrack = (int)r.Top;
            span = (int)r.Width;
            data = new List<pc.OrderedDictionary<int, EventRef>>();

            for (int t = (int)r.Top; t < (int)r.Bottom; t++)
            {
                if (t >= song.Sequences.Count) return;

                var events = (song.Sequences[t].Events.Dictionary) as pc.OrderedDictionary<int, SequenceEvent>;
                var range = events.Range((int)r.Left, true, (int)r.Right, false);

                var d = new pc.OrderedDictionary<int, EventRef>();
                foreach (var e in range) d[e.Key - (int)r.Left] = new EventRef(e.Value);
                data.Add(d);

                if (cut) song.Sequences[t].Clear((int)r.Left, (int)r.Width);
            }

        }
    }
}
