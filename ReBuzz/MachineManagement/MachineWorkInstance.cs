using Buzz.MachineInterface;
using BuzzGUI.Common.Settings;
using BuzzGUI.Interfaces;
using ReBuzz.Audio;
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

        public MachineWorkInstance(MachineCore machine, ReBuzzCore reBuzz, EngineSettings settings)
        {
            this.Machine = machine;
            this.buzz = reBuzz;

            var ManagedMachines = reBuzz.MachineManager.ManagedMachines;
            var NativeMachines = reBuzz.MachineManager.NativeMachines;

            if (ManagedMachines.ContainsKey(Machine))
                manageMachineHost = ManagedMachines[Machine];
            if (NativeMachines.ContainsKey(Machine))
                nativeMachineHost = NativeMachines[Machine];
            engineSettings = settings;
        }

        internal bool WorkMachine(int nSamples)
        {
            bool isActive = false;
            DateTime dtStart = DateTime.UtcNow;
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
                    (Machine.IsMuted && !engineSettings.ProcessMutedMachines))
                {
                    Sample[] samples = Machine.GetStereoSamples(nSamples);
                    Machine.UpdateOutputs(samples, nSamples, false);

                    foreach (var output in Machine.Outputs)
                    {
                        (output as MachineConnectionCore).DoTap(samples, nSamples, true, buzz.GetSongTime());
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
            DateTime dtEnd = DateTime.UtcNow;
            long timeDelta = dtEnd.Ticks - dtStart.Ticks;
            Machine.PerformanceDataCurrent.PerformanceCount += timeDelta;
            Machine.PerformanceDataCurrent.CycleCount += timeDelta;
            Machine.PerformanceDataCurrent.MaxEngineLockTime = 0;
            Machine.PerformanceDataCurrent.SampleCount += nSamples;
            Machine.performanceLastCount = timeDelta;

            return isActive;
        }

        readonly List<Sample[]> multiSamplesOut = new List<Sample[]>();
        readonly Sample[][] multiSamplesOutBuffers = new Sample[256][];
        readonly Sample[] silentBuffer = new Sample[256];
        private readonly EngineSettings engineSettings;

        private bool WorkMachineNative(int nSamples)
        {
            bool isActive;
            bool isReadWriteFlag = Machine.DLL.Info.Type == MachineType.Effect;
            // Native machines
            var nmh = nativeMachineHost;
            var audiom = nmh.AudioMessage;

            var flags = Machine.DLL.Info.Flags;

            WorkTypes type;

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

            if (!engineSettings.ProcessMutedMachines && Machine.IsMuted)
            {
                return false;
            }

            Sample[] samples = null;

            // If machine wants to do input mixing, first call Input for all inputs
            if ((flags & MachineInfoFlags.DOES_INPUT_MIXING) == MachineInfoFlags.DOES_INPUT_MIXING)
            {

                foreach (var input in Machine.Inputs)
                {
                    var inputCore = input as MachineConnectionCore;
                    samples = inputCore.Buffer;
                    audiom.AudioInput(Machine, samples, nSamples, Utils.FlushDenormalToZero(input.Amp / (float)0x4000), true);
                }
            }

            if ((flags & MachineInfoFlags.MULTI_IO) == MachineInfoFlags.MULTI_IO)
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
                    if (multiSamplesOutBuffers[outConnection.SourceChannel] == null)
                        multiSamplesOutBuffers[outConnection.SourceChannel] = new Sample[256];
                    multiSamplesOut[outConnection.SourceChannel] = multiSamplesOutBuffers[outConnection.SourceChannel];
                    for (int i = 0; i < nSamples; i++)
                    {
                        multiSamplesOut[outConnection.SourceChannel][i].L = 0;
                        multiSamplesOut[outConnection.SourceChannel][i].R = 0;
                    }
                }

                //isActive = audiom.AudioMultiWork(Machine, nSamples, multiSamplesIn, multiSamplesOut, true, true);
                isActive = ProcessMultiWork(Machine, nSamples, multiSamplesIn, multiSamplesOut);
                
                // For muted machines, call Work() but send empty buffer
                if (Machine.IsMuted || Machine.IsSeqMute || (buzz.SongCore.SoloMode && !Machine.IsSoloed && Machine.DLL.Info.Type == MachineType.Generator))
                {
                    samples = silentBuffer;
                    Machine.UpdateOutputs(samples, nSamples);
                }
                else
                {
                    Machine.UpdateOutputs(multiSamplesOut, nSamples);
                }

                foreach (var output in Machine.Outputs)
                {
                    var outputCore = output as MachineConnectionCore;
                    outputCore.DoTap(outputCore.Buffer, nSamples, true, buzz.GetSongTime());
                }
            }
            else
            {
                if ((flags & MachineInfoFlags.MONO_TO_STEREO) == MachineInfoFlags.MONO_TO_STEREO || ((flags & MachineInfoFlags.DOES_INPUT_MIXING) == MachineInfoFlags.DOES_INPUT_MIXING && !((flags & MachineInfoFlags.STEREO_EFFECT) == MachineInfoFlags.STEREO_EFFECT)
                        && Machine.DLL.Info.Version > MachineManager.BUZZ_MACHINE_INTERFACE_VERSION_12))
                {
                    type = WorkTypes.MonoToStereo;
                    // Most? old machines have mono_to_stereo if they implement DOES_INPUT_MIXING
                }
                else if ((flags & MachineInfoFlags.STEREO_EFFECT) == MachineInfoFlags.STEREO_EFFECT /* || Machine.DLL.Info.Version >= 42 */)
                {
                    type = WorkTypes.Stereo;
                }
                else
                {
                    type = WorkTypes.Mono;
                }

                samples = Machine.GetStereoSamples(nSamples);

                BuzzWorkMode wm = isReadWriteFlag ? BuzzWorkMode.WM_READWRITE : BuzzWorkMode.WM_WRITE;
                // Mono

                isActive = ProcessWork(Machine, samples, nSamples, wm, type);

                // For muted machines, call Work() but send empty buffer
                if (Machine.IsMuted || Machine.IsSeqMute || (buzz.SongCore.SoloMode && !Machine.IsSoloed && Machine.DLL.Info.Type == MachineType.Generator))
                {
                    samples = silentBuffer;
                }
                Machine.UpdateOutputs(samples, nSamples);
                foreach (var output in Machine.Outputs)
                {
                    (output as MachineConnectionCore).DoTap(samples, nSamples, true, buzz.GetSongTime());
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
            if ((flags & MachineInfoFlags.MULTI_IO) == MachineInfoFlags.MULTI_IO)
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
                    if (multiSamplesOutBuffers[outConnection.SourceChannel] == null)
                        multiSamplesOutBuffers[outConnection.SourceChannel] = new Sample[256];
                    multiSamplesOut[outConnection.SourceChannel] = multiSamplesOutBuffers[outConnection.SourceChannel];
                    for (int i = 0; i < nSamples; i++)
                    {
                        multiSamplesOut[outConnection.SourceChannel][i].L = 0;
                        multiSamplesOut[outConnection.SourceChannel][i].R = 0;
                    }
                }

                isActive = machineHost.MultiWork(multiSamplesOut, multiSamplesIn, nSamples, WorkModes.WM_WRITE);
                Machine.UpdateOutputs(multiSamplesOut, nSamples);

                // For muted machines, call Work() but send empty buffer
                if (Machine.IsMuted || Machine.IsSeqMute || (buzz.SongCore.SoloMode && !Machine.IsSoloed && Machine.DLL.Info.Type == MachineType.Generator))
                {
                    Sample[] samples = new Sample[nSamples];
                    Machine.UpdateOutputs(samples, nSamples);

                    foreach (var output in Machine.Outputs)
                    {
                        (output as MachineConnectionCore).DoTap(samples, nSamples, true, buzz.GetSongTime());
                    }
                }
                else
                {
                    foreach (var output in Machine.Outputs)
                    {
                        var outputConn = output as MachineConnectionCore;
                        if (multiSamplesOut[outputConn.SourceChannel] != null)
                        {
                            outputConn.DoTap(multiSamplesOut[outputConn.SourceChannel], nSamples, true, buzz.GetSongTime());
                        }
                        else
                        {
                            outputConn.DoTap(new Sample[nSamples], nSamples, true, buzz.GetSongTime());
                        }
                    }
                }
            }
            else
            {
                Sample[] samples = Machine.GetStereoSamples(nSamples);

                if (type == MachineType.Generator)
                {
                    isActive = machineHost.Work(samples, nSamples, WorkModes.WM_WRITE);
                }
                else if (type == MachineType.Effect)
                {
                    isActive = machineHost.Work(samples, nSamples, WorkModes.WM_READWRITE);
                }

                // For muted machines, call Work() but send empty buffer
                if (Machine.IsMuted || Machine.IsSeqMute || (buzz.SongCore.SoloMode && !Machine.IsSoloed && Machine.DLL.Info.Type == MachineType.Generator))
                {
                    samples = new Sample[nSamples];
                    Machine.UpdateOutputs(samples, nSamples);

                    foreach (var output in Machine.Outputs)
                    {
                        (output as MachineConnectionCore).DoTap(samples, nSamples, true, buzz.GetSongTime());
                    }
                }
                else
                {
                    Machine.UpdateOutputs(samples, nSamples);

                    foreach (var output in Machine.Outputs)
                    {
                        (output as MachineConnectionCore).DoTap(samples, nSamples, true, buzz.GetSongTime());
                    }
                }
            }

            return isActive;
        }

        Sample[][] oversampleMultiInBuffers = new Sample[256][];
        List<Sample[]> oversampleMultiIn = new List<Sample[]>();

        Sample[][] oversampleMultiInPartsBuffers = new Sample[256][];
        List<Sample[]> oversampleMultiInParts = new List<Sample[]>();

        Sample[][] oversampleMultiOutPartsBuffers = new Sample[256][];
        List<Sample[]> oversampleMultiOutParts = new List<Sample[]>();
        private bool ProcessMultiWork(MachineCore machine, int nSamples, List<Sample[]> multiSamplesIn, List<Sample[]> multiSamplesOut)
        {
            bool isActive = false;

            var nmh = nativeMachineHost;
            var audiom = nmh.AudioMessage;

            int oversample = Machine.oversampleFactorOnTick - 1;

            if (oversample > 0)
            {
                oversampleMultiIn.Clear();
                oversampleMultiInParts.Clear();
                oversampleMultiOutParts.Clear();

                // Oversample every input
                for (int i = 0; i < multiSamplesIn.Count; i++)
                {
                    Sample[] buff = null;

                    if (multiSamplesIn[i] != null)
                    {
                        if (oversampleMultiInBuffers[i] == null)
                            oversampleMultiInBuffers[i] = new Sample[256 * 2];
                        buff = oversampleMultiInBuffers[i];
                        Oversample(multiSamplesIn[i], nSamples, oversample, false, oversampleMultiInBuffers[i]);
                    }

                    oversampleMultiIn.Add(buff);
                }

                var nSamplesO = nSamples << oversample;

                // Create output buffers
                for (int i = 0; i < multiSamplesOut.Count; i++)
                {
                    Sample[] buff = null;

                    if (multiSamplesOut[i] != null)
                    {
                        if (oversampleMultiOutPartsBuffers[i] == null)
                            oversampleMultiOutPartsBuffers[i] = new Sample[256 * 2];
                        buff = oversampleMultiOutPartsBuffers[i];
                    }

                    oversampleMultiOutParts.Add(buff);
                }

                // Iterate buffers and call Work 
                int offset = 0;
                for (int i = 0; i < (1 << oversample); i++)
                {
                    var samplesPart = GetParts(oversampleMultiIn, nSamples, offset);
                    isActive |= audiom.AudioMultiWork(Machine, nSamples, samplesPart, multiSamplesOut, true, true);
                    AppendSamples(oversampleMultiOutParts, nSamples, offset, multiSamplesOut);
                    offset += nSamples;
                }

                // Oversample every output back
                for (int i = 0; i < multiSamplesOut.Count; i++)
                {
                    if (multiSamplesOut[i] != null)
                    {
                        Oversample(oversampleMultiOutParts[i], nSamplesO, oversample, true, multiSamplesOut[i]);
                    }
                }
            }
            else
            {
                isActive = audiom.AudioMultiWork(Machine, nSamples, multiSamplesIn, multiSamplesOut, true, true);
            }

            return isActive;
        }

        private bool ProcessWork(MachineCore machine, Sample[] samples, int nSamples, BuzzWorkMode wm, WorkTypes type)
        {
            bool isActive = false;

            var nmh = nativeMachineHost;
            var audiom = nmh.AudioMessage;

            int oversample = Machine.oversampleFactorOnTick - 1;

            if (oversample > 0)
            {
                Oversample(samples, nSamples, oversample, false, oversampleTmp);
                var nSamplesO = nSamples << oversample;

                int offset = 0;
                for (int i = 0; i < (1 << oversample); i++)
                {
                    var samplesPart = GetPart(oversampleTmp, nSamples, offset);
                    switch (type)
                    {
                        case WorkTypes.MonoToStereo:
                            {
                                isActive |= audiom.AudioWorkMonoToStereo(Machine, samplesPart, nSamples, wm, true);
                            }
                            break;
                        case WorkTypes.Mono:
                            {
                                isActive |=  audiom.AudioWork(Machine, 1, samplesPart, nSamples, wm);
                            }
                            break;
                        case WorkTypes.Stereo:
                            {
                                isActive |= audiom.AudioWork(Machine, 2, samplesPart, nSamples, wm);
                            }
                            break;
                    }
                    
                    AppendSamples(samplesOB, nSamples, offset, samplesPart);
                    offset += nSamples;
                }

                Oversample(samplesOB, nSamplesO, oversample, true, samples);
            }
            else
            {
                switch (type)
                {
                    case WorkTypes.MonoToStereo:
                        {
                            isActive |= audiom.AudioWorkMonoToStereo(Machine, samples, nSamples, wm, true);
                        }
                        break;
                    case WorkTypes.Mono:
                        {
                            isActive |= audiom.AudioWork(Machine, 1, samples, nSamples, wm);
                        }
                        break;
                    case WorkTypes.Stereo:
                        {
                            isActive |= audiom.AudioWork(Machine, 2, samples, nSamples, wm);
                        }
                        break;
                }
            }

            return isActive;
        }

        enum WorkTypes
        {
            MultiIO,
            MonoToStereo,
            Stereo,
            Mono
        }

        float[] oversampleFromSamples = new float[256 * 2 * 2];
        float[] oversampleToSamples = new float[256 * 2 * 2];
        Sample[] oversampleTmp = new Sample[256 * 2];
        Sample[] samplesOB = new Sample[256 * 2];
        Sample[] oversampleArray = new Sample[256];
        private void Oversample(Sample[] samples, int nSamples, int factor, bool back, Sample[] samplesOut)
        {
            int fromCount = nSamples << 1;
            
            int toCount = back ? fromCount >> factor : fromCount << factor;
            int j = 0;
            for (int i = 0; i < nSamples; i++)
            {
                oversampleFromSamples[j++] = samples[i].L;
                oversampleFromSamples[j++] = samples[i].R;
            }

            WorkManager.SpeedDown(oversampleToSamples, 0, toCount, oversampleFromSamples, fromCount, false);

            j = 0;
            for (int i = 0; i < toCount >> 1; i++)
            {
                samplesOut[i].L = oversampleToSamples[j++];
                samplesOut[i].R = oversampleToSamples[j++];
            }
        }
        
        private Sample[] GetPart(Sample[] array, int count, int offset)
        {
            Array.Copy(array, offset, oversampleArray, 0, count);
            return oversampleArray;
        }

        private List<Sample[]> GetParts(List<Sample[]> sampleArray, int count, int offset)
        {
            oversampleMultiInParts.Clear();
            for (int i = 0; i < sampleArray.Count; i++)
            {
                var from = sampleArray[i];
                if (from != null)
                {
                    if (oversampleMultiInPartsBuffers[i] == null)
                        oversampleMultiInPartsBuffers[i] = new Sample[256];

                    var to = oversampleMultiInPartsBuffers[i];
                    Array.Copy(from, offset, to, 0, count);
                    oversampleMultiInParts.Add(to);
                }
                else
                {
                    oversampleMultiInParts.Add(null);
                }
            }
            return oversampleMultiInParts;
        }

        private void AppendSamples(Sample[] to, int nSamples, int offset, Sample[] from)
        {
            for (int i = 0; i < nSamples; i++)
            {
                to[i+ offset] = from[i];
            }
        }

        private void AppendSamples(List<Sample[]> toList, int nSamples, int offset, List<Sample[]> fromList)
        {
            for (int j = 0; j < fromList.Count; j++)
            {
                if (fromList[j] != null)
                {
                    var to = toList[j];
                    var from = fromList[j];

                    for (int i = 0; i < nSamples; i++)
                    {
                        to[i + offset] = from[i];
                    }
                }
            }
        }

        // Old machines expect this when pos in tick == 0
        internal void Tick(bool resetToNoValue = false, bool forceTick = false)
        {
            if (Machine.Ready &&
                (forceTick || ReBuzzCore.masterInfo.PosInTick == 0 ||
                    (engineSettings.SubTickTiming && ReBuzzCore.subTickInfo.PosInSubTick == 0 && Machine.DLL.Info.Version >= MachineManager.BUZZ_MACHINE_INTERFACE_VERSION_42)))
            {
                Machine.oversampleFactorOnTick = Machine.OversampleFactor;
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

                    par.InvokeEvents(buzz, track);
                }

                Machine.parametersChanged.Clear();
                TickSent = true;

                int noRecord = 1 << 16;

                // Set pvalues to NoValue immediately to avoid sending param value twice
                foreach (var p in Machine.ParameterGroups[0].Parameters)
                {
                    p.SetValue(noRecord, p.NoValue);
                }
                foreach (var p in Machine.ParameterGroups[1].Parameters)
                {
                    p.SetValue(noRecord, p.NoValue);
                }
                foreach (var p in Machine.ParameterGroups[2].Parameters)
                {
                    for (int i = 0; i < Machine.TrackCount; i++)
                    {
                        p.SetValue(i | noRecord, p.NoValue);
                    }
                }
            }
        }

        internal void TickAndWork(int nSamples, bool resetToNoValue = false)
        {
            if (Machine.Ready)
            {
                if (Machine.updateWaveInfo)
                {
                    if (nativeMachineHost != null)
                    {
                        var audiom = nativeMachineHost.AudioMessage;
                        audiom.AudioBeginBlock(Machine, buzz.SongCore.WavetableCore);
                    }
                    Machine.updateWaveInfo = false;

                    // Wave structure updated. Now we can send wave events if machine has requested the event
                    while (Machine.wavesEventsPending.TryTake(out int index))
                    {
                        // Need to find a machine that registers to this event
                        // buzz.MachineManager.SendWaveChangedEvents(Machine, index);
                    }
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
