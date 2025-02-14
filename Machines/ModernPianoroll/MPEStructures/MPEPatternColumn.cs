using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using static WDE.ModernPatternEditor.PlayRecordManager;

namespace WDE.ModernPatternEditor.MPEStructures
{
    public class MPEPatternColumn
    {
        public static readonly int BUZZ_TICKS_PER_BEAT = 4;

        public MPEPattern MPEPattern { get; }
        public Action<IEnumerable<PatternEvent>, bool> EventsChanged { get; internal set; }
        public IMachine Machine { get; internal set; }

        // public int IndexInGroup { get; internal set; }
        public int ParamTrack { get; internal set; }
        public bool Graphical { get; internal set; }
        //public string MachineName { get; internal set; }

        List<PatternEvent> eventList = new List<PatternEvent>();

        // This is only for loading/saving pxp format
        // public int ParamIndex { get; internal set; }
        public List<int> BeatRowsList { get; private set; }
        public ParameterGroupType GroupType { get; internal set; }
        public IParameter Parameter { get; internal set; }
        public object VisualIndexInGroup
        {
            get
            {
                if (Parameter is MPEInternalParameter)
                {
                    return PatternEditorUtils.LastMidiTrackParameter - Parameter.IndexInGroup - 1;
                }
                else
                {
                    return Parameter.IndexInGroup;
                }
            }
        }

        public MPEPatternColumn(MPEPattern pat)
        {
            this.MPEPattern = pat;
            BeatRowsList = new List<int>();
        }

        public IEnumerable<PatternEvent> GetEvents(int start, int end)
        {
            return eventList.ToList().Where(e => e.Time >= start && e.Time < end);
        }

        internal void SetEvents(PatternEvent[] events, bool set, bool play = true)
        {
            int patternLenght = int.MaxValue;
            var pattern = MPEPattern.Pattern;
            if (pattern != null)
                patternLenght = pattern.Length * PatternEvent.TimeBase;

            // Paste might introduce wrong numbers for notes, so check.
            if (Parameter.Type == ParameterType.Note)
            {
                for (int i = 0; i < events.Length; i++)
                {
                    int value = events[i].Value;
                    if (value != BuzzNote.Off)
                    {
                        int o = value >> 4;
                        if ((value & 15) < 1 || (value & 15) > 12 || o < BuzzNote.MinOctave || o > BuzzNote.MaxOctave)
                        {
                            // convert from midinote
                            value = Math.Max(0, Math.Min(value, 119));
                            value = BuzzNote.FromMIDINote(value);
                        }
                    }
                    events[i].Value = value;
                }
            }

            if (!set)
            {
                foreach (var e in events)
                    eventList.Remove(e);
            }
            else
            {
                foreach (var e in events)
                {
                    if (e.Time < patternLenght)
                        eventList.Add(e);
                }
            }

            EventsChanged?.Invoke(events, set);
        }

        internal void SetEvents(PatternEvent[] events, int timeOffset, bool set)
        {
            for (int i = 0; i < events.Length; i++)
            {
                events[i].Time += timeOffset;
            }

            SetEvents(events, set);
        }

        int BuzzTicksToBeats(int x)
        {
            return (x + BUZZ_TICKS_PER_BEAT - 1) / BUZZ_TICKS_PER_BEAT;
        }

        internal void UpdateLength()
        {
            int newNumbBeats = BuzzTicksToBeats(MPEPattern.Pattern.Length);
            List<int> newBeatRows = new List<int>();

            for (int i = 0; i < newNumbBeats; i++)
            {
                if (i < BeatRowsList.Count)
                {
                    newBeatRows.Add(BeatRowsList[i]);
                }
                else
                    newBeatRows.Add(MPEPattern.RowsPerBeat);
            }

            BeatRowsList = newBeatRows;
        }

        internal void PlayColumnEvents(IPattern pat, int start, int end, List<CollectedEventState> collectEvents)
        {
            var events = GetEvents(start, end).ToArray();
            {
                foreach (var e in events)
                {
                    //Parameter.SetValue(ParamTrack, e.Value);
                    collectEvents.Add(new CollectedEventState() { parameter = Parameter, time = e.Time, ParamTrack = ParamTrack, value = e.Value, eventType = CollectedEventType.Buzz });
                }
            }
        }

        internal void SetRPB(int rpb)
        {
            if (BeatRowsList.Count == 0)
            {
                for (int i = 0; i < BuzzTicksToBeats(MPEPattern.Pattern.Length); i++)
                    BeatRowsList.Add(rpb);
            }
            else
            {
                for (int i = 0; i < BeatRowsList.Count; i++)
                {
                    BeatRowsList[i] = rpb;
                }
            }
        }

        /*
        internal void ClearRegion(int regionLenght, int offsetFromTop, Digit topLeftDigit)
        {
            for (int i = 0; i < eventList.Count; i++)
            {
                PatternEvent e = eventList[i];
                if (e.Time >= offsetFromTop && e.Time < offsetFromTop + regionLenght)
                {
                    eventList.RemoveAt(i);
                    i--;
                }
            }

            // Need to refresh editor view
            //topLeftDigit.PatternVM.Editor.InvalidateVisual();
            HashSet<int> beats = new HashSet<int>();
            Digit iterator = topLeftDigit;
            while (true)
            {
                beats.Add(iterator.Beat);
                if (iterator.ParameterColumn.GetDigitTime(iterator) >= offsetFromTop + regionLenght ||
                    iterator.IsLastBeat)
                    break;
                iterator = iterator.NextBeat;
            }
            topLeftDigit.ParameterColumn.Set.FireBeatsInvalidated(beats);
        }

        internal void Quantize(Digit startBeat, Digit endBeat)
        {
            HashSet<int> beats = new HashSet<int>();
            Digit iterator = startBeat.FirstRowInBeat;
            Digit iteratorEnd = endBeat.FirstRowInBeat;
            while (true)
            {
                beats.Add(iterator.Beat);
                int start = startBeat.ParameterColumn.GetDigitTime(iterator);
                //int numbeats = BuzzTicksToBeats(MPEPattern.Pattern.Length);
                int beatTime = PatternControl.BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase;
                int end = start + beatTime;
                var events = GetEvents(start, end);

                Dictionary<int, PatternEvent> cleanedEvents = new Dictionary<int, PatternEvent>();

                for (int i = 0; i < events.Count(); i++)
                {
                    var e = events.ElementAt(i);
                    Digit nearestRow = startBeat.NearestRow(e.Time - start);
                    int rowTime = beatTime / startBeat.PatternVM.GetBeat(startBeat).Rows.Count;
                    e.Time = nearestRow.RowInBeat * rowTime + start;
                    cleanedEvents[e.Time] = e;
                }

                this.ClearRegion(end - start, start, iterator);
                this.SetEvents(cleanedEvents.Values.ToArray(), true);

                if (iterator.TimeInBeat >= iteratorEnd.TimeInBeat)
                    break;

                iterator = iterator.NextBeat;
            }
            startBeat.ParameterColumn.Set.FireBeatsInvalidated(beats);
        }
        */
        public int GetTimeQuantized(int time)
        {
            //int beats = BuzzTicksToBeats(MPEPattern.Pattern.Length);
            int beatTime = BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase;
            int beat = time / beatTime;
            int beatRows = beat < BeatRowsList.Count ? BeatRowsList[beat] : BeatRowsList[BeatRowsList.Count - 1];
            double rowTime = beatTime / (double)beatRows;
            int row = (int)((time - beat * beatTime) / rowTime);

            return (int)(beat * beatTime + row * rowTime);
        }

        public int GetTimeQuantized(int time, int rpb)
        {
            //int beats = BuzzTicksToBeats(MPEPattern.Pattern.Length);
            int beatTime = BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase;
            int beat = time / beatTime;
            double rowTime = beatTime / rpb;
            int row = (int)((time - beat * beatTime) / rowTime);

            return (int)(beat * beatTime + row * rowTime);
        }

        internal void SetBeats(List<int> beatRows)
        {
            this.BeatRowsList = beatRows;
        }


        internal double GetRelativeTimeInRow(int time)
        {
            //int beats = MPEPattern.Pattern.Length / MPEPattern.RowsPerBeat;
            int beatTime = BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase;
            int beat = time / beatTime;
            double rowTime = beatTime / BeatRowsList[beat];
            int row = (int)((time - beat * beatTime) / rowTime);

            return (time - beat * beatTime) / rowTime - row;
        }

        internal void SetBeatCount(int beats)
        {
            int newNumbBeats = beats;
            List<int> newBeatRows = new List<int>();

            for (int i = 0; i < newNumbBeats; i++)
            {
                if (i < BeatRowsList.Count)
                {
                    newBeatRows.Add(BeatRowsList[i]);
                }
                else
                    newBeatRows.Add(MPEPattern.RowsPerBeat);
            }

            BeatRowsList = newBeatRows;
        }

        internal MPEPatternColumn Clone(MPEPattern pattern)
        {
            var column = new MPEPatternColumn(pattern);
            //column.MachineName = MachineName;
            column.Machine = Machine;
            column.Graphical = Graphical;
            column.GroupType = GroupType;
            // column.ParamIndex = ParamIndex;
            // column.IndexInGroup = IndexInGroup;
            column.Parameter = Parameter;
            column.ParamTrack = ParamTrack;

            return column;

        }

        internal int RowLenghtAt(int time)
        {
            //int beats = BuzzTicksToBeats(MPEPattern.Pattern.Length);
            int beatTime = BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase;
            int beat = time / beatTime;
            int beatRows = beat < BeatRowsList.Count ? BeatRowsList[beat] : BeatRowsList[BeatRowsList.Count - 1];
            int rowTime = beatTime / beatRows;

            return rowTime;
        }

        internal IEnumerable<PatternEvent> GetEventsQuantized(int start, int end, int rpb)
        {
            var events = eventList.Where(e => e.Time >= start && e.Time < end).ToArray();
            for (int i = 0; i < events.Length; i++)
            {
                events[i].Time = GetTimeQuantized(events[i].Time, rpb);
            }
            return events;
        }

        internal int GetRelativeTimeInRowToParamValue(int playPosition)
        {
            double relativePos = GetRelativeTimeInRow(playPosition);
            return (int)(relativePos * (Parameter.MaxValue - Parameter.MinValue)) + Parameter.MinValue;
        }

        internal void Clear()
        {
            eventList.Clear();
            var notes = MPEPattern.PianorollEditor.pattern.GetAllNotes(this.ParamTrack);
            MPEPattern.PianorollEditor.pattern.DeleteNotes(ParamTrack, notes);
        }
    }
}
