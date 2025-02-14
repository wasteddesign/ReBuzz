using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using WDE.ModernPatternEditor.MPEStructures;
using static System.Net.Mime.MediaTypeNames;

namespace Pianoroll.GUI
{
    public class Pattern
    {
        private MPEPattern mpePattern;

        public IPattern ReBuzzPattern { get; }

        int length;

        public delegate void LengthChanged(Pattern p);
        public event LengthChanged LengthChangedEvent;

        internal int Length
        {
            get { return length; }
            set
            {
                length = value;
                ReBuzzPattern.Length = length * 4;

                if (LengthChangedEvent != null)
                    LengthChangedEvent(this);
            }
        }

        public int LengthInTicks { get { return length * PianorollGlobal.TicksPerBeat; } }

        public int LengthInBuzzTicks
        {
            set
            {
                length = LengthFromNumBuzzTicks(value);

                if (LengthChangedEvent != null)
                    LengthChangedEvent(this);
            }
        }

        int LengthFromNumBuzzTicks(int n)
        {
            return n;
        }

        public Pattern(MPEPattern mpep, IPattern pattern, int numbuzzticks)
        {
            this.mpePattern = mpep;
            this.ReBuzzPattern = pattern;
            length = LengthFromNumBuzzTicks(numbuzzticks);
        }

        public void Update()
        {
            if (LengthChangedEvent != null)
                LengthChangedEvent(this);
        }

        public void AddNote(int track, NoteEvent note)
        {
            var notec = mpePattern.MPEPatternColumns.FirstOrDefault(c => c.Parameter.Type == ParameterType.Note && c.ParamTrack == track);
            if (notec != null)
            {
                PatternEvent pe = new PatternEvent()
                {
                    Time = GetReBuzzTime(note.Time),
                    Duration = GetReBuzzTime(note.Length),
                    Value = BuzzNote.FromMIDINote(note.Note)
                };
                PatternEvent peOff = new PatternEvent()
                {
                    Time = GetReBuzzTime(note.Time + note.Length),
                    Duration = 0,
                    Value = BuzzNote.Off
                };
                PatternEvent[] pEvents = [pe, peOff];
                notec.SetEvents(pEvents, true, false);
            }
        }

        public void AddTrack()
        {

        }

        public void BeginAction(string name)
        {

        }

        public void DeleteNotes()
        {
            if (mpePattern?.Pattern?.Machine != null)
            {
                var m = mpePattern.Pattern.Machine;
                for (int i = 0; i < m.TrackCount; i++)
                {
                    var notec = mpePattern.MPEPatternColumns.FirstOrDefault(c => c.Parameter.Type == ParameterType.Note && c.ParamTrack == i);
                    var pe = notec.GetEvents(0, int.MaxValue).ToArray();
                    notec.SetEvents(pe, false, false);
                }
            }
        }

        public void DeleteNotes(IEnumerable<NoteEvent> notes)
        {
            if (mpePattern?.Pattern?.Machine != null)
            {
                var m = mpePattern.Pattern.Machine;
                for (int i = 0; i < m.TrackCount; i++)
                {
                    DeleteNotes(i, notes);
                }
            }
        }

        public void DeleteNotes(int track, IEnumerable<NoteEvent> notes)
        {
            var notec = mpePattern.MPEPatternColumns.FirstOrDefault(c => c.Parameter.Type == ParameterType.Note && c.ParamTrack == track);
            if (notec != null)
            {
                foreach (var prNote in notes)
                {
                    var pe = notec.GetEvents(0, int.MaxValue).OrderBy(ne => ne.Time).ToArray();
                    foreach (var e in pe)
                    {
                        if (GetReBuzzTime(prNote.Time) == e.Time)
                        {
                            notec.SetEvents([e], false, false);
                        }

                        // Is next off?
                        var next = notec.GetEvents(e.Time + 1, int.MaxValue);
                        if (next.Count() > 0 && next.First().Value == BuzzNote.Off)
                        {
                            notec.SetEvents([next.First()], false, false);
                        }
                    }
                }
            }
        }

        public NoteEvent GetAdjacentNote(int track, NoteEvent note, bool next)
        {
            return new NoteEvent();
        }


        public List<NoteEvent> GetAllNotes(int track)
        {
            List<NoteEvent> noteEvents = new List<NoteEvent>();

            noteEvents.Clear();
            var notec = mpePattern.MPEPatternColumns.FirstOrDefault(c => c.Parameter.Type == ParameterType.Note && c.ParamTrack == track);
            if (notec != null)
            {
                var pe = notec.GetEvents(0, int.MaxValue);
                foreach (var e in pe)
                {
                    NoteEvent noteEvent = new NoteEvent();
                    if (e.Value != BuzzNote.Off)
                        noteEvent.Note = BuzzNote.ToMIDINote(e.Value);

                    // Calculate next note or note off.
                    var next = notec.GetEvents(e.Time + 1, int.MaxValue);
                    if (next.Count() > 0)
                    {
                        noteEvent.Length = GetPianorollTime(next.First().Time - e.Time);
                    }
                    else
                    {
                        noteEvent.Length = GetPianorollTime(mpePattern.Pattern.Length - e.Time);
                    }

                    noteEvent.Velocity = 127; // TODO: Get volumen from volume column
                    noteEvent.Time = GetPianorollTime(e.Time);
                    noteEvents.Add(noteEvent);
                }
            }

            return noteEvents;
        }

        public void MoveNotes(int track, IEnumerable<NoteEvent> notes, int dx, int dy)
        {

        }

        public void SetHostLength(int nbuzzticks)
        {

        }

        public void SetLength(int track, IEnumerable<NoteEvent> notes, int length)
        {

        }

        public void SetVelocity(int track, IEnumerable<NoteEvent> notes, int velocity)
        {

        }

        int GetReBuzzTime(int time)
        {
            return time * PatternEvent.TimeBase * mpePattern.RowsPerBeat / PianorollGlobal.TicksPerBeat / 4;
        }

        int GetPianorollTime(int time)
        {
            return 4 * time / PatternEvent.TimeBase * PianorollGlobal.TicksPerBeat / mpePattern.RowsPerBeat;
        }

        internal int GetAvailableTrack(NoteEvent note)
        {
            int time = GetReBuzzTime(note.Time);

            var m = mpePattern.Pattern.Machine;
            for (int track = 0; track < m.TrackCount; track++)
            {
                var notec = mpePattern.MPEPatternColumns.FirstOrDefault(c => c.Parameter.Type == ParameterType.Note && c.ParamTrack == track);
                if (notec != null)
                {
                    var pe = notec.GetEvents(0, int.MaxValue);

                    if (pe.Count() == 0)
                        return track;

                    bool available = true;

                    for (int i = 0; i < pe.Count(); i++)
                    {
                        PatternEvent peStart = pe.ElementAt(i);
                        PatternEvent? peNext = null;
                        if (i + 1 < pe.Count())
                        {
                            peNext = pe.ElementAt(i + 1);
                        }

                        if (time < peStart.Time && available)
                        {
                            return track;
                        }
                        else if (time >= peStart.Time && time < peNext?.Time && peStart.Value != BuzzNote.Off)
                        {
                            available = false;
                        }
                        else if (time >= peStart.Time && time < peNext?.Time && peStart.Value == BuzzNote.Off)
                        {
                            available = true;
                        }
                    }
                }
            }

            return m.TrackCount - 1;
        }
    }
}
