using System;
using System.Windows;

namespace WDE.ModernPatternEditor
{
    public struct NoteEvent : IComparable<NoteEvent>
    {
        public int Time;
        public int Note;
        public int Length;
        public int Velocity;

        public static NoteEvent Invalid { get { return new NoteEvent(-1, -1, -1, -1); } }
        public bool IsInvalid { get { return Time < 0 && Length < 0 && Note < 0 && Velocity < 0; } }
        public int OffTime { get { return Time + Length; } }

        public NoteEvent(int t, int l, int n, int v)
        {
            Time = t;
            Length = l;
            Note = n;
            Velocity = v;
        }


        public int CompareTo(NoteEvent x)
        {
            if (Time == x.Time)
            {
                if (Note == x.Note)
                {
                    if (Length == x.Length)
                        return Velocity.CompareTo(x.Velocity);
                    else
                        return Length.CompareTo(x.Length);
                }
                else
                    return Note.CompareTo(x.Note);
            }
            else
                return Time.CompareTo(x.Time);
        }

        public bool InRect(Int32Rect r)
        {
            return Time + Length > r.Y && Time < r.Y + r.Height && Note >= r.X && Note < r.X + r.Width;
        }
    }
}
