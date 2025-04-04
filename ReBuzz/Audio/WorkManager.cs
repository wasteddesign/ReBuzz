using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using ReBuzz.Common;
using ReBuzz.Core;
using ReBuzz.MachineManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using BuzzGUI.Common.Settings;

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

        internal static BuzzMasterInfo MasterInfoStruct;
        unsafe internal static byte[] MasterInfoData = new byte[sizeof(BuzzMasterInfo)];

        bool multiThreadingEnabled = false;

        private readonly ReBuzzCore buzzCore;
        readonly WorkThreadEngine workEngine;

        readonly int algorithm;

        readonly Dictionary<MachineCore, bool> workList = new Dictionary<MachineCore, bool>();
        private bool stopped;
        readonly bool SpeedAdjustLinear = false;

        float[] playWaveBuffer = new float[256 * 2];

        public WorkManager(ReBuzzCore buzzCore, WorkThreadEngine workEngine, int algorithm, EngineSettings settings)
        {
            this.buzzCore = buzzCore;
            this.workEngine = workEngine;
            this.algorithm = algorithm;
            engineSettings = settings;
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

        internal int workBufferOffset;
        // Avoid new object creation to minimize GC.
        public int ThreadRead(float[] buffer, int offset, int count)
        {

            lock (ReBuzzCore.AudioLock)
            {
                long time = DateTime.UtcNow.Ticks;

                multiThreadingEnabled = engineSettings.Multithreading;

                var subTickInfo = ReBuzzCore.subTickInfo;
                var masterInfo = ReBuzzCore.masterInfo;
                int subTickSize = subTickInfo.SamplesPerSubTick;
                int subTickWorkSamplesSize = subTickSize / 2 + 1; // Samples per work call
                int reminingBuffer = count;
                workBufferOffset = offset;

                if (buzzCore.TPB != masterInfo.TicksPerBeat ||
                    buzzCore.BPM != masterInfo.BeatsPerMin)
                {
                    buzzCore.UpdateMasterInfo();
                }

                Utils.FlipDenormalDC();

                while (reminingBuffer > 0)
                {
                    int samplesToProcess = Math.Min(reminingBuffer / 2, subTickWorkSamplesSize);

                    // Initiate master info for Audio Messages
                    CopyMasterInfo();

                    // Update SubTick
                    UpdateSubTickLength();

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
                    if (subTickInfo.PosInSubTick + samplesToProcess > subTickInfo.SamplesPerSubTick)
                    {
                        // Make sure tick will be zero
                        samplesToProcess = subTickInfo.SamplesPerSubTick - subTickInfo.PosInSubTick;
                        if (samplesToProcess < 0)
                            return 0;
                    }

                    // Update position info in patterns
                    UpdatePatternPositions(samplesToProcess);

                    // Call Pattern Column Events
                    UpdatePatternColumnEvents(samplesToProcess);

                    // Update Managed Machine host info
                    buzzCore.MachineManager.UpdateMasterAndSubTickInfoToHost();

                    // Tick all machines first and then call tick again if sendcontrolchanges flag set?
                    CallTick();

                    // Call work
                    ReadWork(buffer, workBufferOffset, samplesToProcess);

                    // Reset non static parameteres if tick == 0
                    UpdateNonStaticParametersToDefault();

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

                    // Update frame count
                    unchecked { ReBuzzCore.GlobalState.AudioFrame++; }
                }

                float audioOutMul = 1 / 32768.0f;
                for (int i = 0; i < count; i++)
                {
                    buffer[offset + i] = Utils.FlushDenormalToZero(buffer[offset + i] * audioOutMul); // From Buzz to audio output scale
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

                buzzCore.PerformanceCurrent.EnginePerformanceCount += (DateTime.UtcNow.Ticks - time);

                return count;
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
            }
        }

        private void UpdateNonStaticParametersToDefault()
        {
            int noRecord = 1 << 16;
            foreach (var machine in buzzCore.SongCore.MachinesList.Where(m => !m.DLL.IsManaged && m.Ready))
            {
                if (!machine.sendControlChangesFlag && (ReBuzzCore.masterInfo.PosInTick == 0 || (engineSettings.SubTickTiming && ReBuzzCore.subTickInfo.PosInSubTick == 0 && machine.DLL.Info.Version >= MachineManager.BUZZ_MACHINE_INTERFACE_VERSION_42)))
                {
                    foreach (var p in machine.ParameterGroups[0].Parameters)
                    {
                        // Reset parameters so they wont be triggered next Tick
                        p.SetValue(noRecord, p.NoValue);
                    }
                    foreach (var p in machine.ParameterGroups[1].Parameters)
                    {
                        // Reset parameters so they wont be triggered next Tick
                        p.SetValue(noRecord, p.NoValue);
                    }
                    foreach (var p in machine.ParameterGroups[2].Parameters)
                    {
                        for (int i = 0; i < machine.TrackCount; i++)
                        {
                            // Reset parameters so they wont be triggered next Tick
                            p.SetValue(i | noRecord, p.NoValue);
                        }
                    }
                }
            }
        }

        internal void UpdatePatternPositions(int sampleCount)
        {
            // Clear all pattern play positions
            foreach (var machine in buzzCore.SongCore.Machines)
            {
                foreach (var pattern in machine.Patterns)
                {
                    (pattern as PatternCore).PlayPosition = int.MinValue;
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
                    if (te != null)
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

        readonly List<Task> tickTasks = new List<Task>();

        internal void CallTickMultiThread()
        {
            foreach (var machine in buzzCore.SongCore.MachinesList)
            {
                // Tick should be inexpensive operation so no tasks?
                // Some old machines don't support subtick
                if (machine.IsControlMachine && ReBuzzCore.masterInfo.PosInTick == 0 ||
                    (engineSettings.SubTickTiming && ReBuzzCore.subTickInfo.PosInSubTick == 0 && machine.DLL.Info.Version > MachineManager.BUZZ_MACHINE_INTERFACE_VERSION_42))
                {
                    var t = AudioEngine.TaskFactoryAudio.StartNew(() =>
                    {
                        var workInstance = buzzCore.MachineManager.GetMachineWorkInstance(machine);
                        workInstance.Tick(false, false);
                    });
                    tickTasks.Add(t);
                }
            }
            Task.WaitAll(tickTasks.ToArray());
            tickTasks.Clear();

            foreach (var machine in buzzCore.SongCore.MachinesList)
            {
                if (!machine.IsControlMachine && ReBuzzCore.masterInfo.PosInTick == 0 ||
                    (engineSettings.SubTickTiming && ReBuzzCore.subTickInfo.PosInSubTick == 0 && machine.DLL.Info.Version > MachineManager.BUZZ_MACHINE_INTERFACE_VERSION_42))
                {
                    var t = AudioEngine.TaskFactoryAudio.StartNew(() =>
                    {
                        var workInstance = buzzCore.MachineManager.GetMachineWorkInstance(machine);
                        workInstance.Tick(false, false);
                    });
                    tickTasks.Add(t);
                }
            }
            Task.WaitAll(tickTasks.ToArray());
            tickTasks.Clear();
        }

        internal void CallTick()
        {
            // First control, then other?
            foreach (var machine in buzzCore.SongCore.MachinesList)
            {
                // Tick should be inexpensive operation so no tasks?
                // Some old machines don't support subtick
                if (machine.IsControlMachine && (ReBuzzCore.masterInfo.PosInTick == 0 ||
                    (engineSettings.SubTickTiming && ReBuzzCore.subTickInfo.PosInSubTick == 0 && machine.DLL.Info.Version > MachineManager.BUZZ_MACHINE_INTERFACE_VERSION_42)))
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
                    (engineSettings.SubTickTiming && ReBuzzCore.subTickInfo.PosInSubTick == 0 && machine.DLL.Info.Version > MachineManager.BUZZ_MACHINE_INTERFACE_VERSION_42)))
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

        void PlayPatternColumnEvents(IPattern pattern, int sampleCount)
        {
            if (pattern != null)
            {
                var masterInfo = ReBuzzCore.masterInfo;
                int nextPos = (sampleCount + masterInfo.PosInTick * PatternEvent.TimeBase) / masterInfo.SamplesPerTick;
                foreach (var column in pattern.Columns.ToArray())
                {
                    foreach (var pe in column.GetEvents(pattern.PlayPosition, pattern.PlayPosition + nextPos))
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

        public int ReadWork(float[] buffer, int offset, int workSamplesCount)
        {
            foreach (var m in buzzCore.SongCore.MachinesList)
                m.workDone = false;

            // Iterate machine connection graph staring from Master
            var master = buzzCore.SongCore.MachinesList.FirstOrDefault(m => m.DLL.Info.Type == MachineType.Master);
            if (master == null)
                return workSamplesCount;

            if (!multiThreadingEnabled)
            {
                HandleWorkAlgorithmSingleThread(master, workSamplesCount);
            }
            else if (algorithm == 0)
            {
                HandleWorkAlgorithmGroups(master, workSamplesCount);
            }
            else if (algorithm == 1)
            {
                // Recursive processing of audio paths
                HandleWorkAlgorithmRecursive(master, workSamplesCount);
            }
            else if (algorithm == 2)
            {
                HandleWorkAlgorithm2(master, workSamplesCount);
            }

            // Mix all to single L + R signal
            Sample[] samples = master.GetStereoSamples(workSamplesCount);
            for (int i = 0; i < workSamplesCount; i++)
            {
                buffer[offset + i * 2] = Utils.FlushDenormalToZero(samples[i].L * (float)buzzCore.MasterVolume);
                buffer[offset + i * 2 + 1] = Utils.FlushDenormalToZero(samples[i].R * (float)buzzCore.MasterVolume);
            }

            master.IsActive = master.GetActivity();

            return workSamplesCount;
        }


        private void HandleWorkAlgorithmGroups(MachineCore master, int workSamplesCount)
        {
            while (true)
            {
                workList.Clear();

                // 1. Get machines that can be processed
                CollectEditorMachinesThatCanWork();
                HandleWorkList(workSamplesCount);
                CollectControlMachinesThatCanWork();
                HandleWorkList(workSamplesCount);
                CollectMachinesThatCanWork(master);

                if (workList.Count == 1 && workList.Keys.First().DLL.Info.Type == MachineType.Master) // Master
                {
                    break;
                }

                // If workList.Count == 1 then call work from this thread
                if (workList.Count == 1)
                {
                    var machine = workList.Keys.First();

                    // Call work and update machine activity flag

                    var workInstance = buzzCore.MachineManager.GetMachineWorkInstance(machine);
                    workInstance.TickAndWork(workSamplesCount, true);

                    machine.workDone = true;
                }
                else
                {
                    List<Task> workTasks = new List<Task>();

                    foreach (var machine in workList.Keys.OrderByDescending(m => m.performanceLastCount))
                    {
                        var t = AudioEngine.TaskFactoryAudio.StartNew(() =>
                        {
                            if (machine.workDone)
                                return;

                            // Call work and update machine activity flag
                            var workInstance = buzzCore.MachineManager.GetMachineWorkInstance(machine);
                            workInstance.TickAndWork(workSamplesCount, true);

                            machine.workDone = true;
                        });

                        workTasks.Add(t);
                    }

                    // Wait all tasks to complete
                    Task.WaitAll(workTasks.ToArray());
                }
            }
        }

        internal void HandleWorkAlgorithmRecursive(MachineCore master, int numRead)
        {
            workList.Clear();

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

        void HandleWorkList(int numRead)
        {
            workTasks.Clear();
            foreach (var machine in workList.Keys)
            {
                var t = AudioEngine.TaskFactoryAudio.StartNew(() =>
                {
                    // Call work and update machine activity flag
                    var workInstance = buzzCore.MachineManager.GetMachineWorkInstance(machine);
                    workInstance.TickAndWork(numRead, true);
                });
                workTasks.Add(t);
                machine.workDone = true;
            }

            // Wait all tasks to complete
            Task.WaitAll(workTasks.ToArray());
            workList.Clear();
        }

        internal void HandleWorkRecursive(MachineCore machine, int numRead)
        {
            lock (machine.workLock)
            {
                // Was work called for this machine already?
                if (machine.workDone)
                    return;

                machine.workTasks.Clear();

                // Process most time consuming branches first
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
                    Task.WaitAll(machine.workTasks.ToArray());
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
            machine.IsActive = machine.GetActivity();
        }

        private void HandleWorkAlgorithm2(MachineCore master, int workSamplesCount)
        {
            workEngine.PrepareEngine(workSamplesCount);
            while (true)
            {
                workList.Clear();

                // 1. Get machines that can be processed
                CollectEditorMachinesThatCanWork();
                HandleWorkList(workSamplesCount);
                CollectControlMachinesThatCanWork();
                HandleWorkList(workSamplesCount);
                CollectMachinesThatCanWork(master);

                if ((workList.Count == 1) && (workList.Keys.First().DLL.Info.Type == MachineType.Master)) // Master
                {
                    break;
                }

                foreach (var machine in workList.Keys.OrderByDescending(m => m.performanceLastCount))
                {
                    // Add work instances to be processed
                    var workInstance = buzzCore.MachineManager.GetMachineWorkInstance(machine);
                    workEngine.AddWork(workInstance);
                    machine.workDone = true;
                }

                // Notify workEngine that all jobs are added and processing can start. This way we can be sure that
                // all threads are properly finished
                workEngine.AllJobsAdded();

                // Wait all tasks to complete
                if (workList.Count > 0)
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

        private void HandleWorkAlgorithmSingleThread(MachineCore master, int workSamplesCount)
        {
            while (true)
            {
                workList.Clear();

                // 1. Get machines that can be processed
                CollectEditorMachinesThatCanWork();
                CollectControlMachinesThatCanWork();
                CollectMachinesThatCanWork(master);

                if (workList.Count == 1 && workList.Keys.First().DLL.Info.Type == MachineType.Master) // Master
                {
                    break;
                }

                foreach (var machine in workList.Keys)
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
                workList[machine] = true;
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
                    workList[machine] = true;
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
                    workList[machine] = true;
                }
            }
        }

        private void CollectInputMachines(MachineCore machine)
        {
            foreach (var input in machine.AllInputs)
            {
                var sourceMachine = input.Source as MachineCore;
                workList[machine] = true;
            }
        }

        internal void Stop()
        {
            stopped = true;
            workList.Clear();
        }

        internal int ThreadReadSpeedAdjust(float[] buffer, int offset, int count)
        {
            if (buzzCore.Speed == 0)
            {
                return ThreadRead(buffer, offset, count);
            }
            else
            {
                double div = (Math.Abs(buzzCore.Speed) / 20.0 + 1.0);

                int sCount = (int)(count / div);
                sCount = sCount % 2 != 0 ? sCount - 1 : sCount;

                float[] sBuffer = new float[sCount];
                ThreadRead(sBuffer, 0, sCount);

                SpeedDown(buffer, offset, count, sBuffer, sCount, SpeedAdjustLinear);
            }
            return count;
        }

        internal static void SpeedDown(float[] buffer, int offset, int count, float[] sBuffer, int sCount, bool SpeedAdjustLinear)
        {
            float step = Utils.FlushDenormalToZero(sCount / (float)count);
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
