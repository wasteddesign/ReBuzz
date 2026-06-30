using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Common.Settings;
using BuzzGUI.Interfaces;
using ReBuzz.Common;
using ReBuzz.Core;
using ReBuzz.MachineManagement;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ReBuzz.Audio
{
    internal class WorkManager
    {
        // Update this struct and array in the beginning of Thread read.
        // This is optimization. Avoids memory allocations and speeds up data copy.

        [StructLayout(LayoutKind.Sequential)]
        public struct BuzzMasterInfo
        {
            public int BeatsPerMin;
            public int TicksPerBeat;
            public int SamplesPerSec;
            public int SamplesPerTick;
            public int PosInTick;
            public float TicksPerSec;
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct BuzzSubTickInfo
        {
            public int SubTicksPerTick;
            public int CurrentSubTick;
            public int SamplesPerSubTick;
            public int PosInSubTick;
        };

        internal static BuzzMasterInfo MasterInfoStruct;
        unsafe internal static byte[] MasterInfoData = new byte[sizeof(BuzzMasterInfo)];
        internal static BuzzSubTickInfo SubTickInfoStruct;
        unsafe internal static byte[] SubTickInfoData = new byte[sizeof(BuzzSubTickInfo)];

        bool multiThreadingEnabled = false;

        private readonly ReBuzzCore buzzCore;
        readonly WorkThreadEngine workEngine;

        readonly int algorithm;

        // Use HashSet to ensure do duplicate list items
        readonly HashSet<MachineCore> workSet = new HashSet<MachineCore>();
        private bool stopped;
        readonly bool SpeedAdjustLinear = false;

        float[] playWaveBuffer = new float[256 * 2];

        public WorkManager(ReBuzzCore buzzCore, WorkThreadEngine workEngine, int algorithm, EngineSettings settings)
        {
            this.buzzCore = buzzCore;
            this.workEngine = workEngine;
            this.algorithm = algorithm;
            engineSettings = settings;

            workOneList = WorkOneList;
            workOneGroups = WorkOneGroups;

            var master = buzzCore.SongCore.MachinesList.FirstOrDefault(m => m.DLL.Info.Type == MachineType.Master);
            var masterGlobalParameters = master.ParameterGroupsList[1].ParametersList;
        }

        internal void CopyMasterInfo()
        {
            var masterInfo = ReBuzzCore.masterInfo;
            MasterInfoStruct.BeatsPerMin = masterInfo.BeatsPerMin;
            MasterInfoStruct.TicksPerBeat = masterInfo.TicksPerBeat;
            MasterInfoStruct.SamplesPerSec = masterInfo.SamplesPerSec;
            MasterInfoStruct.SamplesPerTick = masterInfo.SamplesPerTick;
            MasterInfoStruct.PosInTick = masterInfo.PosInTick;
            MasterInfoStruct.TicksPerSec = masterInfo.TicksPerSec;

            // Update array
            MasterInfoData = Utils.SerializeValueTypeChangePointer(MasterInfoStruct, ref MasterInfoData);
        }

        internal void CopySubTickInfo()
        {
            var subtickInfo = ReBuzzCore.subTickInfo;
            if (engineSettings.SubTickTiming)
            {   
                SubTickInfoStruct.CurrentSubTick = subtickInfo.CurrentSubTick;
                SubTickInfoStruct.PosInSubTick = subtickInfo.PosInSubTick;
                SubTickInfoStruct.SubTicksPerTick = subtickInfo.SubTicksPerTick;
                SubTickInfoStruct.SamplesPerSubTick = subtickInfo.SamplesPerSubTick;
            }
            else
            {
                SubTickInfoStruct.CurrentSubTick = 0;
                SubTickInfoStruct.PosInSubTick = 0;
                SubTickInfoStruct.SubTicksPerTick = 0;
                SubTickInfoStruct.SamplesPerSubTick = 0;
            }

            // Update array
            SubTickInfoData = Utils.SerializeValueTypeChangePointer(SubTickInfoStruct, ref SubTickInfoData);
        }

        internal int workBufferOffset;
        // Avoid new object creation to minimize GC.
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public int MainAudioFillBuffer(float[] buffer, int offset, int count)
        {
            // Override audio driver and call workManager.ThreadRead outside of Read
            if (stopped || ReBuzzCore.SkipAudio)
            {
                // Array.Clear(buffer, offset, count) is wrongly casted when using ASIO
                for (int i = 0; i < count; i++)
                {
                    buffer[offset + i] = 0;
                }
                return count;
            }

            lock (ReBuzzCore.AudioLock)
            {
                long time = Stopwatch.GetTimestamp();

                multiThreadingEnabled = engineSettings.Multithreading;

                var subTickInfo = ReBuzzCore.subTickInfo;
                var masterInfo = ReBuzzCore.masterInfo;
                
                int reminingBuffer = count;
                workBufferOffset = offset;

                while (reminingBuffer > 0)
                {   
                    int samplesToProcess = Math.Min(reminingBuffer / 2, 256);

                    // More accurate samples per tick?
                    //if (engineSettings.AccurateBPM)
                    //{
                    //    UpdateMasterSamplesPerTick();
                    //}

                    UpdateMasterParams();

                    // Initiate master info for Audio Messages
                    CopyMasterInfo();

                    // Update SubTick
                    UpdateSubTickLength();

                    // Initiate master info for Audio Messages
                    CopySubTickInfo();

                    // HandleParameterRecord();

                    // Ensure we don't go over tick
                    if (masterInfo.PosInTick + samplesToProcess > masterInfo.SamplesPerTick)
                    {
                        // Make sure tick will be zero
                        samplesToProcess = masterInfo.SamplesPerTick - masterInfo.PosInTick;
                        if (samplesToProcess < 0)
                            return 0;
                    }

                    // Ensure we don't go over subtick
                    if (engineSettings.SubTickTiming && 
                        subTickInfo.PosInSubTick + samplesToProcess > subTickInfo.SamplesPerSubTick)
                    {
                        // Make sure tick will be zero
                        samplesToProcess = subTickInfo.SamplesPerSubTick - subTickInfo.PosInSubTick;
                        if (samplesToProcess < 0)
                            return 0;
                    }

                    // Call Pattern Column Events
                    UpdatePatternColumnEvents(samplesToProcess);

                    // Update Managed Machine host info
                    buzzCore.MachineManager.UpdateMasterAndSubTickInfoToHost();

                    // Tick all machines first and then call tick again if sendcontrolchanges flag set?
                    CallTick();

                    // Call work
                    ReadWork(buffer, workBufferOffset, samplesToProcess);

                    // Mix waves playing from wavetable 
                    if (buzzCore.SongCore.WavetableCore.IsPlayingWave())
                    {
                        buzzCore.SongCore.WavetableCore.GetPlayWaveSamples(playWaveBuffer, offset, samplesToProcess);
                        for (int i = 0; i < samplesToProcess * 2; i++)
                        {
                            buffer[workBufferOffset + i] += playWaveBuffer[i] * 32768.0f;
                        }
                    }

                    // Master Tap
                    buzzCore.MasterTapSamples(buffer, workBufferOffset, samplesToProcess * 2);

                    workBufferOffset += samplesToProcess * 2;
                    reminingBuffer -= samplesToProcess * 2;

                    subTickInfo.PosInSubTick += samplesToProcess;
                    masterInfo.PosInTick += samplesToProcess;

                    if (subTickInfo.PosInSubTick >= subTickInfo.SamplesPerSubTick)
                    {
                        subTickInfo.PosInSubTick = 0;
                        subTickInfo.CurrentSubTick++;
                    }
                    // Last frame?
                    if (masterInfo.PosInTick >= masterInfo.SamplesPerTick)
                    {
                        masterInfo.PosInTick = 0;
                        subTickInfo.CurrentSubTick = 0;

                        if (!buzzCore.StartPlaying())
                        {
                            // Update song play position
                            if (buzzCore.Playing && buzzCore.SoloPattern == null)
                            {
                                buzzCore.SongCore.UpdatePlayPosition(1);
                            }
                        }
                    }

                    // Update position info in patterns
                    UpdatePatternPositions(samplesToProcess);

                    // Update frame count
                    unchecked { ReBuzzCore.GlobalState.AudioFrame++; }
                }

                float audioOutMul = 1 / 32768.0f;
                for (int i = 0; i < count; i++)
                {
                    buffer[offset + i] = buffer[offset + i] * audioOutMul; // From Buzz to audio output scale
                }

                // Update sample counters
                unchecked
                {
                    if (ReBuzzCore.GlobalState.ADPlayPos == ReBuzzCore.GlobalState.ADWritePos)
                    {
                        ReBuzzCore.GlobalState.ADWritePos += count / 2;
                    }
                    else
                    {
                        ReBuzzCore.GlobalState.ADPlayPos += count / 2;
                        ReBuzzCore.GlobalState.ADWritePos += count / 2;
                    }
                }

                buzzCore.PerformanceCurrent.EnginePerformanceCount += (Stopwatch.GetTimestamp() - time);

                return count;
            }
        }

        private void UpdateMasterParams()
        {
            var masterInfo = ReBuzzCore.masterInfo;
            // Update master params on the next tick
            var master = buzzCore.SongCore.MachinesList[0];
            var masterGlobalParameters = master.ParameterGroupsList[1].ParametersList;
            var bpm = masterGlobalParameters[1].GetValue(0);
            var tpb = masterGlobalParameters[2].GetValue(0);

            if (tpb != masterInfo.TicksPerBeat ||
                bpm != masterInfo.BeatsPerMin)
            {
                buzzCore.BPM = bpm;
                buzzCore.TPB = tpb;
                buzzCore.UpdateMasterInfo();
                buzzCore.MachineManager.RefreshMachineParams();
            }
        }
        private void UpdateSubTickLength()
        {
            var masterInfo = ReBuzzCore.masterInfo;
            var subTickInfo = ReBuzzCore.subTickInfo;

            if (subTickInfo.PosInSubTick == 0)
            {
                var subTickRemainder = masterInfo.SamplesPerTick % subTickInfo.SubTicksPerTick;

                subTickInfo.SamplesPerSubTick = (int)subTickInfo.AverageSamplesPerSubTick;
                subTickInfo.SubTickReminderCounter += subTickRemainder;

                if (subTickInfo.SubTickReminderCounter >= subTickInfo.SubTicksPerTick)
                {
                    subTickInfo.SubTickReminderCounter -= subTickInfo.SubTicksPerTick;
                    subTickInfo.SamplesPerSubTick++;
                }
                ReBuzzCore.masterInfoStructDirty = true;
            }
        }

        private void UpdateMasterSamplesPerTick()
        {
            var masterInfo = ReBuzzCore.masterInfo;

            if (masterInfo.PosInTick == 0)
            {
                var ticksPerMin = masterInfo.BeatsPerMin * masterInfo.TicksPerBeat;
                var samplesPerTickRemainder = 60 * masterInfo.SamplesPerSec % ticksPerMin;

                masterInfo.SamplesPerTick = (int)masterInfo.AverageSamplesPerTick;
                masterInfo.SamplesPerTickReminderCounter += samplesPerTickRemainder;

                if (masterInfo.SamplesPerTickReminderCounter >= ticksPerMin)
                {
                    masterInfo.SamplesPerTickReminderCounter -= ticksPerMin;
                    masterInfo.SamplesPerTick++;
                }
            }
        }

        internal void UpdatePatternPositions(int sampleCount)
        {
            // Clear all pattern play positions
            foreach (var machine in buzzCore.SongCore.MachinesList)
            {
                if (machine.Hidden || !machine.Ready) continue;
                foreach (var pattern in machine.PatternsList)
                {
                    pattern.PlayPosition = int.MinValue;
                }
            }

            if (!buzzCore.Playing)
                return;

            var masterInfo = ReBuzzCore.masterInfo;

            if (buzzCore.SoloPattern != null)
            {
                var pattern = buzzCore.SoloPattern;
                pattern.UpdateSoloPlayPosition(sampleCount);
            }
            else
            {
                var song = buzzCore.SongCore;
                int songPlayPosition = song.PlayPosition;
                foreach (var seq in buzzCore.SongCore.SequencesList)
                {
                    // Update trigger event
                    var te = seq.TriggerEventInfo;
                    if (te != null && te.se.Type == SequenceEventType.PlayPattern)
                    {
                        var pattern = te.se.Pattern as PatternCore;
                        if (!te.started)
                        {
                            if (songPlayPosition == te.time)
                            {
                                te.started = true;
                                pattern.PlayPosition = 0;
                                te.PreviousPosition = 0;
                            }
                        }
                        else
                        {
                            int newPos = (songPlayPosition % pattern.Length) * PatternEvent.TimeBase + (masterInfo.PosInTick * PatternEvent.TimeBase / masterInfo.SamplesPerTick);
                            if (te.loop)
                            {
                                pattern.PlayPosition = newPos;
                            }
                            else
                            {
                                // We've looped once so stop.
                                if (te.PreviousPosition > newPos)
                                {
                                    seq.TriggerEventInfo = null;
                                }
                                else
                                {
                                    pattern.PlayPosition = newPos;
                                    te.PreviousPosition = newPos;
                                }
                            }
                        }
                    }

                    foreach (var e in seq.Events)
                    {
                        int eventTime = e.Key;
                        var seqEvent = e.Value;
                        var pc = (seqEvent.Pattern as PatternCore);

                        if (songPlayPosition >= eventTime && songPlayPosition < eventTime + seqEvent.Span)
                        {
                            // Event is playing in sequece
                            if (seqEvent.Type == SequenceEventType.PlayPattern &&
                                seqEvent.Pattern != null)
                            {
                                int timeBase = PatternEvent.TimeBase;
                                int newPatternPos = int.MinValue;

                                // Hack. Pattern editor needs to get adjusted position if play position moved by user or control machine
                                if (song.AdjustPositionOnTick && masterInfo.PosInTick == 0)
                                {
                                    newPatternPos = (songPlayPosition - eventTime) * timeBase +
                                        (ReBuzzCore.masterInfo.PosInTick - 1) * timeBase / masterInfo.SamplesPerTick;

                                    if (newPatternPos < 0)
                                        newPatternPos = int.MinValue;
                                }
                                else
                                {
                                    newPatternPos = (songPlayPosition - eventTime) * timeBase +
                                        (masterInfo.PosInTick * timeBase / masterInfo.SamplesPerTick);
                                }
                                pc.PlayPosition = newPatternPos;
                            }
                        }
                    }
                }
                if (masterInfo.PosInTick == 0)
                {
                    song.AdjustPositionOnTick = false;
                }
            }
        }

        internal void CallTick()
        {
            // First control, then other?
            foreach (var machine in buzzCore.SongCore.MachinesList)
            {
                // Tick should be inexpensive operation so no tasks?
                // Some old machines don't support subtick
                if (machine.IsControlMachine && (ReBuzzCore.masterInfo.PosInTick == 0 ||
                    (engineSettings.SubTickTiming && ReBuzzCore.subTickInfo.PosInSubTick == 0 && machine.DLL.Info.Version > MachineManager.BUZZ_MACHINE_INTERFACE_VERSION_42 &&
                    !buzzCore.Gear.IsSubTickDisabled(machine))))
                {
                    var workInstance = buzzCore.MachineManager.GetMachineWorkInstance(machine);
                    workInstance.Tick(false, false);
                }
            }

            foreach (var machine in buzzCore.SongCore.MachinesList)
            {
                // Tick should be inexpensive operation so no tasks?
                // Some old machines don't support subtick
                if (!machine.IsControlMachine && (ReBuzzCore.masterInfo.PosInTick == 0 ||
                    (engineSettings.SubTickTiming && ReBuzzCore.subTickInfo.PosInSubTick == 0 && machine.DLL.Info.Version > MachineManager.BUZZ_MACHINE_INTERFACE_VERSION_42 &&
                    !buzzCore.Gear.IsSubTickDisabled(machine))))
                {
                    var workInstance = buzzCore.MachineManager.GetMachineWorkInstance(machine);
                    workInstance.Tick(false, false);
                }
            }
        }

        struct PlayingEventsStruct
        {
            public MachineCore Machine { get; internal set; }
            public int Channel { get; internal set; }
            public int MidiNote { get; internal set; }
            internal PatternEvent Pe { get; set; }
            internal IPattern Pattern { get; set; }
        }

        readonly List<PlayingEventsStruct> playingEvents = new List<PlayingEventsStruct>();
        void UpdatePatternColumnEvents(int sampleCount)
        {
            if (!buzzCore.Playing)
            {
                if (playingEvents.Count > 0)
                {
                    foreach (var pes in playingEvents.ToArray())
                    {
                        var machine = pes.Machine;
                        buzzCore.MachineManager.SendMIDINote(machine, pes.Channel, pes.MidiNote, 0);
                        playingEvents.Remove(pes);
                    }
                }
                return;
            }

            if (buzzCore.SoloPattern != null)
            {
                PlayPatternColumnEvents(buzzCore.SoloPattern, sampleCount);
            }
            else
            {
                foreach (var seq in buzzCore.SongCore.Sequences)
                {
                    foreach (var se in seq.Events)
                    {
                        var pattern = se.Value.Pattern as PatternCore;
                        PlayPatternColumnEvents(pattern, sampleCount);
                    }
                }
            }

            // Send not offs
            for (int i = 0; i < playingEvents.Count; i++)
            {
                var pes = playingEvents[i];
                var pe = pes.Pe;
                var machine = pes.Machine;

                if (pe.Time + pe.Duration < pes.Pattern.PlayPosition)
                {
                    buzzCore.MachineManager.SendMIDINote(machine, pes.Channel, pes.MidiNote, 0);
                    playingEvents.RemoveAt(i);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        void PlayPatternColumnEvents(IPattern pattern, int sampleCount)
        {
            if (pattern != null)
            {
                var masterInfo = ReBuzzCore.masterInfo;
                int nextPos = ((sampleCount + masterInfo.PosInTick) * PatternEvent.TimeBase) / masterInfo.SamplesPerTick;

                var columns = pattern.Columns;

                for (int i = 0; i < columns.Count; i++)
                {
                    var column = columns[i];
                    if (column.Machine == null)
                        continue;

                    var ces = column.GetEvents(pattern.PlayPosition, pattern.PlayPosition + nextPos);
                    foreach (var pe in ces)
                    {
                        if (column.Type == PatternColumnType.MIDI)
                        {
                            var data = pe.Value;
                            buzzCore.MachineManager.SendMidiInput(column.Machine, data, false);

                            int b = MIDI.DecodeStatus(data);
                            int data1 = MIDI.DecodeData1(data);
                            int data2 = MIDI.DecodeData2(data);
                            int channel = 0;
                            int commandCode = MIDI.ControlChange;

                            if ((b & 0xF0) == 0xF0)
                            {
                                // both bytes are used for command code in this case
                                commandCode = b;
                            }
                            else
                            {
                                commandCode = (b & 0xF0);
                                channel = (b & 0x0F);
                            }

                            if (commandCode == MIDI.NoteOn)
                            {
                                playingEvents.Add(new PlayingEventsStruct()
                                {
                                    Pe = pe,
                                    Pattern = pattern,
                                    Machine = column.Machine as MachineCore,
                                    Channel = channel,
                                    MidiNote = data1
                                });
                            }
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public int ReadWork(float[] buffer, int offset, int workSamplesCount)
        {
            foreach (var m in buzzCore.SongCore.MachinesList)
                m.workDone = false;

            // Iterate machine connection graph staring from Master
            if (buzzCore.SongCore.MachinesList.Count == 0)
                return workSamplesCount;
            var master = buzzCore.SongCore.MachinesList[0];

            if (!multiThreadingEnabled)
            {
                if (engineSettings.UseCachedWorkOrder)
                    HandleWorkAlgorithmSingleThreadCached(master, workSamplesCount);
                else
                    HandleWorkAlgorithmSingleThread(master, workSamplesCount);
            }
            else if (algorithm == 0)
            {
                if (engineSettings.UseCachedWorkOrder)
                    HandleWorkAlgorithmGroupsCached(master, workSamplesCount);
                else
                    HandleWorkAlgorithmGroups(master, workSamplesCount);
            }
            else if (algorithm == 1)
            {
                // Recursive processing of audio paths
                HandleWorkAlgorithmRecursive(master, workSamplesCount);
            }
            else if (algorithm == 2)
            {
                if (engineSettings.UseCachedWorkOrder)
                    HandleWorkAlgorithm2Cached(master, workSamplesCount);
                else
                    HandleWorkAlgorithm2(master, workSamplesCount);
            }

            // Mix all to single L + R signal
            Sample[] samples = master.GetStereoSamples(workSamplesCount);
            for (int i = 0; i < workSamplesCount; i++)
            {
                buffer[offset + i * 2] = samples[i].L * (float)buzzCore.MasterVolume;
                buffer[offset + i * 2 + 1] = samples[i].R * (float)buzzCore.MasterVolume;
            }

            master.UpdateIsActive();

            return workSamplesCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void HandleWorkAlgorithmGroups(MachineCore master, int workSamplesCount)
        {
            while (true)
            {
                workSet.Clear();

                // 1. Get machines that can be processed
                CollectEditorMachinesThatCanWork();
                HandleWorkList(workSamplesCount);
                CollectControlMachinesThatCanWork();
                HandleWorkList(workSamplesCount);
                CollectMachinesThatCanWork(master);

                if (workSet.Count == 1 && workSet.First().DLL.Info.Type == MachineType.Master) // Master
                {
                    break;
                }

                // If workList.Count == 1 then call work from this thread
                if (workSet.Count == 1)
                {
                    var machine = workSet.First();

                    // Call work and update machine activity flag

                    var workInstance = buzzCore.MachineManager.GetMachineWorkInstance(machine);
                    workInstance.TickAndWork(workSamplesCount, true);

                    machine.workDone = true;
                }
                else
                {
                    dispatchSamples = workSamplesCount;
                    workTasks.Clear();

                    int n = FillAndSortDesc();
                    for (int s = 0; s < n; s++)
                        workTasks.Add(AudioEngine.TaskFactoryAudio.StartNew(workOneGroups, sortScratch[s]));

                    // Wait all tasks to complete
                    Task.WaitAll(workTasks);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        internal void HandleWorkAlgorithmRecursive(MachineCore master, int numRead)
        {
            workSet.Clear();

            // 1. Get machines that can be processed
            CollectEditorMachinesThatCanWork();
            HandleWorkList(numRead);
            CollectControlMachinesThatCanWork();
            HandleWorkList(numRead);

            // Recursively go through the rest
            HandleWorkRecursive(master, numRead);
        }

        readonly List<Task> workTasks = new List<Task>(100);
        private readonly EngineSettings engineSettings;

        // Interim §5: reused scratch + cached non-capturing dispatch delegates
        // (kills per-chunk OrderByDescending alloc and per-task display-class capture).
        private MachineCore[] sortScratch = new MachineCore[64];
        private int dispatchSamples;
        private readonly Action<object> workOneList;
        private readonly Action<object> workOneGroups;

        // Copy workSet into sortScratch and stable-descending insertion-sort by
        // performanceLastCount. Only one algorithm path runs per ReadWork (single
        // audio thread), so a single shared scratch is safe.
        private int FillAndSortDesc()
        {
            int n = workSet.Count;
            if (sortScratch.Length < n)
                sortScratch = new MachineCore[Math.Max(n, sortScratch.Length * 2)];

            int i = 0;
            foreach (var m in workSet)
                sortScratch[i++] = m;

            for (int a = 1; a < n; a++)
            {
                var key = sortScratch[a];
                long kc = key.performanceLastCount;
                int b = a - 1;
                while (b >= 0 && sortScratch[b].performanceLastCount < kc)
                {
                    sortScratch[b + 1] = sortScratch[b];
                    b--;
                }
                sortScratch[b + 1] = key;
            }
            return n;
        }

        // HandleWorkList body: work only; caller sets workDone on the dispatch thread.
        private void WorkOneList(object state)
        {
            var machine = (MachineCore)state;
            var workInstance = buzzCore.MachineManager.GetMachineWorkInstance(machine);
            workInstance.TickAndWork(dispatchSamples, true);
        }

        // Groups body: guard + set workDone inside, matching the original lambda.
        private void WorkOneGroups(object state)
        {
            var machine = (MachineCore)state;
            if (machine.workDone)
                return;
            var workInstance = buzzCore.MachineManager.GetMachineWorkInstance(machine);
            workInstance.TickAndWork(dispatchSamples, true);
            machine.workDone = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        void HandleWorkList(int numRead)
        {
            dispatchSamples = numRead;
            workTasks.Clear();
            foreach (var machine in workSet)
            {
                workTasks.Add(AudioEngine.TaskFactoryAudio.StartNew(workOneList, machine));
                machine.workDone = true;
            }

            // Wait all tasks to complete
            Task.WaitAll(workTasks);
            workSet.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        internal void HandleWorkRecursive(MachineCore machine, int numRead)
        {
            lock (machine.workLock)
            {
                // Was work called for this machine already?
                if (machine.workDone)
                    return;

                machine.workTasks.Clear();

                // Process most time consuming branches first
                //
                // Note: We use OrderByDescending to handle most computing heavy branches first.
                // OrderByDescending should perform ok bececause there is only few inputs per machine.
                foreach (var input in machine.AllInputs.OrderByDescending(c => (c.Source as MachineCore).performanceBranchCount))
                {
                    var sourceMachine = input.Source as MachineCore;

                    // Was work called for this sourceMachine already?
                    if (!sourceMachine.workDone)
                    {
                        // Handle inputs
                        var t = AudioEngine.TaskFactoryAudio.StartNew(() =>
                        //var t = Task.Run(() =>
                        {
                            HandleWorkRecursive(sourceMachine, numRead);
                        });
                        machine.workTasks.Add(t);
                    }
                }

                // Wait all input tasks to complete
                if (machine.workTasks.Count > 0)
                {
                    Task.WaitAll(machine.workTasks);
                }

                // All inputs handled (if any)

                // Call work and update machine activity flag
                var workInstance = buzzCore.MachineManager.GetMachineWorkInstance(machine);
                workInstance.TickAndWork(numRead, true);

                // Count overall branch process time
                machine.performanceBranchCount = machine.performanceLastCount;
                foreach (var input in machine.AllInputs)
                {
                    machine.performanceBranchCount += (input.Source as MachineCore).performanceBranchCount;
                }

                machine.workDone = true;
            }

            // Some machines report the active state wrong, double check
            machine.IsActive = machine.GetActivity(numRead);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void HandleWorkAlgorithm2(MachineCore master, int workSamplesCount)
        {
            workEngine.PrepareEngine(workSamplesCount);
            while (true)
            {
                workSet.Clear();

                // 1. Get machines that can be processed
                CollectEditorMachinesThatCanWork();
                HandleWorkList(workSamplesCount);
                CollectControlMachinesThatCanWork();
                HandleWorkList(workSamplesCount);
                CollectMachinesThatCanWork(master);

                if ((workSet.Count == 1) && (workSet.First().DLL.Info.Type == MachineType.Master)) // Master
                {
                    break;
                }

                int n = FillAndSortDesc();
                for (int s = 0; s < n; s++)
                {
                    var machine = sortScratch[s];
                    // Add work instances to be processed
                    var workInstance = buzzCore.MachineManager.GetMachineWorkInstance(machine);
                    workEngine.AddWork(workInstance);
                    machine.workDone = true;
                }

                // Notify workEngine that all jobs are added and processing can start. This way we can be sure that
                // all threads are properly finished
                workEngine.AllJobsAdded();

                // Wait all tasks to complete
                if (workSet.Count > 0)
                {
                    var jobs = workEngine.AllDoneEvent();
                    jobs.WaitOne();
                }

                if (stopped)
                {
                    break;
                }
            }
        }

        // ===== #107 prototype: cached topological work order =====
        // Gated by engineSettings.UseCachedWorkOrder. Shares the cache build
        // (CachedWorkOrder) across dispatch styles; only consumption differs.
        // Algorithm 1 (Recursive) is intentionally untouched: it has no per-chunk
        // re-scan, so the cache does not apply to it.

        private readonly CachedWorkOrder cachedOrder = new CachedWorkOrder();

        // Runs on the audio thread under AudioLock (held for the whole buffer),
        // so the rebuild can never race a topology mutation. Rebuild is lazy:
        // at most one rebuild per buffer, and only when the topology changed.
        private CachedWorkOrder GetOrBuildOrder()
        {
            var song = buzzCore.SongCore;
            long gen = buzzCore.TopologyGeneration;
            if (cachedOrder.BuiltGeneration != gen ||
                cachedOrder.BuiltMachineCount != song.MachinesList.Count)
            {
                cachedOrder.Rebuild(song);
                cachedOrder.BuiltGeneration = gen;
            }
            return cachedOrder;
        }

        // Copy a wave into sortScratch and stable-descending insertion-sort by
        // performanceLastCount (kept per-chunk/dynamic, like FillAndSortDesc).
        private int FillAndSortDescFrom(MachineCore[] wave)
        {
            int n = wave.Length;
            if (sortScratch.Length < n)
                sortScratch = new MachineCore[Math.Max(n, sortScratch.Length * 2)];

            for (int i = 0; i < n; i++)
                sortScratch[i] = wave[i];

            for (int a = 1; a < n; a++)
            {
                var key = sortScratch[a];
                long kc = key.performanceLastCount;
                int b = a - 1;
                while (b >= 0 && sortScratch[b].performanceLastCount < kc)
                {
                    sortScratch[b + 1] = sortScratch[b];
                    b--;
                }
                sortScratch[b + 1] = key;
            }
            return n;
        }

        // Dispatch a prefix partition (editors or controls) as one parallel
        // batch + barrier, mirroring CollectEditor/Control + HandleWorkList.
        // Ready is re-tested here (a native crash can clear it mid-buffer).
        private void DispatchPrefixCached(MachineCore[] prefix, int numRead)
        {
            workSet.Clear();
            foreach (var m in prefix)
                if (m.Ready && !m.workDone)
                    workSet.Add(m);

            if (workSet.Count > 0)
                HandleWorkList(numRead); // dispatches workSet, sets workDone, clears workSet
            else
                workSet.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void HandleWorkAlgorithm2Cached(MachineCore master, int workSamplesCount)
        {
            var order = GetOrBuildOrder();
            if (order.HasCycle)
            {
                // Deterministic fallback: legacy walk (its CanConnect gate keeps
                // valid songs acyclic; this only fires on a malformed graph).
                HandleWorkAlgorithm2(master, workSamplesCount);
                return;
            }

            workEngine.PrepareEngine(workSamplesCount);

            DispatchPrefixCached(order.EditorPrefix, workSamplesCount);
            DispatchPrefixCached(order.ControlPrefix, workSamplesCount);

            foreach (var wave in order.AudioWaves)
            {
                if (stopped)
                    break;

                int n = FillAndSortDescFrom(wave);
                for (int s = 0; s < n; s++)
                {
                    var machine = sortScratch[s];
                    var workInstance = buzzCore.MachineManager.GetMachineWorkInstance(machine);
                    workEngine.AddWork(workInstance);
                    machine.workDone = true;
                }

                workEngine.AllJobsAdded();
                if (n > 0)
                    workEngine.AllDoneEvent().WaitOne();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void HandleWorkAlgorithmGroupsCached(MachineCore master, int workSamplesCount)
        {
            var order = GetOrBuildOrder();
            if (order.HasCycle)
            {
                HandleWorkAlgorithmGroups(master, workSamplesCount);
                return;
            }

            // Groups uses no workEngine (it is null for algorithm 0); prefix and
            // waves dispatch via AudioEngine.TaskFactoryAudio + Task.WaitAll,
            // matching the legacy path. workOneGroups guards/sets workDone.
            DispatchPrefixCached(order.EditorPrefix, workSamplesCount);
            DispatchPrefixCached(order.ControlPrefix, workSamplesCount);

            foreach (var wave in order.AudioWaves)
            {
                int n = FillAndSortDescFrom(wave);
                if (n == 1)
                {
                    var machine = sortScratch[0];
                    var workInstance = buzzCore.MachineManager.GetMachineWorkInstance(machine);
                    workInstance.TickAndWork(workSamplesCount, true);
                    machine.workDone = true;
                }
                else if (n > 1)
                {
                    dispatchSamples = workSamplesCount;
                    workTasks.Clear();
                    for (int s = 0; s < n; s++)
                        workTasks.Add(AudioEngine.TaskFactoryAudio.StartNew(workOneGroups, sortScratch[s]));
                    Task.WaitAll(workTasks);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void HandleWorkAlgorithmSingleThreadCached(MachineCore master, int workSamplesCount)
        {
            var order = GetOrBuildOrder();
            if (order.HasCycle)
            {
                HandleWorkAlgorithmSingleThread(master, workSamplesCount);
                return;
            }

            // Controls-first prefix (Ready-gated), then audio waves in topo order.
            // This also tightens the legacy single-thread path, which works
            // editors/controls/audio together in HashSet order with no guaranteed
            // controls-before-generators ordering.
            WorkSerialCached(order.EditorPrefix, workSamplesCount, readyGated: true);
            WorkSerialCached(order.ControlPrefix, workSamplesCount, readyGated: true);
            foreach (var wave in order.AudioWaves)
                WorkSerialCached(wave, workSamplesCount, readyGated: false);
        }

        private void WorkSerialCached(MachineCore[] list, int numRead, bool readyGated)
        {
            foreach (var m in list)
            {
                if (m.workDone)
                    continue;
                if (readyGated && !m.Ready)
                    continue;
                var workInstance = buzzCore.MachineManager.GetMachineWorkInstance(m);
                workInstance.TickAndWork(numRead, true);
                m.workDone = true;
            }
        }
        // ===== end #107 prototype =====

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void HandleWorkAlgorithmSingleThread(MachineCore master, int workSamplesCount)
        {
            while (true)
            {
                workSet.Clear();

                // 1. Get machines that can be processed
                CollectEditorMachinesThatCanWork();
                CollectControlMachinesThatCanWork();
                CollectMachinesThatCanWork(master);

                if (workSet.Count == 1 && workSet.First().DLL.Info.Type == MachineType.Master) // Master
                {
                    break;
                }

                foreach (var machine in workSet)
                {
                    var workInstance = buzzCore.MachineManager.GetMachineWorkInstance(machine);
                    workInstance.TickAndWork(workSamplesCount, true);
                    machine.workDone = true;
                }
            }
        }

        private void CollectMachinesThatCanWork(MachineCore machine)
        {
            bool machineCanWork = true;
            foreach (var input in machine.AllInputs)
            {
                var sourceMachine = input.Source as MachineCore;

                // Was work called for this machine already?
                if (!sourceMachine.workDone)
                {
                    machineCanWork = false;
                    CollectMachinesThatCanWork(sourceMachine);
                }
            }

            if (machineCanWork)
            {
                workSet.Add(machine);
            }
        }
        private void CollectEditorMachinesThatCanWork()
        {
            foreach (var mac in (buzzCore.Song as SongCore).MachinesList)
            {
                var machine = mac;
                // Was work called for this editor machine already?
                if (machine.DLL.Info.Flags.HasFlag(MachineInfoFlags.CONTROL_MACHINE) && machine.Hidden && !machine.workDone && machine.Ready)
                {
                    workSet.Add(machine);
                }
            }
        }

        private void CollectControlMachinesThatCanWork()
        {
            foreach (var mac in buzzCore.Song.Machines)
            {
                var machine = mac as MachineCore;
                // Was work called for this control machine already?
                if (machine.DLL.Info.Flags.HasFlag(MachineInfoFlags.CONTROL_MACHINE) && !machine.workDone && machine.Ready)
                {
                    workSet.Add(machine);
                }
            }
        }

        private void CollectInputMachines(MachineCore machine)
        {
            foreach (var input in machine.AllInputs)
            {
                var sourceMachine = input.Source as MachineCore;
                workSet.Add(machine);
            }
        }

        internal void Stop()
        {
            stopped = true;
            workSet.Clear();
        }

        internal int ThreadReadSpeedAdjust(float[] buffer, int offset, int count)
        {
            if (buzzCore.Speed == 0)
            {
                return MainAudioFillBuffer(buffer, offset, count);
            }
            else
            {
                double div = (Math.Abs(buzzCore.Speed) / 20.0 + 1.0);

                int sCount = (int)(count / div);
                sCount = sCount % 2 != 0 ? sCount - 1 : sCount;

                if (sCount == 0)
                    return 0;

                float[] sBuffer = new float[sCount];
                MainAudioFillBuffer(sBuffer, 0, sCount);

                SpeedDown(buffer, offset, count, sBuffer, sCount, SpeedAdjustLinear);
            }
            return count;
        }

        internal static void SpeedDown(float[] buffer, int offset, int count, float[] sBuffer, int sCount, bool SpeedAdjustLinear)
        {
            float step = sCount / (float)count;
            float pos = 0;

            // Linear interpolation. FIXME
            for (int i = 0; i < count; i += 2)
            {
                if (SpeedAdjustLinear)
                {
                    int index = (int)pos * 2; // Stereo
                    float t = pos - (int)pos; // pos between x1 & x2

                    float x0 = sBuffer[index];
                    float x1 = index < sBuffer.Length - 2 ? sBuffer[index + 2] : x0;
                    float k = x1 - x0;

                    buffer[offset + i] = x0 + t * k;

                    index++;
                    x0 = sBuffer[index];
                    x1 = index < sBuffer.Length - 2 ? sBuffer[index + 2] : x0;
                    k = x1 - x0;

                    buffer[offset + i + 1] = x0 + t * k;

                    pos += step;
                }
                else
                {
                    int index = (int)pos * 2; // Stereo
                    float t = pos - (int)pos; // pos between x1 & x2

                    // Left
                    float x0 = index < 2 ? 0 : sBuffer[index - 2];
                    float x1 = sBuffer[index];
                    float x2 = index < sBuffer.Length - 2 ? sBuffer[index + 2] : x1;
                    float x3 = index < sBuffer.Length - 4 ? sBuffer[index + 4] : x2;

                    buffer[offset + i] = InterpolateHermite(x0, x1, x2, x3, t);

                    // Right
                    index++;
                    x0 = index < 2 ? 0 : sBuffer[index - 2];
                    x1 = sBuffer[index];
                    x2 = index < sBuffer.Length - 2 ? sBuffer[index + 2] : x1;
                    x3 = index < sBuffer.Length - 4 ? sBuffer[index + 4] : x2;

                    buffer[offset + i + 1] = InterpolateHermite(x0, x1, x2, x3, t);

                    pos += step;
                }
            }
        }

        internal static float InterpolateHermite(float x0, float x1, float x2, float x3, float t)
        {
            float diff = x1 - x2;
            float c1 = x2 - x0;
            float c3 = x3 - x0 + 3 * diff;
            float c2 = -(2 * diff + c1 + c3);
            return 0.5f * ((c3 * t + c2) * t + c1) * t + x1;
        }
    }
}
