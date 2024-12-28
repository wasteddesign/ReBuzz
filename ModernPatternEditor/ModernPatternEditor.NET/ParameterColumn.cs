using BuzzGUI.Common;
using BuzzGUI.Common.Actions;
using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using WDE.ModernPatternEditor.Actions;
using WDE.ModernPatternEditor.MPEStructures;

namespace WDE.ModernPatternEditor
{
    public class ParameterColumn : ColumnRenderer.IColumn
    {
        static int BeatDuration = 4 * PatternEvent.TimeBase;

        internal readonly ParameterColumnSet Set;
        internal readonly IPatternColumn PatternColumn;
        readonly int track;
        private readonly MPEPatternColumn mpeColumn;

        class Beat : ColumnRenderer.IBeat
        {
            readonly int index;
            readonly ParameterColumn column;
            public IList<string> ValueStrings { get { return Values.Select(v => ValueToString(v)).ToArray(); } }
            readonly int[] rowTimes;        // actual times, not necessarily the same as visual times
            public IList<int> RowVisualTimes { get { return rowTimes; } }
            readonly int RowsPerBeat;

            public IList<ColumnRenderer.BeatRow> Rows
            {
                get
                {
                    var rows = new ColumnRenderer.BeatRow[rowTimes.Length];
                    var values = Values;
                    var vstrings = ValueStrings;
                    var types = ValueTypes.ToArray();
                    var vtimes = RowVisualTimes;

                    for (int i = 0; i < rows.Length; i++)
                    {
                        rows[i].Type = types[i];
                        rows[i].Value = values[i];
                        rows[i].ValueString = vstrings[i];
                        rows[i].VisualTime = vtimes[i];
                        rows[i].Time = rowTimes[i];
                    }

                    return rows;
                }
            }

            IEnumerable<ColumnRenderer.BeatValueType> ValueTypes
            {
                get
                {
                    var v = Values;

                    return Enumerable.Range(0, RowsPerBeat).Select(i =>
                    {
                        if (v[i] == column.PatternColumn.Parameter.NoValue) return ColumnRenderer.BeatValueType.NoValue;
                        if (column.PatternColumn.Parameter.Type == ParameterType.Note) return ColumnRenderer.BeatValueType.Note;
                        else return ColumnRenderer.BeatValueType.Parameter;
                    });
                }
            }

            string ValueToString(int v)
            {
                if (v == column.PatternColumn.Parameter.NoValue)
                    return new string('.', column.DigitCount);

                return column.PatternColumn.Parameter.GetHexValueString(v);
            }

            IList<int> Values
            {
                get
                {
                    int[] values = new int[RowsPerBeat];
                    for (int i = 0; i < values.Length; i++) values[i] = column.PatternColumn.Parameter.NoValue;

                    int t = index * BeatDuration;
                    //foreach (var e in column.PatternColumn.GetEvents(t, t + BeatDuration).Where(e => t % PatternEvent.TimeBase == 0))
                    //						values[(e.Time - t) / PatternEvent.TimeBase] = e.Value;

                    // Get event data from our own structure
                    MPEPattern mpePattern = column.Set.Pattern.Editor.MPEPatternsDB.GetMPEPattern(column.Set.Pattern.Pattern);
                    MPEPatternColumn mpeColumn = mpePattern.GetColumn(column.PatternColumn);
                    if (mpeColumn != null)
                    {
                        foreach (var e in mpeColumn.GetEvents(t, t + BeatDuration).Where(e => rowTimes.Contains(e.Time - t)))
                            values[Array.IndexOf(rowTimes, e.Time - t)] = e.Value;
                    }

                    return values;
                }
            }

            public int GetRowTime(int row)
            {
                return index * BeatDuration + rowTimes[row];
            }

            public bool HasValue(int row)
            {
                int t = GetRowTime(row);
                MPEPattern mpePattern = column.Set.Pattern.Editor.MPEPatternsDB.GetMPEPattern(column.Set.Pattern.Pattern);
                MPEPatternColumn mpeColumn = mpePattern.GetColumn(column.PatternColumn);
                return mpeColumn.GetEvents(t, t + 1).Any();
            }

            public BuzzAction SetValue(int row, int value)
            {
                if (column.PatternColumn.Parameter.Type != ParameterType.Note || value != BuzzNote.Off)
                {
                    value = Math.Min(Math.Max(value, column.PatternColumn.Parameter.MinValue), column.PatternColumn.Parameter.MaxValue);
                }

                if (column.PatternColumn.Type == PatternColumnType.MIDI)
                {
                    MPEInternalParameter iParameter = (MPEInternalParameter)column.PatternColumn.Parameter;
                    iParameter.SetValue(column.track, value);
                }
                /*
				if (column.PatternColumn.Parameter.Type == ParameterType.Note && value != BuzzNote.Off)
                {
					int o = value >> 4;
					if ((value & 15) < 1 || (value & 15) > 12 || o < BuzzNote.MinOctave || o > BuzzNote.MaxOctave)
                    {
						// convert from midinote
						value = Math.Max(0, Math.Min(value, 119));
						value = BuzzNote.FromMIDINote(value);
                    }
                }
				*/
                int t = GetRowTime(row);
                MPEPattern mpePattern = column.Set.Pattern.Editor.MPEPatternsDB.GetMPEPattern(column.Set.Pattern.Pattern);
                MPEPatternColumn mpeColumn = mpePattern.GetColumn(column.PatternColumn);
                return new MPESetOrClearEventsAction(mpePattern.Pattern, mpeColumn, new[] { new PatternEvent(t, value) }, true);
            }

            public BuzzAction ClearValue(int row)
            {
                int t = GetRowTime(row);
                MPEPattern mpePattern = column.Set.Pattern.Editor.MPEPatternsDB.GetMPEPattern(column.Set.Pattern.Pattern);
                MPEPatternColumn mpeColumn = mpePattern.GetColumn(column.PatternColumn);
                return new MPESetOrClearEventsAction(mpePattern.Pattern, mpeColumn, mpeColumn.GetEvents(t, t + 1), false);
            }

            public void SendCCAtTime(int time)
            {
                int row = Array.IndexOf(rowTimes, time);
                if (row < 0 || !HasValue(row)) return;

                if (column.PatternColumn.Type == PatternColumnType.MIDI)
                {
                    MPEInternalParameter iParameter = (MPEInternalParameter)column.PatternColumn.Parameter;
                    if (iParameter.InternalType == PatternEditorUtils.InternalParameter.MidiNote)
                    {
                        int newValue = Values[row];
                        // ToDo: fetch volume from vol column
                        this.column.Set.Pattern.Editor.playRecordManager.UpdatePlayingNotePattern(iParameter, column.PatternColumn.Track, newValue, MPEInternalParameter.DefaultMidiVolume);
                        iParameter.SetValue(column.track, newValue);
                    }
                }
                else
                {
                    var machine = column.PatternColumn.Machine;
                    int group = column.PatternColumn.Machine.ParameterGroups.IndexOf(this.column.PatternColumn.Parameter.Group) | 16; // Don't record
                    column.Set.Pattern.Editor.cb.ControlChange(machine, group, this.column.track, this.column.PatternColumn.Parameter.IndexInGroup, Values[row]);
                    //column.PatternColumn.Parameter.SetValue(column.track, Values[row]);
                }
            }

            internal Beat(ParameterColumn column, int index)
            {
                this.column = column;
                this.index = index;

                // Use our own structure
                RowsPerBeat = column.mpeColumn.BeatRowsList.Count > index ? column.mpeColumn.BeatRowsList[index] : 4;

                // if (!column.PatternColumn.BeatSubdivision.TryGetValue(index, out RowsPerBeat))
                //	RowsPerBeat = rpb;

                rowTimes = Enumerable.Range(0, RowsPerBeat).Select(t => PatternEvent.TimeBase * 4 * t / RowsPerBeat).ToArray();
            }
        }

        public ColumnRenderer.ColumnType Type
        {
            get
            {
                if (PatternColumn.Parameter.Type == ParameterType.Note)
                    return ColumnRenderer.ColumnType.Note;
                else
                    return PatternColumn.Parameter.Flags.HasFlag(ParameterFlags.Ascii)
                        ? ColumnRenderer.ColumnType.Ascii : ColumnRenderer.ColumnType.HexValue;
            }
        }

        public int DigitCount
        {
            get
            {
                switch (PatternColumn.Parameter.Type)
                {
                    case ParameterType.Note: return 3;
                    case ParameterType.Word: return 4;
                    case ParameterType.Switch: return 1;
                    default: return PatternColumn.Parameter.Flags.HasFlag(ParameterFlags.Ascii) ? 1 : 2;
                }
            }
        }

        public bool TiedToNext
        {
            get
            {
                return PatternColumn.Parameter.Flags.HasFlag(ParameterFlags.TiedToNext);
            }
        }

        public string Label { get { return PatternColumn.Parameter.Name; } }
        public string Description { get { return PatternColumn.Parameter.Description; } }
        public ColumnRenderer.IBeat FetchBeat(int index) { return new Beat(this, index); }

        public ParameterColumn(ParameterColumnSet set, IPatternColumn pc, int track, int rpb)
        {
            BeatDuration = 4 * PatternEvent.TimeBase;
            this.Set = set;
            this.PatternColumn = pc;
            this.track = track;
            MPEPattern mPEPattern = Set.Pattern.Editor.MPEPatternsDB.GetMPEPattern(pc.Pattern);
            this.mpeColumn = mPEPattern.GetColumnOrCreate(PatternColumn);
            mpeColumn.EventsChanged += pc_EventsChanged;
            pc.BeatSubdivisionChanged += pc_BeatSubdivisionChanged;
        }

        public void Release()
        {
            // PatternColumn.EventsChanged -= pc_EventsChanged;
            mpeColumn.EventsChanged -= pc_EventsChanged;
            PatternColumn.BeatSubdivisionChanged -= pc_BeatSubdivisionChanged;
        }

        void pc_EventsChanged(IEnumerable<PatternEvent> events, bool setorclear)
        {
            HashSet<int> hs = new HashSet<int>();
            var invalidbeats = events.Select(e => e.Time / (PatternControl.BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase));
            foreach (var e in invalidbeats)
                hs.Add(e);
            Set.FireBeatsInvalidated(hs);
        }

        void pc_BeatSubdivisionChanged(int index)
        {
            var invalidbeats = new HashSet<int>();
            invalidbeats.Add(index);
            Set.FireBeatsInvalidated(invalidbeats);
            Set.Pattern.CursorPosition = Set.Pattern.CursorPosition.Constrained;
        }

        public int GetDigitTime(Digit d)
        {
            var beat = FetchBeat(d.Beat) as Beat;
            return beat.GetRowTime(d.RowInBeat);
        }

        public IEnumerable<BuzzAction> EditDigit(Digit d, int newvalue)
        {
            var beat = FetchBeat(d.Beat) as Beat;

            if (newvalue < 0)
            {
                return LinqExtensions.Return(beat.ClearValue(d.RowInBeat));
            }
            else
            {
                int value;

                if (DigitCount == 3)
                {
                    if (d.Index == 0)
                        value = newvalue;
                    else if (beat.HasValue(d.RowInBeat) && beat.Rows[d.RowInBeat].Value != BuzzNote.Off)
                        value = (beat.Rows[d.RowInBeat].Value & 15) | (newvalue << 4);
                    else
                        return null;
                }
                else if (DigitCount == 2)
                {
                    if (beat.HasValue(d.RowInBeat))
                        value = (beat.Rows[d.RowInBeat].Value & (0x0f << (d.Index * 4))) | (newvalue << ((d.Index ^ 1) * 4));
                    else
                        value = (newvalue << ((d.Index ^ 1) * 4));
                }
                else if (DigitCount == 4)
                {
                    if (beat.HasValue(d.RowInBeat))
                        value = (beat.Rows[d.RowInBeat].Value & (~(0x0f << ((3 - d.Index) * 4)))) | (newvalue << ((3 - d.Index) * 4));
                    else
                        value = (newvalue << ((3 - d.Index) * 4));
                }
                else if (DigitCount == 1)
                {
                    value = newvalue;
                }
                else
                {
                    return null;
                }

                return LinqExtensions.Return(beat.SetValue(d.RowInBeat, value));
            }
        }

        public IEnumerable<BuzzAction> EditValue(Digit d, int newvalue)
        {
            var beat = FetchBeat(d.Beat) as Beat;

            if (newvalue < 0)
                return LinqExtensions.Return(beat.ClearValue(d.RowInBeat));
            else
                return LinqExtensions.Return(beat.SetValue(d.RowInBeat, newvalue));
        }

        public void SendCCAtTime(int time)
        {
            int ibeat = time / BeatDuration;
            if (ibeat < 0 || ibeat >= Set.BeatCount) return;
            var beat = FetchBeat(ibeat) as Beat;
            beat.SendCCAtTime(time % BeatDuration);
        }

        public Digit GetDigitAtTime(int time)
        {
            Digit digit = new Digit(Set.Pattern, Set.Pattern.ColumnSets.IndexOf(Set), this.Set.Columns.IndexOf(this), 0, 0, 0);
            int ibeat = time / BeatDuration;
            if (ibeat < 0 || ibeat >= Set.BeatCount) return digit;
            var beat = FetchBeat(ibeat) as Beat;
            digit = digit.SetBeat(ibeat);
            int rowTime = BeatDuration / beat.Rows.Count;
            digit = digit.SetRowInBeat((time - ibeat * BeatDuration) / rowTime);
            return digit;
        }

    }
}
