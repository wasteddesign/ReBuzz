using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using WDE.ModernPatternEditor.MPEStructures;

namespace WDE.ModernPatternEditor
{
    public static class PatternEditorUtils
    {
        static readonly byte PATTERNXP_DATA_VERSION = 3;
        static readonly byte NOT_PATTERNXP_DATA = 255;
        static readonly byte MODERN_PATTERN_EDITOR_DATA_VERSION = 1;

        public enum InternalParameter
        {
            SPGlobalTrigger = -10,
            SPGlobalEffect1 = -11,
            SPGlobalEffect1Data = -12,

            //FirstInternalTrackParameter = -101,
            SPTrackTrigger = -110,
            SPTrackEffect1 = -111,
            SPTrackEffect1Data = -112,

            //FirstMidiTrackParameter = -128,
            MidiNote = -128,
            MidiVelocity = -129,
            MidiNoteDelay = -130,
            MidiNoteCut = -131,
            MidiPitchWheel = -132,
            MidiCC = -133
        };

        public static readonly int LastMidiTrackParameter = -133;


        public static List<MPEPattern> ProcessEditorData(PatternEditor editor, byte[] data)
        {
            List<MPEPattern> patterns = new List<MPEPattern>();
            if (data.Length == 0)
                return patterns;

            int index = 0;
            // Decode pattern editor data
            byte[] machinePatternsData = data;

            byte version = machinePatternsData[index++];

            if (index >= data.Length)
                return patterns;

            if (version >= 1 && version <= PATTERNXP_DATA_VERSION)
            {
                return ParsePatternXPData(editor, machinePatternsData, index, version);
            }
            else if (version == NOT_PATTERNXP_DATA)
            {
                byte mpeVersion = machinePatternsData[index++];
                index += sizeof(int); // Skip data size
                if (mpeVersion == MODERN_PATTERN_EDITOR_DATA_VERSION)
                    return ParseModernPatternEditorData(editor, machinePatternsData, index, version);
            }
            return patterns;
        }

        #region PatternXPData Read
        private static List<MPEPattern> ParsePatternXPData(PatternEditor editor, byte[] machinePatternsData, int index, byte version)
        {
            List<MPEPattern> patterns = new List<MPEPattern>();
            int numpat = BCReadInt(machinePatternsData, ref index);

            for (int i = 0; i < numpat; i++)
            {
                string name = GetStringFromByteArray(machinePatternsData, ref index);
                MPEPattern mpePattern = new MPEPattern(editor, name);
                mpePattern.PatternName = name;
                LoadPattern(mpePattern, machinePatternsData, ref index, version);

                patterns.Add(mpePattern);
            }

            return patterns;
        }

        private static void LoadPattern(MPEPattern pat, byte[] machinePatternsData, ref int index, byte ver)
        {
            int rowsPerBeat = 4;
            if (ver > 1)
                rowsPerBeat = BCReadInt(machinePatternsData, ref index);

            int count = BCReadInt(machinePatternsData, ref index);

            pat.RowsPerBeat = rowsPerBeat;

            for (int i = 0; i < count; i++)
            {
                LoadColumn(pat, machinePatternsData, ref index, ver);
            }
        }

        public static void LoadColumn(MPEPattern pat, byte[] machinePatternsData, ref int index, byte ver)
        {
            MPEPatternColumn mpeColumn = new MPEPatternColumn(pat);

            string machineName = GetStringFromByteArray(machinePatternsData, ref index);
            machineName = GetMachineCurrentName(machineName);
            int paramIndex = BCReadInt(machinePatternsData, ref index); // Can be negative if internal param
            int paramTrack = BCReadInt(machinePatternsData, ref index);
            bool graphical = false;

            if (ver >= 3)
            {
                graphical = BitConverter.ToBoolean(machinePatternsData, index);
                index += sizeof(bool);
            }

            mpeColumn.Machine = Global.Buzz.Song.Machines.FirstOrDefault(x => x.Name == machineName);
            mpeColumn.ParamTrack = paramTrack;
            mpeColumn.Graphical = graphical;

            var parameter = GetParameter(mpeColumn.Machine, paramIndex, paramTrack);
            mpeColumn.Parameter = parameter;

            if (parameter != null)
            {
                mpeColumn.GroupType = parameter.Group.Type;
            }

            int count = BCReadInt(machinePatternsData, ref index);

            List<PatternEvent> events = new List<PatternEvent>();
            for (int i = 0; i < count; i++)
            {
                int eventTime = BCReadInt(machinePatternsData, ref index);
                int eventValue = BCReadInt(machinePatternsData, ref index);

                PatternEvent pe = new PatternEvent();
                pe.Time = eventTime * PatternEvent.TimeBase * 4 / pat.RowsPerBeat;
                pe.Value = eventValue;

                events.Add(pe);
            }

            if (parameter != null)
            {
                mpeColumn.SetEvents(events.ToArray(), true);
                pat.MPEPatternColumns.Add(mpeColumn);
            }
        }

        #endregion

        #region Modern Pattern Editor Data Read
        private static List<MPEPattern> ParseModernPatternEditorData(PatternEditor editor, byte[] machinePatternsData, int index, byte version)
        {
            List<MPEPattern> patterns = new List<MPEPattern>();
            int numpat = BCReadInt(machinePatternsData, ref index);

            for (int i = 0; i < numpat; i++)
            {
                string name = GetStringFromByteArray(machinePatternsData, ref index);
                MPEPattern mpePattern = new MPEPattern(editor, name);
                mpePattern.PatternName = name;
                LoadModernPatternEditorPattern(mpePattern, machinePatternsData, ref index, version);

                patterns.Add(mpePattern);
            }

            return patterns;
        }


        private static void LoadModernPatternEditorPattern(MPEPattern pat, byte[] machinePatternsData, ref int index, byte ver)
        {
            int numberOfBeats = BCReadInt(machinePatternsData, ref index);
            int rowsPerBeat = BCReadInt(machinePatternsData, ref index);

            int count = BCReadInt(machinePatternsData, ref index);

            pat.RowsPerBeat = rowsPerBeat;

            for (int i = 0; i < count; i++)
            {
                LoadModernPatternEditorColumn(pat, machinePatternsData, ref index, ver, numberOfBeats);
            }

        }

        public static void LoadModernPatternEditorColumn(MPEPattern pat, byte[] machinePatternsData, ref int index, byte ver, int numberOfBeats)
        {
            MPEPatternColumn mpeColumn = new MPEPatternColumn(pat);

            string machineName = GetStringFromByteArray(machinePatternsData, ref index);
            machineName = GetMachineCurrentName(machineName);
            int paramIndex = BCReadInt(machinePatternsData, ref index);
            int paramTrack = BCReadInt(machinePatternsData, ref index);
            bool graphical = false;

            if (ver >= 3)
            {
                graphical = BitConverter.ToBoolean(machinePatternsData, index);
                index += sizeof(bool);
            }

            mpeColumn.Machine = Global.Buzz.Song.Machines.FirstOrDefault(x => x.Name == machineName);
            mpeColumn.ParamTrack = paramTrack;
            mpeColumn.Graphical = graphical;

            var parameter = GetParameter(mpeColumn.Machine, paramIndex, paramTrack);
            mpeColumn.GroupType = parameter.Group.Type;
            mpeColumn.Parameter = parameter;

            int count = BCReadInt(machinePatternsData, ref index);

            List<PatternEvent> events = new List<PatternEvent>();
            for (int i = 0; i < count; i++)
            {
                int first = BCReadInt(machinePatternsData, ref index);
                int second = BCReadInt(machinePatternsData, ref index);

                PatternEvent pe = new PatternEvent();
                pe.Time = first;
                pe.Value = second;
                events.Add(pe);
            }

            mpeColumn.SetEvents(events.ToArray(), true);

            List<int> beatRows = new List<int>();
            // Read rows in beat
            for (int i = 0; i < numberOfBeats; i++)
            {
                beatRows.Add(BCReadInt(machinePatternsData, ref index));
            }

            mpeColumn.SetBeats(beatRows);

            pat.MPEPatternColumns.Add(mpeColumn);
        }



        #endregion

        #region PatternXPData Write
        public static byte[] CreatePatternXPPatternData(IEnumerable<MPEPattern> patterns)
        {
            // Encode pattern editor data
            List<byte> data = new List<byte>();

            data.Add(PATTERNXP_DATA_VERSION);

            int numpat = patterns.Count();
            data.AddRange(BitConverter.GetBytes(numpat));

            for (int i = 0; i < numpat; i++)
            {
                MPEPattern mpePattern = patterns.ElementAt(i);
                data.AddRange(Encoding.ASCII.GetBytes(mpePattern.PatternName));
                data.Add(0);

                CreatePatternData(mpePattern, data, PATTERNXP_DATA_VERSION);
            }
            
            byte[] dataret = data.ToArray();
            return dataret;
        }

        private static void CreatePatternData(MPEPattern pat, List<byte> data, byte ver)
        {
            if (ver > 1)
                data.AddRange(BitConverter.GetBytes(pat.RowsPerBeat));

            int count = pat.MPEPatternColumns.Count;
            data.AddRange(BitConverter.GetBytes(count));

            for (int i = 0; i < count; i++)
            {
                SaveColumn(pat.MPEPatternColumns[i], data, ver, pat.RowsPerBeat);
            }
        }

        public static void SaveColumn(MPEPatternColumn mpeColumn, List<byte> data, byte ver, int rpb)
        {
            data.AddRange(Encoding.ASCII.GetBytes(mpeColumn.Machine.Name));
            data.Add(0);
            data.AddRange(BitConverter.GetBytes(GetParamIndex(mpeColumn.Parameter, mpeColumn.ParamTrack)));
            data.AddRange(BitConverter.GetBytes(mpeColumn.ParamTrack));
            data.Add(Convert.ToByte(mpeColumn.Graphical));

            IEnumerable<PatternEvent> events = mpeColumn.GetEventsQuantized(0, int.MaxValue, rpb);
            data.AddRange(BitConverter.GetBytes(events.Count()));

            foreach (var e in events.OrderBy(x => x.Time).ToList())
            {
                int time = (int)(e.Time / (PatternEvent.TimeBase * 4 / mpeColumn.MPEPattern.RowsPerBeat));
                data.AddRange(BitConverter.GetBytes(time));
                data.AddRange(BitConverter.GetBytes(e.Value));
            }
        }
        #endregion

        #region Modern Pattern Editor Data Write

        private static IParameter GetParameter(IMachine machine, int paramIndex, int paramTrack)
        {
            IParameter ret = null;


            if (paramIndex == (int)InternalParameter.MidiNote)
            {
                ret = MPEInternalParameter.GetInternalParameter(machine, InternalParameter.MidiNote);
            }
            else if (paramIndex == (int)InternalParameter.MidiVelocity)
            {
                ret = MPEInternalParameter.GetInternalParameter(machine, InternalParameter.MidiVelocity);
            }
            else if (paramIndex == (int)InternalParameter.MidiNoteDelay)
            {
                ret = MPEInternalParameter.GetInternalParameter(machine, InternalParameter.MidiNoteDelay);
            }
            else if (paramIndex == (int)InternalParameter.MidiNoteCut)
            {
                ret = MPEInternalParameter.GetInternalParameter(machine, InternalParameter.MidiNoteCut);
            }
            else if (paramIndex == (int)InternalParameter.MidiPitchWheel)
            {
                ret = MPEInternalParameter.GetInternalParameter(machine, InternalParameter.MidiPitchWheel);
            }
            else if (paramIndex == (int)InternalParameter.MidiCC)
            {
                ret = MPEInternalParameter.GetInternalParameter(machine, InternalParameter.MidiCC);
            }
            else if (paramIndex >= 0)
            {
                int gourp0ParamsCount = machine.ParameterGroups[0].Parameters.Count;
                int gourp1ParamsCount = machine.ParameterGroups[1].Parameters.Count;
                int gourp2ParamsCount = machine.ParameterGroups[2].Parameters.Count;

                if (paramIndex < gourp1ParamsCount && paramTrack == 0)
                {
                    // Global
                    ret = machine.ParameterGroups[1].Parameters[paramIndex];
                }
                else
                {   
                    int index = paramIndex - gourp1ParamsCount;
                    if (index < machine.ParameterGroups[2].Parameters.Count)
                    {
                        ret = machine.ParameterGroups[2].Parameters[index];
                    }
                }
            }

            return ret;
        }

        public static byte[] CreateModernPatternEditorData(IEnumerable<MPEPattern> patterns)
        {
            // Encode pattern editor data
            List<byte> data = new List<byte>();

            data.Add(NOT_PATTERNXP_DATA);
            data.Add(MODERN_PATTERN_EDITOR_DATA_VERSION);

            int numpat = patterns.Count();
            data.AddRange(BitConverter.GetBytes(numpat));

            for (int i = 0; i < numpat; i++)
            {
                MPEPattern mpePattern = patterns.ElementAt(i);
                data.AddRange(Encoding.ASCII.GetBytes(mpePattern.PatternName));
                data.Add(0);

                CreateMPEPatternData(mpePattern, data, PATTERNXP_DATA_VERSION);
            }

            int dataSize = data.Count + sizeof(int);
            data.InsertRange(2, BitConverter.GetBytes(dataSize));

            byte[] dataret = data.ToArray();
            return dataret;
        }
        private static void CreateMPEPatternData(MPEPattern pat, List<byte> data, byte ver)
        {
            // MPE Beats
            data.AddRange(BitConverter.GetBytes((int)(pat.Pattern.Length + PatternControl.BUZZ_TICKS_PER_BEAT - 1) / PatternControl.BUZZ_TICKS_PER_BEAT));
            data.AddRange(BitConverter.GetBytes(pat.RowsPerBeat));

            int count = pat.MPEPatternColumns.Count;
            data.AddRange(BitConverter.GetBytes(count));

            for (int i = 0; i < count; i++)
            {
                SaveMPEColumn(pat.MPEPatternColumns[i], data, ver);
            }
        }

        public static void SaveMPEColumn(MPEPatternColumn mpeColumn, List<byte> data, byte ver)
        {
            data.AddRange(Encoding.ASCII.GetBytes(mpeColumn.Machine.Name));
            data.Add(0);
            data.AddRange(BitConverter.GetBytes(GetParamIndex(mpeColumn.Parameter, mpeColumn.ParamTrack)));
            data.AddRange(BitConverter.GetBytes(mpeColumn.ParamTrack));
            data.Add(Convert.ToByte(mpeColumn.Graphical));

            IEnumerable<PatternEvent> events = mpeColumn.GetEvents(0, int.MaxValue);
            data.AddRange(BitConverter.GetBytes(events.Count()));

            foreach (var e in events)
            {
                data.AddRange(BitConverter.GetBytes((int)(e.Time)));
                data.AddRange(BitConverter.GetBytes(e.Value));
            }

            // MPE Save beats

            foreach (var beatRow in mpeColumn.BeatRowsList)
            {
                data.AddRange(BitConverter.GetBytes(beatRow));
            }
        }
        #endregion

        #region Read Write Utils
        private static string GetStringFromByteArray(byte[] machinePatternsData, ref int index)
        {
            string result = "";

            while (true)
            {
                if (machinePatternsData[index] == 0)
                    break;

                result += (char)(machinePatternsData[index]);
                index++;
            }

            index++;

            return result;
        }

        public static int BCReadInt(byte[] data, ref int index)
        {
            int ret = 0;
            if (index < data.Length)
            {
                ret = BitConverter.ToInt32(data, index);
                index += sizeof(int);
            }

            return ret;
        }

        public static IEnumerable<IParameter> GetInternalParameters(IMachine mac)
        {
            List<IParameter> pars = new List<IParameter>();

            pars.Add(MPEInternalParameter.GetInternalParameter(mac, InternalParameter.MidiNote));
            pars.Add(MPEInternalParameter.GetInternalParameter(mac, InternalParameter.MidiVelocity));
            pars.Add(MPEInternalParameter.GetInternalParameter(mac, InternalParameter.MidiNoteDelay));
            pars.Add(MPEInternalParameter.GetInternalParameter(mac, InternalParameter.MidiNoteCut));
            pars.Add(MPEInternalParameter.GetInternalParameter(mac, InternalParameter.MidiPitchWheel));
            pars.Add(MPEInternalParameter.GetInternalParameter(mac, InternalParameter.MidiCC));

            return pars;
        }

        public static int GetParamIndex(IParameter par, int track)
        {
            int index = 0;
            IMachine machine = par.Group.Machine;
            int group = machine.ParameterGroups.IndexOf(par.Group);

            if (par is MPEInternalParameter)
            {
                index = par.IndexInGroup;
            }
            else if (group == 1)
            {
                index = par.IndexInGroup;
            }
            else if (group == 2)
            {
                int group1Size = machine.ParameterGroups[1].Parameters.Count;
                index = group1Size + par.IndexInGroup;
            }
            return index;
        }

        #endregion

        #region MIDI Import Export
        public static int[] ExportMidiEvents(MPEPattern pattern)
        {
            const int MidiTimeBase = 960;

            List<Tuple<int, int, int>> midiEvents = new List<Tuple<int, int, int>>();

            // Todo: delay & cut

            foreach (var mpeColumn in pattern.MPEPatternColumns)
            {
                if (mpeColumn.Parameter.Type == ParameterType.Note)
                {
                    int previousNote = 0;

                    foreach (var e in mpeColumn.GetEvents(0, int.MaxValue).OrderBy(x => x.Time).ToList())
                    {
                        int midiTime = (e.Time / PatternEvent.TimeBase) * MidiTimeBase;
                        if (e.Value != BuzzNote.Off)
                        {
                            int newNote = BuzzNote.ToMIDINote(e.Value);
                            if (previousNote != 0)
                            {
                                // Add note off before new note
                                var msgOff = MIDI.EncodeNoteOff(previousNote);
                                midiEvents.Add(new Tuple<int, int, int>(midiTime, msgOff, previousNote));
                            }
                            var msg = MIDI.EncodeNoteOn(newNote, 70);
                            midiEvents.Add(new Tuple<int, int, int>(midiTime, msg, newNote));
                            previousNote = newNote;
                        }
                        else
                        {
                            var msg = MIDI.EncodeNoteOff(previousNote);
                            midiEvents.Add(new Tuple<int, int, int>(midiTime, msg, previousNote));
                            previousNote = 0;
                        }
                    }
                }
            }

            midiEvents = midiEvents.OrderBy(x => x.Item1).ToList();
            List<int> ordereEvents = new List<int>();
            foreach (var t in midiEvents)
            {
                ordereEvents.Add(t.Item1);
                ordereEvents.Add(t.Item2);
            }
            ordereEvents.Add(-1);
            int[] intArray = ordereEvents.ToArray();
            //byte[] result = new byte[intArray.Length * sizeof(int)];
            //Buffer.BlockCopy(intArray, 0, result, 0, result.Length);

            return intArray;
        }

        internal struct ActiveMidiNote
        {
            internal enum State { note_on_pending, playing, note_off_pending, recording, pw_or_cc };

            internal State state;
            internal int note;
            internal int velocity;
            internal int previousNote;
            internal int delaytime;
            internal int cuttime;
            internal int pw;
            internal int cc;
            internal int rowInBeat;
            internal int RPB;
        };

        // Use pvst param names. ToDo: Make it configurable
        private static string noteParameterName = "note";
        private static string noteVelocityParameterName = "note velocity";
        private static string noteDelayParameterName = "note delay";
        private static string noteCutParameterName = "note cut";
        private static string midiCCParameterName = "MidiCC";
        private static string midiPitchWheelParameterName = "MidiPitchWheel";
        public static bool ImportMidiEvents(MPEPattern pattern, int [] data)
        {
            int tc = pattern.Pattern.Machine.TrackCount;
            if (tc < 1) return false;

            List<MPEPatternColumn> notecol = new List<MPEPatternColumn>();
            List<MPEPatternColumn> velcol = new List<MPEPatternColumn>();
            List<MPEPatternColumn> delaycol = new List<MPEPatternColumn>();
            List<MPEPatternColumn> cutcol = new List<MPEPatternColumn>();

            for (int t = 0; t < tc; t++)
            {
                // try to find these columns
                notecol.Add(GetColumn(pattern, noteParameterName, t));
                velcol.Add(GetColumn(pattern, noteVelocityParameterName, t));
                delaycol.Add(GetColumn(pattern, noteDelayParameterName, t));
                cutcol.Add(GetColumn(pattern, noteCutParameterName, t));
            }

            for (int t = 0; t < tc; t++)
            {   
                notecol[t]?.Clear();
                velcol[t]?.Clear();
                delaycol[t]?.Clear();
                cutcol[t]?.Clear();
            }

            Dictionary<Tuple<IMachine, int>, ActiveMidiNote> notes = new Dictionary<Tuple<IMachine, int>, ActiveMidiNote>();
            int index = 0;
            while (true)
            {
                // we read 2 ints per loop
                if (index + 1 >= data.Length)
                    return false;

                int time = data[index++];
                if (time < 0) break;

                time = time * pattern.RowsPerBeat / (10 * MPEPatternColumn.BUZZ_TICKS_PER_BEAT);

                int mididata = data[index++];

                int row = time / 96;
                int delay = time % 96;

                if (row >= pattern.Pattern.Length) continue;

                int status = mididata & 0xff;
                int data1 = (mididata >> 8) & 0xff;
                int data2 = (mididata >> 16) & 0xff;

                int patternRow = row * PatternEvent.TimeBase;

                if (status == 0x90)
                {
                    int t = AllocateMidiTrack(pattern, row, delay, notes, tc);
                    if (t < 0) continue;

                    if (notecol[t] == null) continue;

                    // note
                    PatternEvent e = new PatternEvent();
                    e.Value = BuzzNote.FromMIDINote(data1);
                    e.Time = patternRow;
                    notecol[t].SetEvents([e], true, false);

                    // velocity
                    e = new PatternEvent();
                    e.Value = data2;
                    e.Time = patternRow;
                    if (velcol[t] != null) velcol[t].SetEvents([e], true, false);

                    // delay
                    e = new PatternEvent();
                    e.Value = delay;
                    e.Time = patternRow;
                    if (delaycol[t] != null && delay != 0) delaycol[t].SetEvents([e], true, false);

                    ActiveMidiNote n = new ActiveMidiNote();
                    n.note = data1;
                    n.velocity = data2;
                    n.state = ActiveMidiNote.State.recording;
                    notes[new Tuple<IMachine, int>(pattern.Pattern.Machine, t)] = n;
                }
                else if (status == 0x80)
                {
                    int t = FreeMidiTrack(pattern, data1, notes);
                    if (t < 0) continue;

                    if (notecol[t] == null) continue;
                    

                    if (notecol[t].GetEvents(patternRow, patternRow + 1).Count() > 0)
                    {
                        PatternEvent e = new PatternEvent();
                        e.Value = delay;
                        e.Time = patternRow;
                        if (cutcol[t] != null && delay != 0) cutcol[t].SetEvents([e], true, false);

                    }
                    else
                    {
                        PatternEvent e = new PatternEvent();
                        e.Value = BuzzNote.Off;
                        e.Time = patternRow;
                        notecol[t].SetEvents([e], true, false);

                        e = new PatternEvent();
                        e.Value = delay;
                        e.Time = patternRow;
                        if (delaycol[t] != null && delay != 0) delaycol[t].SetEvents([e], true, false);
                    }
                }
            }

            return false;
        }

        private static MPEPatternColumn? GetColumn(MPEPattern mpePattern, string name, int t)
        {
            foreach (var c in mpePattern.MPEPatternColumns)
            {
                if (c.ParamTrack == t && c.Parameter.Name.ToLower() == name.ToLower())
                    return c;
            }
            return null;
        }

        private static int AllocateMidiTrack(MPEPattern mpePattern, int row, int delay, Dictionary<Tuple<IMachine, int>, ActiveMidiNote> notes, int tc)
        {
            int patternTime = row * PatternEvent.TimeBase;
            for (int t = 0; t < tc; t++)
            {   
                foreach (var i in notes)
                {
                    if (i.Key.Item2 == t)
                    {
                        break;
                    }
                    else if (i.Key == notes.Last().Key)
                    {
                        var pwcol = GetColumn(mpePattern, midiPitchWheelParameterName, t);
                        if (pwcol != null && pwcol.GetEvents(patternTime, patternTime + 1).Count() > 0) continue;
                        var cccol = GetColumn(mpePattern, midiCCParameterName, t);
                        if (cccol != null && cccol.GetEvents(patternTime, patternTime + 1).Count() > 0) continue;

                        var notecol = GetColumn(mpePattern, noteParameterName, t);
                        if (notecol != null)
                        {
                            if (notecol.GetEvents(patternTime, patternTime + 1).Count() > 0)
                            {
                                if (delay == 0 && notecol.GetEvents(patternTime, patternTime + 1).First().Value == BuzzNote.Off)
                                {
                                    // note off can be overwritten except when it's delayed or cut

                                    var delaycol = GetColumn(mpePattern, noteDelayParameterName, t);
                                    var cutcol = GetColumn(mpePattern, noteCutParameterName, t);

                                    bool dval = delaycol != null && delaycol.GetEvents(patternTime, patternTime + 1).Count() > 0;
                                    bool cval = cutcol != null && cutcol.GetEvents(patternTime, patternTime + 1).Count() > 0;

                                    if (!dval && !cval) return t;
                                }
                            }
                            else
                            {
                                return t;
                            }
                        }
                    }

                }
            }

            return tc > 0 ? 0 : -1;
        }

        private static int FreeMidiTrack(MPEPattern mpePattern, int note, Dictionary<Tuple<IMachine, int>, ActiveMidiNote> notes)
        {
            foreach (var i in notes.Keys.ToArray())
            {
                if (i.Item1 == mpePattern.Pattern.Machine && notes[i].note == note)
                {
                    int t = i.Item2;
                    notes.Remove(i);
                    return t;
                }
            }

            return -1;
        }

        #endregion

        #region Other

        internal static ResourceDictionary GetBuzzThemeSettingsWindowResources()
        {
            ResourceDictionary skin = new ResourceDictionary();

            try
            {
                string selectedTheme = Global.Buzz.SelectedTheme == "<default>" ? "Default" : Global.Buzz.SelectedTheme;
                string skinPath = Global.BuzzPath + "\\Themes\\" + selectedTheme + "\\ModernPatternEditor\\PatternPropertiesWindow.xaml";
                //string skinPath = "..\\..\\..\\Themes\\" + selectedTheme + "\\ModernPatternEditor\\ModernPatternEditor.xaml";

                //skin.Source = new Uri(skinPath, UriKind.Absolute);
                skin = (ResourceDictionary)XamlReaderEx.LoadHack(skinPath);
            }
            catch (Exception)
            {
                string skinPath = Global.BuzzPath + "\\Themes\\Default\\ModernPatternEditor\\PatternPropertiesWindow.xaml";
                skin.Source = new Uri(skinPath, UriKind.Absolute);
            }

            return skin;
        }

        #endregion

        #region Registry

        static readonly string regpath = Global.RegistryRoot;
        internal static readonly string regPathBuzzSettings = "Settings";
        internal static readonly string regPathBuzzMachineDefaultPE = "DefaultPE";
        internal static readonly string regDefaultPE = "DefaultPE";
        public static void WriteRegistry<T>(string key, T x, string path)
        {
            try
            {
                var regkey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(regpath + "\\" + path);
                if (regkey == null) return;
                regkey.SetValue(key, x.ToString());
            }
            catch (Exception) { }

        }

        #endregion


        public static IDictionary<string, string> MachineNameMap { get; internal set; }

        public static string GetMachineCurrentName(string machineName)
        {
            if (MachineNameMap != null)
            {
                if (MachineNameMap.ContainsKey(machineName))
                    return MachineNameMap[machineName];
            }

            return machineName;
        }

        internal static IList<IPatternEditorColumn> GetPatternEditorColumnEvents(MPEPattern mpePattern, int tbegin, int tend)
        {
            List<IPatternEditorColumn> pecList = new List<IPatternEditorColumn>();

            foreach (var column in mpePattern.MPEPatternColumns)
            {
                PatternEditorColumn pec = new PatternEditorColumn();
                if (column != null)
                {
                    pec.Parameter = column.Parameter;
                    pec.Events = column.GetEvents(tbegin, tend).ToList();
                    pecList.Add(pec);
                }
            }
            return pecList;
        }
    }

    internal class PatternEditorColumn : IPatternEditorColumn
    {
        public IParameter Parameter { get; set; }

        public IEnumerable<PatternEvent> Events { get; set; }
    }

}
