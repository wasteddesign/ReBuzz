using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using ReBuzz.Common;
using ReBuzz.Core;
using ReBuzz.ManagedMachine;
using ReBuzz.NativeMachine;
using System;
using System.Collections.Generic;
using System.Threading;

namespace ReBuzz.MachineManagement
{
    public class MachineWorkInstance
    {
        public MachineCore Machine;
        private readonly ReBuzzCore buzz;

        internal ManagedMachineHost manageMachineHost;
        internal NativeMachineHost nativeMachineHost;

        internal readonly int MAX_MULTI_IO_CHANNELS = 64;

        public bool TickSent { get; private set; }

        public MachineWorkInstance(MachineCore machine, ReBuzzCore reBuzz)
        {
            this.Machine = machine;
            this.buzz = reBuzz;

            var ManagedMachines = reBuzz.MachineManager.ManagedMachines;
            var NativeMachines = reBuzz.MachineManager.NativeMachines;

            if (ManagedMachines.ContainsKey(Machine))
                manageMachineHost = ManagedMachines[Machine];
            if (NativeMachines.ContainsKey(Machine))
                nativeMachineHost = NativeMachines[Machine];
        }

        internal bool WorkMachine(int nSamples)
        {
            bool isActive = false;
            DateTime dtStart = DateTime.Now;
            Machine.EngineThreadId = Thread.CurrentThread.ManagedThreadId;

            foreach (var tracks in Machine.setMachineTrackCountList)
            {
                Machine.SetTrackCount(tracks);
            }
            Machine.setMachineTrackCountList.Clear();

            // Some machines expect Tick() before Work()
            if (TickSent)
            {
                // Don't call Work() if bypassed
                if (Machine.IsBypassed || Machine.IsSeqThru || Machine.MachineDLL.IsMissing ||
                    (Machine.IsMuted && !Global.EngineSettings.ProcessMutedMachines))
                {
                    Sample[] samples = Machine.GetStereoSamples(nSamples);
                    Machine.UpdateOutputs(samples, false);

                    foreach (var output in Machine.Outputs)
                    {
                        (output as MachineConnectionCore).DoTap(samples, true, buzz.GetSongTime());
                    }
                }
                else if (Machine.Ready && manageMachineHost != null)
                {
                    isActive = WorkMachineManaged(nSamples);

                }
                else if (Machine.Ready && nativeMachineHost != null)
                {
                    isActive = WorkMachineNative(nSamples);
                }
            }
            DateTime dtEnd = DateTime.Now;
            long timeDelta = dtEnd.Ticks - dtStart.Ticks;
            Machine.PerformanceDataCurrent.PerformanceCount += timeDelta;
            Machine.PerformanceDataCurrent.CycleCount += timeDelta;
            Machine.PerformanceDataCurrent.MaxEngineLockTime = 0;
            Machine.PerformanceDataCurrent.SampleCount += nSamples;

            return isActive;
        }

        readonly List<Sample[]> multiSamplesOut = new List<Sample[]>();

        private bool WorkMachineNative(int nSamples)
        {
            bool isActive;
            bool isReadWriteFlag = Machine.DLL.Info.Type == MachineType.Effect;
            // Native machines
            var nmh = nativeMachineHost;
            var audiom = nmh.AudioMessage;

            var flags = Machine.DLL.Info.Flags;

            if (Machine.DLL.Info.Type == MachineType.Effect)
            {
                if (Machine.Inputs.Count == 0)
                {
                    foreach (var mo in Machine.Outputs)
                    {
                        (mo as MachineConnectionCore).ClearBuffer();
                    }
                    return false;
                }
            }

            if (!Global.EngineSettings.ProcessMutedMachines && Machine.IsMuted)
            {
                return false;
            }

            // If machine wants to do input mixing, first call Input for all inputs
            if (flags.HasFlag(MachineInfoFlags.DOES_INPUT_MIXING))
            {
                Sample[] samples;

                foreach (var input in Machine.Inputs)
                {
                    var inputCore = input as MachineConnectionCore;
                    samples = inputCore.Buffer;
                    audiom.AudioInput(Machine, samples, nSamples, Utils.FlushDenormalToZero(input.Amp / (float)0x4000), true);
                }
            }

            if (flags.HasFlag(MachineInfoFlags.MULTI_IO))
            {
                if (Machine.OutputChannelCount >= MAX_MULTI_IO_CHANNELS || Machine.InputChannelCount >= MAX_MULTI_IO_CHANNELS)
                    return false;

                List<Sample[]> multiSamplesIn = Machine.GetMultiIOSamples(nSamples);
                multiSamplesOut.Clear();
                for (int i = 0; i < Machine.OutputChannelCount; i++)
                {
                    multiSamplesOut.Add(null);
                }

                foreach (var outConnection in Machine.Outputs)
                {
                    multiSamplesOut[outConnection.SourceChannel] = multiSamplesOut[outConnection.SourceChannel] == null ? new Sample[nSamples] : multiSamplesOut[outConnection.SourceChannel];
                }

                isActive = audiom.AudioMultiWork(Machine, nSamples, multiSamplesIn, multiSamplesOut, true, true);
                Machine.UpdateOutputs(multiSamplesOut);

                // For muted machines, call Work() but send empty buffer
                if (Machine.IsMuted || Machine.IsSeqMute || (buzz.SongCore.SoloMode && !Machine.IsSoloed && Machine.DLL.Info.Type == MachineType.Generator))
                {
                    Sample[] samples = new Sample[nSamples];
                    Machine.UpdateOutputs(samples);
                }

                foreach (var output in Machine.Outputs)
                {
                    var outputCore = output as MachineConnectionCore;
                    outputCore.DoTap(outputCore.Buffer, true, buzz.GetSongTime());
                }

            }
            else if ((flags.HasFlag(MachineInfoFlags.MONO_TO_STEREO) || (flags.HasFlag(MachineInfoFlags.DOES_INPUT_MIXING) && !flags.HasFlag(MachineInfoFlags.STEREO_EFFECT)))
                    && Machine.DLL.Info.Version > MachineManager.BUZZ_MACHINE_INTERFACE_VERSION_12)
            {
                // Most? old machines have mono_to_stereo if they implement DOES_INPUT_MIXING
                Sample[] samples = Machine.GetStereoSamples(nSamples);

                BuzzWorkMode wm = isReadWriteFlag ? BuzzWorkMode.WM_READWRITE : BuzzWorkMode.WM_WRITE;
                isActive = audiom.AudioWorkMonoToStereo(Machine, samples, nSamples, wm, true);

                // For muted machines, call Work() but send empty buffer
                if (Machine.IsMuted || Machine.IsSeqMute || (buzz.SongCore.SoloMode && !Machine.IsSoloed && Machine.DLL.Info.Type == MachineType.Generator))
                {
                    samples = new Sample[nSamples];

                }
                Machine.UpdateOutputs(samples);
                foreach (var output in Machine.Outputs)
                {
                    (output as MachineConnectionCore).DoTap(samples, true, buzz.GetSongTime());
                }
            }
            else if (flags.HasFlag(MachineInfoFlags.STEREO_EFFECT) /* || Machine.DLL.Info.Version >= 42 */)
            {
                Sample[] samples = Machine.GetStereoSamples(nSamples);

                BuzzWorkMode wm = isReadWriteFlag ? BuzzWorkMode.WM_READWRITE : BuzzWorkMode.WM_WRITE;

                isActive = audiom.AudioWork(Machine, 2, samples, nSamples, wm);

                // For muted machines, call Work() but send empty buffer
                if (Machine.IsMuted || Machine.IsSeqMute || (buzz.SongCore.SoloMode && !Machine.IsSoloed && Machine.DLL.Info.Type == MachineType.Generator))
                {
                    samples = new Sample[nSamples];
                }

                Machine.UpdateOutputs(samples);
                foreach (var output in Machine.Outputs)
                {
                    (output as MachineConnectionCore).DoTap(samples, true, buzz.GetSongTime());
                }
            }
            else
            {
                Sample[] samples = Machine.GetStereoSamples(nSamples);

                BuzzWorkMode wm = isReadWriteFlag ? BuzzWorkMode.WM_READWRITE : BuzzWorkMode.WM_WRITE;
                // Mono
                isActive = audiom.AudioWork(Machine, 1, samples, nSamples, wm);

                // For muted machines, call Work() but send empty buffer
                if (Machine.IsMuted || Machine.IsSeqMute || (buzz.SongCore.SoloMode && !Machine.IsSoloed && Machine.DLL.Info.Type == MachineType.Generator))
                {
                    samples = new Sample[nSamples];
                }

                Machine.UpdateOutputs(samples);
                foreach (var output in Machine.Outputs)
                {
                    (output as MachineConnectionCore).DoTap(samples, true, buzz.GetSongTime());
                }
            }

            return isActive;
        }

        private bool WorkMachineManaged(int nSamples)
        {
            bool isActive = false;
            // Managed machines
            var machineHost = manageMachineHost;
            var flags = Machine.DLL.Info.Flags;
            var type = Machine.DLL.Info.Type;
            if (flags.HasFlag(MachineInfoFlags.MULTI_IO))
            {
                if (Machine.OutputChannelCount >= MAX_MULTI_IO_CHANNELS || Machine.InputChannelCount >= MAX_MULTI_IO_CHANNELS)
                    return false;

                List<Sample[]> multiSamplesIn = Machine.GetMultiIOSamples(nSamples);
                multiSamplesOut.Clear();
                for (int i = 0; i < Machine.OutputChannelCount; i++)
                {
                    multiSamplesOut.Add(null);
                }
                foreach (var outConnection in Machine.Outputs)
                {
                    multiSamplesOut[outConnection.SourceChannel] = (outConnection as MachineConnectionCore).Buffer;
                }

                machineHost.MultiWork(multiSamplesOut, multiSamplesIn, nSamples, WorkModes.WM_WRITE);
                Machine.UpdateOutputs(multiSamplesOut);

                // For muted machines, call Work() but send empty buffer
                if (Machine.IsMuted || Machine.IsSeqMute || (buzz.SongCore.SoloMode && !Machine.IsSoloed && Machine.DLL.Info.Type == MachineType.Generator))
                {
                    Sample[] samples = new Sample[nSamples];
                    Machine.UpdateOutputs(samples);

                    foreach (var output in Machine.Outputs)
                    {
                        (output as MachineConnectionCore).DoTap(samples, true, buzz.GetSongTime());
                    }
                }
                else
                {
                    foreach (var output in Machine.Outputs)
                    {
                        var outputConn = output as MachineConnectionCore;
                        if (multiSamplesOut[outputConn.SourceChannel] != null)
                        {
                            outputConn.DoTap(multiSamplesOut[outputConn.SourceChannel], true, buzz.GetSongTime());
                        }
                        else
                        {
                            outputConn.DoTap(new Sample[nSamples], true, buzz.GetSongTime());
                        }
                    }
                }
            }
            else
            {
                Sample[] samples = Machine.GetStereoSamples(nSamples);

                if (type == MachineType.Generator)
                {
                    machineHost.Work(samples, nSamples, WorkModes.WM_WRITE);
                }
                else if (type == MachineType.Effect)
                {
                    machineHost.Work(samples, nSamples, WorkModes.WM_READWRITE);
                }

                // For muted machines, call Work() but send empty buffer
                if (Machine.IsMuted || Machine.IsSeqMute || (buzz.SongCore.SoloMode && !Machine.IsSoloed && Machine.DLL.Info.Type == MachineType.Generator))
                {
                    samples = new Sample[nSamples];
                    Machine.UpdateOutputs(samples);

                    foreach (var output in Machine.Outputs)
                    {
                        (output as MachineConnectionCore).DoTap(samples, true, buzz.GetSongTime());
                    }
                }
                else
                {
                    Machine.UpdateOutputs(samples);

                    foreach (var output in Machine.Outputs)
                    {
                        (output as MachineConnectionCore).DoTap(samples, true, buzz.GetSongTime());
                    }
                }
            }

            return isActive;
        }

        // Old machines expect this when pos in tick == 0
        internal void Tick(bool resetToNoValue = false, bool forceTick = false)
        {
            if (Machine.Ready &&
                (forceTick || ReBuzzCore.masterInfo.PosInTick == 0 ||
                    (Global.EngineSettings.SubTickTiming && ReBuzzCore.subTickInfo.PosInSubTick == 0 && Machine.DLL.Info.Version >= MachineManager.BUZZ_MACHINE_INTERFACE_VERSION_42)))
            {
                if (Machine.Ready && manageMachineHost != null)
                {
                    // Do we need to "tick" for managed machines if SetValue is called immediately?
                    manageMachineHost.Tick(Machine);
                }
                else if (Machine.Ready && nativeMachineHost != null)
                {
                    var audiom = nativeMachineHost.AudioMessage;
                    audiom.AudioBeginFrame(Machine);               // Send song info: playposition etc.
                    audiom.AudioTick(Machine);
                }

                foreach (var paramTrack in Machine.parametersChanged)
                {
                    var par = paramTrack.Key;
                    var track = paramTrack.Value;

                    // Need to wait until ReBuzzCore.masterInfo.PosInTick == 0 ||
                    // (Global.EngineSettings.SubTickTiming && ReBuzzCore.subTickInfo.PosInSubTick == 0 && Machine.DLL.Info.Version >= MachineManager.BUZZ_MACHINE_INTERFACE_VERSION_42))?
                    /*
                    if (resetToNoValue)
                    {
                        if (!par.Flags.HasFlag(ParameterFlags.State))
                        {
                            par.SetValue(track | 1 << 16, par.NoValue);
                        }
                    }
                    */
                    par.InvokeEvents(track);
                }

                Machine.parametersChanged.Clear();
                TickSent = true;
            }
        }

        internal void TickAndWork(int nSamples, bool resetToNoValue = false)
        {
            if (Machine.Ready)
            {
                if (Machine.invalidateWaves)
                {
                    if (nativeMachineHost != null)
                    {
                        var audiom = nativeMachineHost.AudioMessage;
                        audiom.AudioBeginBlock(Machine, buzz.SongCore.WavetableCore);
                    }
                    Machine.invalidateWaves = false;
                }

                // Tick again if sendControlChangesFlag set
                if (Machine.sendControlChangesFlag)
                {
                    Tick(true, true);
                    Machine.sendControlChangesFlag = false;
                }

                Machine.IsActive = WorkMachine(nSamples);

                // Some machines report the active state wrong, double check
                if (Machine.IsActive)
                {
                    Machine.IsActive = Machine.GetActivity();
                }
            }
        }
    }
}
