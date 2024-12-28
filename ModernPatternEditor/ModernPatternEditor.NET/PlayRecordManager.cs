using BuzzGUI.Common;
using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using WDE.ModernPatternEditor.Actions;
using WDE.ModernPatternEditor.MPEStructures;
using static WDE.ModernPatternEditor.PatternEditorUtils;

namespace WDE.ModernPatternEditor
{
    internal class PlayRecordManager
    {
        public PatternEditor Editor { get; }

        Dictionary<ISequence, PlayInfo> playInfoDictionary = new Dictionary<ISequence, PlayInfo>();
        List<PlayingTrackInfo> PlayingNotesList = new List<PlayingTrackInfo>();
        List<CollectedEventState> midiEventsToTriggerLater = new List<CollectedEventState>();

        struct PlayInfo
        {
            public int PreviousPosition;
            public IPattern PreviousPattern { get; internal set; }
        }

        internal struct MidiColumnData
        {
            internal MPEPatternColumn column;
            internal int value;
        }

        internal struct MidiEventData
        {
            internal MidiColumnData midiNote;
            internal MidiColumnData midiVelocity;
            internal MidiColumnData midiDelay;
            internal MidiColumnData midiCut;
            internal MidiColumnData midiPitchWheel;
            internal MidiColumnData midiCC;
            internal MidiColumnData midiChannel;
            internal bool noteOff;
            internal int midiNoteTimeQ;
        }

        public void Release()
        {
            PlayingNotesList.Clear();
            midiEventsToTriggerLater.Clear();
            playInfoDictionary.Clear();
        }

        class PlayingTrackInfo
        {
            internal int track;
            internal int note;
            internal IPattern pattern;
            internal IParameter parameter;
            internal int channel;
        }
        public PlayRecordManager(PatternEditor editor)
        {
            Editor = editor;
        }

        internal enum CollectedEventType
        {
            Buzz,
            MidiNote,
            MidiCC
        }

        internal class CollectedEventState
        {
            internal IParameter parameter;
            internal int time;
            internal int ParamTrack;
            internal int value;
            internal int midiNote;
            internal int midiVolume;
            internal IMachine machine;
            internal CollectedEventType eventType;
            internal int midiChannel;
        }

        internal void RefreshPlayPosData()
        {
            lock (Editor.syncLock)
            {
                if (Editor.Song != null)
                {
                    foreach (var seq in Editor.Song.Sequences)
                    {
                        playInfoDictionary[seq] = new PlayInfo();
                    }
                }
            }
        }

        internal void Play(SongTime songTime)
        {
            ReadOnlyCollection<ISequence> seqs = GetPlayingSequences(Editor.TargetMachine);

            List<CollectedEventState> collectEvents = new List<CollectedEventState>();

            IPattern pat = null;

            foreach (ISequence seq in seqs)
            {
                int playPosition = GetPositionInPattern(out pat, seq, Editor.TargetMachine);

                if (playPosition != -1 && pat != null)
                {
                    PlayInfo playInfo = new PlayInfo();

                    if (playInfoDictionary.ContainsKey(seq))
                        playInfo = this.playInfoDictionary[seq];

                    int prevPlayPos = playInfo.PreviousPosition;

                    if ((pat.Length * PatternEvent.TimeBase + playPosition - prevPlayPos) % (pat.Length * PatternEvent.TimeBase) > PatternEvent.TimeBase)
                    {
                        prevPlayPos = (pat.Length * PatternEvent.TimeBase + playPosition /* - 1 PatternEvent.TimeBase*/) % (pat.Length * PatternEvent.TimeBase);
                    }

                    if (prevPlayPos > playPosition)
                    {
                        if (playInfo.PreviousPattern != null)
                            PlayPatternEvents(playInfo.PreviousPattern, prevPlayPos, pat.Length * PatternEvent.TimeBase, collectEvents, midiEventsToTriggerLater);
                        PlayPatternEvents(pat, 0, playPosition, collectEvents, midiEventsToTriggerLater);
                    }
                    else
                    {
                        PlayPatternEvents(pat, prevPlayPos, playPosition, collectEvents, midiEventsToTriggerLater);
                    }
                    playInfo.PreviousPosition = playPosition;
                    playInfo.PreviousPattern = pat;
                    lock (Editor.syncLock)
                    {
                        playInfoDictionary[seq] = playInfo;
                    }
                }
                else if (pat != null)
                {
                    var mpePattern = Editor.MPEPatternsDB.GetMPEPattern(pat);
                }
            }

            if (midiEventsToTriggerLater.Count > 0)
            {
                // If playing solo (pattern editor pattern)
                if (pat != null && pat.IsPlayingSolo)
                {
                    FireCollectedMidiEvents(midiEventsToTriggerLater, pat.PlayPosition);
                }
                else
                {
                    FireCollectedMidiEvents(midiEventsToTriggerLater, Global.Buzz.Song.PlayPosition * PatternEvent.TimeBase);
                }
            }

            if (collectEvents.Count > 0)
            {  
                FireCollectedEvents(collectEvents);
            }
        }

        internal void Stop()
        {
            lock (Editor.syncLock)
            {
                foreach (var e in midiEventsToTriggerLater)
                {
                    var machine = e.machine;
                    machine.SendMIDINote(machine.MIDIInputChannel, e.midiNote, 0);
                }
                midiEventsToTriggerLater.Clear();
                foreach (var note in PlayingNotesList)
                {
                    var machine = note.parameter.Group.Machine;
                    machine.SendMIDINote(note.channel, BuzzNote.ToMIDINote(note.note), 0);
                }
                PlayingNotesList.Clear();
            }
        }

        internal void StopNonEditorMidiNotes()
        {
            lock (Editor.syncLock)
            {
                for (int i = 0; i < PlayingNotesList.Count; i++)
                {
                    var note = PlayingNotesList[i];

                    if (note.track == -1)
                    {
                        var machine = note.parameter.Group.Machine;
                        machine.SendMIDINote(note.channel, BuzzNote.ToMIDINote(note.note), 0);
                        PlayingNotesList.RemoveAt(i);
                        i--;
                    }
                }
                PlayingNotesList.Clear();
            }
        }

        private void FireCollectedEvents(List<CollectedEventState> collectEvents)
        {
            Dictionary<IMachine, int> machines = new Dictionary<IMachine, int>();
            foreach (var e in collectEvents)
            {
                machines[e.parameter.Group.Machine] = 0;

                var machine = e.parameter.Group.Machine;
                int group = e.parameter.Group.Machine.ParameterGroups.IndexOf(e.parameter.Group) | 16; // Don't record
                int track = e.ParamTrack;
                int param = e.parameter.IndexInGroup;
                int value = e.value;

                if (e.parameter.Type == ParameterType.Note)
                {
                    UpdatePlayingNotePattern(e.parameter, track, value, 0);
                }
                Editor.cb.ControlChange(machine, group, track, param, value);
            }

            foreach (var m in machines.Keys)
                if (m.DLL.Info.Version >= 42 || PatternEditor.Settings.ForceControlChange)
                    m.SendControlChanges();
        }

        private void FireCollectedMidiEvents(List<CollectedEventState> collectEvents, int playPosition)
        {
            Dictionary<IMachine, int> machines = new Dictionary<IMachine, int>();

            for (int i = 0; i < collectEvents.Count; i++)
            {
                var e = collectEvents[i];
                machines[e.parameter.Group.Machine] = 0;

                IMachine machine = e.machine;
                if (e.eventType == CollectedEventType.MidiNote)
                {
                    if (e.time < playPosition)
                    {
                        //machine.SendMIDINote(e.midiChannel, e.midiNote, e.midiVolume);
                        collectEvents.RemoveAt(i);
                        i--;

                        if (e.midiVolume > 0)
                            UpdatePlayingNotePattern(e.parameter, e.ParamTrack, e.value, e.midiVolume);
                        else
                            UpdatePlayingNotePattern(e.parameter, e.ParamTrack, BuzzNote.Off, 0);
                    }
                }
            }
        }


        // ToDO: lock things?
        internal void PlayPatternEvents(IPattern pat, int start, int end, List<CollectedEventState> collectEvents, List<CollectedEventState> midiEvents)
        {
            var mpep = Editor.MPEPatternsDB.GetMPEPattern(pat);

            foreach (var mpeColumn in mpep.MPEPatternColumns)
            {
                if (Editor.MPEPatternsDB.IsColumnMuted(mpeColumn.Parameter, mpeColumn.ParamTrack))
                    continue;

                // Midi is handled differently
                if (mpeColumn.Parameter.IndexInGroup == (int)PatternEditorUtils.InternalParameter.MidiNote)
                {
                    var midiNoteData = GetMidiDataForNote(pat, mpeColumn, mpeColumn.Machine, start, end, mpeColumn.ParamTrack);

                    // Create Midi events for both note on and note off. Trigger them when play postion catches
                    if (midiNoteData.midiNote.value != -1)
                    {
                        int noteTime = midiNoteData.midiNoteTimeQ + midiNoteData.midiDelay.value;

                        CollectedEventState ces = new CollectedEventState();
                        ces.value = BuzzNote.FromMIDINote(midiNoteData.midiNote.value);
                        ces.midiNote = midiNoteData.midiNote.value;
                        ces.midiVolume = midiNoteData.midiVelocity.value;
                        ces.machine = mpeColumn.Machine;
                        ces.eventType = CollectedEventType.MidiNote;
                        ces.midiChannel = midiNoteData.midiChannel.value;
                        ces.parameter = mpeColumn.Parameter;
                        ces.time = noteTime;

                        midiEvents.Add(ces);
                    }
                    if (midiNoteData.midiNote.value != -1 && midiNoteData.midiCut.value > 0)
                    {
                        int noteOffTime = midiNoteData.midiNoteTimeQ + midiNoteData.midiCut.value;

                        CollectedEventState ces = new CollectedEventState();
                        ces.midiNote = midiNoteData.midiNote.value;
                        ces.value = BuzzNote.FromMIDINote(midiNoteData.midiNote.value);
                        ces.midiVolume = 0;
                        ces.machine = mpeColumn.Machine;
                        ces.eventType = CollectedEventType.MidiNote;
                        ces.midiChannel = midiNoteData.midiChannel.value;
                        ces.parameter = mpeColumn.Parameter;
                        ces.time = noteOffTime;
                        midiEvents.Add(ces);
                    }
                    if (midiNoteData.noteOff)
                    {
                        PlayingTrackInfo pti = PlayingNotesList.FirstOrDefault(x => x.track == mpeColumn.ParamTrack);
                        if (pti != null) // Need to know playing note so that we can stop it.
                        {
                            int noteOffTime = midiNoteData.midiNoteTimeQ + midiNoteData.midiDelay.value;

                            CollectedEventState ces = new CollectedEventState();
                            ces.midiNote = BuzzNote.ToMIDINote(pti.note);
                            ces.value = pti.note;
                            ces.midiVolume = 0;
                            ces.machine = mpeColumn.Machine;
                            ces.eventType = CollectedEventType.MidiNote;
                            ces.midiChannel = midiNoteData.midiChannel.value;
                            ces.parameter = mpeColumn.Parameter;
                            ces.time = noteOffTime;
                            midiEvents.Add(ces);
                        }
                    }
                }
                else if (mpeColumn.Parameter.IndexInGroup != (int)PatternEditorUtils.InternalParameter.MidiVelocity &&
                        mpeColumn.Parameter.IndexInGroup != (int)PatternEditorUtils.InternalParameter.MidiNoteDelay &&
                        mpeColumn.Parameter.IndexInGroup != (int)PatternEditorUtils.InternalParameter.MidiNoteCut)
                {
                    mpeColumn.PlayColumnEvents(pat, start, end, collectEvents);
                }
            }
        }

        internal int GetPositionInPattern(out IPattern pat, ISequence seq, IMachine target)
        {
            pat = null;

            if (seq.Machine == target && seq.PlayingPattern != null && !seq.IsDisabled)
            {
                pat = seq.PlayingPattern;
                if (pat != null && pat.PlayPosition >= 0)
                {
                    return pat.PlayPosition;
                }
                else return -1;
            }
            return -1;
        }

        internal ReadOnlyCollection<ISequence> GetPlayingSequences(IMachine target)
        {
            List<ISequence> sequences = new List<ISequence>();
            foreach (ISequence seq in Global.Buzz.Song.Sequences)
            {
                if (seq.Machine == target)
                {
                    IPattern pat = seq.PlayingPattern;
                    if (pat != null)
                    {
                        sequences.Add(seq);
                    }
                }
            }
            return sequences.AsReadOnly();
        }

        internal IPattern GetPlayingPattern(IMachine target)
        {
            IPattern pattern = null;
            foreach (ISequence seq in Global.Buzz.Song.Sequences)
            {
                if (seq.Machine == target)
                {
                    pattern = seq.PlayingPattern;
                    if (pattern != null)
                    {
                        break;
                    }
                }
            }
            return pattern;
        }

        internal void Init()
        {
            if (Editor.TargetMachine == null)
                return;

            lock (Editor.syncLock)
            {
                PlayingNotesList = new List<PlayingTrackInfo>(Editor.TargetMachine.TrackCount);
                if (Editor.Song != null)
                {
                    for (int i = 0; i < playInfoDictionary.Keys.Count; i++)
                    {
                        var info = playInfoDictionary.ElementAt(i).Value;
                        info.PreviousPosition = Editor.Song.PlayPosition;
                    }
                }
            }
        }

        internal void RecordControlChange(IParameter param, int track, int value)
        {
            IMachine mac = param.Group.Machine;
            IPattern pat = mac.Patterns.FirstOrDefault(x => x.PlayPosition >= 0);
            if (pat != null)
            {
                int playPostion = pat.PlayPosition;
                if (playPostion >= 0)
                {
                    //Task.Run(() =>
                    {
                        RecordControlChangeToPattern(pat, playPostion, param, track, value);
                    }
                    //);
                }
            }
        }

        private void RecordControlChangeToPattern(IPattern pat, int playPosition, IParameter parameter, int track, int value)
        {
            var vmPattern = Editor.SelectedMachine.Patterns.FirstOrDefault(x => x.Pattern == pat);

            foreach (var columnSet in vmPattern.ColumnSets)
            {
                foreach (var column in columnSet.Columns)
                {
                    var parameterColumn = (ParameterColumn)column;
                    if (parameterColumn.PatternColumn.Parameter == parameter && parameterColumn.PatternColumn.Track == track)
                    {
                        // Touching so UI elements so dispatcher...
                        Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                        {
                            var digit = parameterColumn.GetDigitAtTime(playPosition);
                            foreach (var a in parameterColumn.EditValue(digit, value))
                                Editor.EditContext.ActionStack.Do(a);
                        }));
                    }
                }
            }
        }

        internal void RecordPlayingMidiNote(int channel, int value, int velocity)
        {
            foreach (var pattern in Editor.SelectedMachine.Machine.Patterns)
            {
                int playPosition = pattern.PlayPosition;
                if (playPosition >= 0)
                {
                    RecordPlayingMidiNotePattern(pattern, playPosition, value, velocity, channel);
                }
            }
        }

        private void RecordPlayingMidiNotePattern(IPattern pattern, int playPosition, int value, int velocity, int channel)
        {
            int availableTrack = -1;
            int buzzNote = -1;
            try
            {
                buzzNote = BuzzNote.FromMIDINote(value);
            }
            catch (Exception)
            {
            }

            if (buzzNote == -1)
                return;

            IMachine selectedColumnMachine;
            MidiEventData machinePatternColumnData;

            // 1. Note off event. Is there a note already playing?
            if (velocity == 0)
            {
                // Find playing note
                PlayingTrackInfo pti = PlayingNotesList.FirstOrDefault(x => x.note == buzzNote);
                if (pti != null)
                {
                    PlayingNotesList.Remove(pti);

                    // Recording for the "main" pattern machine for now. ToDo: When start recording, save the target machine to Editor and reference to it here
                    // IMachine selectedColumnMachine = Editor.patternControl.Pattern.CursorPosition.ParameterColumn.PatternColumn.Pattern.Machine;

                    selectedColumnMachine = pattern.Machine;
                    machinePatternColumnData = GetMidiDataParameterIndexes(pattern, selectedColumnMachine, pti.track);
                    RecordMidiNoteOff(pattern, playPosition, machinePatternColumnData, channel);
                }
                return;
            }

            // 2. Find available track
            for (int i = 0; i < pattern.Machine.TrackCount; i++)
            {
                bool trackAvailable = true;
                foreach (var playingNote in PlayingNotesList)
                {
                    // Is same note playing in track, then we can use the same track
                    if (playingNote.track == i)
                    {
                        if (playingNote.note == buzzNote)
                        {
                            break;
                        }
                        else
                        {
                            trackAvailable = false;
                            break;
                        }
                    }
                }

                if (trackAvailable)
                {
                    availableTrack = i;
                    break;
                }
            }

            // Not found, use the oldest track
            if (availableTrack == -1)
            {
                PlayingTrackInfo pti = PlayingNotesList.ElementAtOrDefault(0);
                if (pti == null)
                {
                    availableTrack = 0;
                }
                else
                {
                    availableTrack = pti.track;
                    PlayingNotesList.RemoveAt(0);
                }
            }

            selectedColumnMachine = pattern.Machine;
            machinePatternColumnData = GetMidiDataParameterIndexes(pattern, selectedColumnMachine, availableTrack);

            if (machinePatternColumnData.midiNote.column != null)
            {
                IParameter noteParameter = machinePatternColumnData.midiNote.column.Parameter;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    using (new ActionGroup(Editor.EditContext.ActionStack))
                    {
                        // Note
                        if (machinePatternColumnData.midiNote.column != null)
                        {
                            var mpeColumn = machinePatternColumnData.midiNote.column;
                            int timeq = mpeColumn.GetTimeQuantized(playPosition);
                            Editor.DoAction(new MPESetOrClearEventsAction(pattern, mpeColumn, new PatternEvent[] { new PatternEvent() { Time = timeq, Value = buzzNote } }, true));
                        }
                        // Volume
                        if (machinePatternColumnData.midiVelocity.column != null)
                        {
                            var mpeColumn = machinePatternColumnData.midiVelocity.column;
                            int timeq = mpeColumn.GetTimeQuantized(playPosition);
                            int vol = (int)(velocity * ((mpeColumn.Parameter.MaxValue - mpeColumn.Parameter.MinValue) / 127.0));
                            Editor.DoAction(new MPESetOrClearEventsAction(pattern, mpeColumn, new PatternEvent[] { new PatternEvent() { Time = timeq, Value = vol } }, true));
                        }
                        // Delay
                        if (machinePatternColumnData.midiDelay.column != null)
                        {
                            var mpeColumn = machinePatternColumnData.midiDelay.column;
                            int timeq = mpeColumn.GetTimeQuantized(playPosition);
                            int delay = (int)((mpeColumn.Parameter.MaxValue - mpeColumn.Parameter.MinValue) * mpeColumn.GetRelativeTimeInRow(playPosition));
                            Editor.DoAction(new MPESetOrClearEventsAction(pattern, mpeColumn, new PatternEvent[] { new PatternEvent() { Time = timeq, Value = delay } }, true));
                        }

                        if (machinePatternColumnData.midiChannel.column != null)
                        {
                            var mpeColumn = machinePatternColumnData.midiChannel.column;
                            int timeq = mpeColumn.GetTimeQuantized(playPosition);
                            Editor.DoAction(new MPESetOrClearEventsAction(pattern, mpeColumn, new PatternEvent[] { new PatternEvent() { Time = timeq, Value = channel + 1 } }, true));
                        }
                    }
                });

                PlayingTrackInfo ptinew = new PlayingTrackInfo();
                ptinew.pattern = pattern;
                ptinew.note = buzzNote;
                ptinew.track = availableTrack;
                ptinew.channel = channel;
                ptinew.parameter = noteParameter;
                PlayingNotesList.Add(ptinew);
            }
        }

        internal void RecordMidiNoteOff(IPattern pattern, int playPosition, MidiEventData machinePatternColumnData, int channel)
        {
            if (machinePatternColumnData.midiNote.column != null)
            {
                var columnNote = machinePatternColumnData.midiNote.column;

                int noteTimeQ = columnNote.GetTimeQuantized(playPosition);
                var noteEvents = columnNote.GetEvents(noteTimeQ, noteTimeQ + 1);

                // If there is a note and playPosition > timeq + delay, then edit cut
                int delay = 0;
                int delayTimeQ = 0;
                var columnDelay = machinePatternColumnData.midiDelay.column;
                if (columnDelay != null)
                {
                    delayTimeQ = columnDelay.GetTimeQuantized(playPosition);
                    var delayEvents = columnDelay.GetEvents(delayTimeQ, delayTimeQ + 1);
                    int rowLenght = columnDelay.RowLenghtAt(delayTimeQ);
                    if (delayEvents.Count() > 0)
                    {
                        var delayEvent = delayEvents.First();
                        delay = (int)((delayEvent.Value - columnDelay.Parameter.MinValue) /
                            (columnDelay.Parameter.MaxValue - (double)columnDelay.Parameter.MinValue)) * rowLenght;
                    }
                }

                if (noteEvents.Count() > 0)
                {
                    if (machinePatternColumnData.midiCut.column != null && playPosition > noteTimeQ + delay)
                    {
                        var columnCut = machinePatternColumnData.midiCut.column;
                        if (columnCut != null)
                        {
                            int cutTimeQ = columnCut.GetTimeQuantized(playPosition);
                            var cutEvents = columnCut.GetEvents(cutTimeQ, cutTimeQ + 1);
                            if (cutEvents.Count() == 0)
                            {
                                int cutTime = columnCut.GetRelativeTimeInRowToParamValue(playPosition);
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    Editor.DoAction(new MPESetOrClearEventsAction(pattern, columnCut, new PatternEvent[] { new PatternEvent() { Time = cutTimeQ, Value = cutTime } }, true));
                                });
                            }
                        }
                    }
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        using (new ActionGroup(Editor.EditContext.ActionStack))
                        {
                            Editor.DoAction(new MPESetOrClearEventsAction(pattern, columnNote, new PatternEvent[] { new PatternEvent() { Time = noteTimeQ, Value = BuzzNote.Off } }, true));


                            if (columnDelay != null)
                            {
                                int delayTime = columnDelay.GetRelativeTimeInRowToParamValue(playPosition);
                                Editor.DoAction(new MPESetOrClearEventsAction(pattern, columnDelay, new PatternEvent[] { new PatternEvent() { Time = delayTimeQ, Value = delayTime } }, true));
                            }

                            var columnChannel = machinePatternColumnData.midiChannel.column;
                            if (columnChannel != null)
                            {
                                int channelTimeQ = columnChannel.GetTimeQuantized(playPosition);
                                Editor.DoAction(new MPESetOrClearEventsAction(pattern, columnChannel, new PatternEvent[] { new PatternEvent() { Time = channelTimeQ, Value = channel + 1 } }, true));
                            }
                        }
                    });
                    //lock (Editor.syncLock)
                    {
                        PlayingNotesList.RemoveAll(x => x.track == columnNote.ParamTrack && x.parameter == columnNote.Parameter);
                    }
                }
            }
        }

        public void UpdatePlayingNotePattern(IParameter parameter, int track, int buzzNote, int volume)
        {
            // Stop notes on target track
            // ToDo: if midinote, always do SendMidiNote
            PlayingTrackInfo pti = PlayingNotesList.FirstOrDefault(x => x.track == track && x.parameter == parameter);
            if (pti != null &&
                parameter is MPEInternalParameter &&
                ((MPEInternalParameter)parameter).InternalType == InternalParameter.MidiNote)
            {
                parameter.Group.Machine.SendMIDINote(pti.channel, BuzzNote.ToMIDINote(pti.note), 0);
            }

            PlayingNotesList.RemoveAll(x => x.track == track && x.parameter == parameter);

            if (buzzNote != BuzzNote.Off)
            {
                PlayingTrackInfo ptinew = new PlayingTrackInfo();
                ptinew.parameter = parameter;
                ptinew.note = buzzNote;
                ptinew.track = track;
                PlayingNotesList.Add(ptinew);
                if (parameter is MPEInternalParameter &&
                ((MPEInternalParameter)parameter).InternalType == InternalParameter.MidiNote)
                {
                    int midiNote = -1;
                    try
                    {
                        midiNote = BuzzNote.ToMIDINote(buzzNote);
                    }
                    catch { }

                    if (midiNote != -1)
                    {
                        parameter.Group.Machine.SendMIDINote(0, midiNote, volume);
                    }
                }
            }
        }

        private MidiEventData GetMidiDataParameterIndexes(IPattern pattern, IMachine machine, int track)
        {
            MidiEventData midiData = new MidiEventData();

            // If there is a midi note, then try to find rest of the midi columns.
            // Otherwise handle special cases (pVST...)
            var mpePattern = Editor.MPEPatternsDB.GetMPEPattern(pattern);
            var midiNote = mpePattern.GetColumn(machine, (int)InternalParameter.MidiNote, track);
            if (midiNote != null)
            {
                midiData.midiNote.column = midiNote;
                midiData.midiVelocity.column = mpePattern.GetColumn(machine, (int)InternalParameter.MidiVelocity, track);
                midiData.midiCut.column = mpePattern.GetColumn(machine, (int)InternalParameter.MidiNoteCut, track);
                midiData.midiDelay.column = mpePattern.GetColumn(machine, (int)InternalParameter.MidiNoteDelay, track);
                midiData.midiPitchWheel.column = mpePattern.GetColumn(machine, (int)InternalParameter.MidiPitchWheel, track);
                midiData.midiCC.column = mpePattern.GetColumn(machine, (int)InternalParameter.MidiCC, track);
            }
            else
            {
                // Maybe xml file that includes MIDI mappings...
                if (machine.DLL.Name == "Polac VSTi 1.1")
                {
                    midiData.midiNote.column = mpePattern.GetColumn(machine, 0, track);
                    midiData.midiVelocity.column = mpePattern.GetColumn(machine, 1, track);
                    midiData.midiDelay.column = mpePattern.GetColumn(machine, 2, track);
                    midiData.midiCut.column = mpePattern.GetColumn(machine, 3, track);
                    midiData.midiChannel.column = mpePattern.GetColumn(machine, 8, track);
                }
            }

            return midiData;
        }

        internal MidiEventData GetMidiDataForNote(IPattern pattern, MPEPatternColumn mpeColumn, IMachine machine, int start, int end, int track)
        {
            var machinePatternColumnData = GetMidiDataParameterIndexes(pattern, machine, track);
            machinePatternColumnData.midiNote.value = -1;
            machinePatternColumnData.midiVelocity.value = MPEInternalParameter.DefaultMidiVolume;

            if (machinePatternColumnData.midiNote.column != null)
            {
                var columnNote = machinePatternColumnData.midiNote.column;
                var noteEvents = columnNote.GetEvents(start, end);
                machinePatternColumnData.midiNoteTimeQ = columnNote.GetTimeQuantized(end);
                if (noteEvents.Count() > 0)
                {
                    int val = noteEvents.First().Value;
                    if (val == BuzzNote.Off)
                        machinePatternColumnData.noteOff = true;
                    else
                        machinePatternColumnData.midiNote.value = BuzzNote.ToMIDINote(noteEvents.First().Value);
                }
            }
            if (machinePatternColumnData.midiVelocity.column != null)
            {
                var columnVol = machinePatternColumnData.midiVelocity.column;
                var volEvents = columnVol.GetEvents(start, end);
                if (volEvents.Count() > 0)
                {
                    int vol = volEvents.First().Value;
                    machinePatternColumnData.midiVelocity.value = vol;
                    machinePatternColumnData.midiVelocity.column.Parameter.SetValue(track, vol);
                }
                else
                {
                    machinePatternColumnData.midiVelocity.value = machinePatternColumnData.midiVelocity.column.Parameter.GetValue(track);
                }
            }
            if (machinePatternColumnData.midiDelay.column != null)
            {
                var columnDelay = machinePatternColumnData.midiDelay.column;
                var delayEvents = columnDelay.GetEvents(start, end);

                if (delayEvents.Count() > 0)
                {
                    int delayTimeQ = columnDelay.GetTimeQuantized(end);
                    int rowLenght = columnDelay.RowLenghtAt(delayTimeQ);
                    machinePatternColumnData.midiDelay.value = (int)(rowLenght * (delayEvents.First().Value - columnDelay.Parameter.MinValue) / ((double)columnDelay.Parameter.MaxValue - columnDelay.Parameter.MinValue));
                }
            }
            if (machinePatternColumnData.midiCut.column != null)
            {
                var columnCut = machinePatternColumnData.midiCut.column;
                var cutEvents = columnCut.GetEvents(start, end);

                if (cutEvents.Count() > 0)
                {
                    int cutTimeQ = columnCut.GetTimeQuantized(end);
                    int rowLenght = columnCut.RowLenghtAt(cutTimeQ);
                    machinePatternColumnData.midiCut.value = (int)(rowLenght * (cutEvents.First().Value - columnCut.Parameter.MinValue) / ((double)columnCut.Parameter.MaxValue - columnCut.Parameter.MinValue));
                }
            }
            if (machinePatternColumnData.midiChannel.column != null)
            {
                var columnChannel = machinePatternColumnData.midiChannel.column;
                var chanEvents = columnChannel.GetEvents(start, end);
                if (chanEvents.Count() > 0)
                {
                    machinePatternColumnData.midiChannel.value = chanEvents.First().Value;
                }
            }
            else
            {
                machinePatternColumnData.midiChannel.value = mpeColumn.Machine.MIDIInputChannel + 1;
            }


            return machinePatternColumnData;
        }

        internal void UpdatePlayingMidiNote(int channel, int value, int velocity)
        {
            int buzzNote = -1;
            try
            {
                buzzNote = BuzzNote.FromMIDINote(value);
            }
            catch
            {
                return;
            }

            if (velocity == 0)
            {
                PlayingNotesList.RemoveAll(x => x.note == buzzNote);
            }
            else if (Editor.SelectedMachine != null)
            {
                PlayingTrackInfo pti = new PlayingTrackInfo();
                pti.note = buzzNote;
                pti.track = -1;
                pti.channel = channel;
                pti.parameter = Editor.SelectedMachine.Machine.ParameterGroups[0].Parameters[0]; // Just get a parameter
                PlayingNotesList.Add(pti);
            }
        }
    }
}
