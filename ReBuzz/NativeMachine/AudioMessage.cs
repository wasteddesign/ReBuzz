using Buzz.MachineInterface;
using BuzzGUI.Interfaces;
using ReBuzz.Core;
using ReBuzz.MachineManagement;
using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading;
using System.Windows.Input;

namespace ReBuzz.NativeMachine
{
    // All audio message calls should return immediately. Otherwise they are considered crashed.
    internal class AudioMessage : NativeMessage
    {
        private readonly Lock AudioMessageLock = new();

        public AudioMessage(ChannelType channel, MemoryMappedViewAccessor accessor, NativeMachineHost nativeMachineHost) : base(channel, accessor, nativeMachineHost)
        {
        }

        public override event EventHandler<EventArgs> MessageEvent;

        public override void ReceaveMessage()
        {
            try
            {
                DoReceiveIncomingMessage();
            }
            catch (Exception e)
            {
                var kvPair = ChannelListener.buzz.MachineManager.NativeMachines.FirstOrDefault(kv => kv.Value == this.NativeHost);
                if (kvPair.Key != null)
                {
                    MachineCrashed(kvPair.Key, e);
                }
            }
        }

        internal override void Notify()
        {
            MessageEvent?.Invoke(this, new MessageEventArgs() { MessageId = GetMessageId() });
        }

        internal void AudioSetNumTracks(MachineCore machine, int numTracks)
        {
            if (machine.DLL.IsCrashed)
            {
                return;
            }

            try
            {
                lock (AudioMessageLock)
                {
                    Reset();
                    SetMessageData((int)AudioMessages.AudioSetNumTracks);
                    SetMessageDataPtr(machine.CMachinePtr);
                    SetMessageData(numTracks);
                    DoSendMessage(machine);
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
            }
        }

        internal void AudioTick(MachineCore machine)
        {
            if (machine.DLL.IsCrashed)
            {
                return;
            }

            try
            {
                lock (AudioMessageLock)
                {
                    List<byte> globalVals = new List<byte>();
                    List<byte> trackVals = new List<byte>();

                    foreach (var p in machine.ParameterGroupsList[1].ParametersList)
                    {
                        int typeSize = p.GetTypeSize();
                        int pValue = p.GetPValue(0);
                        if (typeSize == 1)
                        {
                            globalVals.Add((byte)pValue);
                        }
                        else if (typeSize == 2)
                        {
                            globalVals.AddRange(GetBytes((ushort)pValue));
                        }
                    }

                    // Send all data for all tracks
                    for (int i = 0; i < machine.TrackCount; i++)
                    {
                        foreach (var p in machine.ParameterGroupsList[2].ParametersList)
                        {
                            int typeSize = p.GetTypeSize();
                            int pValue = p.GetPValue(i);
                            if (typeSize == 1)
                            {
                                trackVals.Add((byte)pValue);
                            }
                            else if (typeSize == 2)
                            {
                                trackVals.AddRange(GetBytes((ushort)pValue));
                            }
                        }
                    }

                    Reset();
                    SetMessageData((int)AudioMessages.AudioTick);
                    WriteMasterInfo(machine);
                    SetMessageDataPtr(machine.CMachinePtr);
                    SetMessageData(globalVals.Count);
                    SetMessageData(globalVals.ToArray());
                    SetMessageData(trackVals.Count);
                    SetMessageData(trackVals.ToArray());

                    // Do this to avoid unnecessary IPC during recording
                    int row = 0;
                    float pos = 0; ;
                    GetPlayingRowAndTickPos(machine, 0, ref row, ref pos);
                    SetMessageData(row);
                    SetMessageData(pos);
                    //DoSendMessage();
                    DoSendMessage(machine);
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
            }
        }

        internal bool AudioWork(MachineCore machine, int numChannels, Sample[] samples, int numSamples, BuzzWorkMode mode)
        {
            if (machine.DLL.IsCrashed)
            {
                return false;
            }

            try
            {
                lock (AudioMessageLock)
                {
                    Reset();
                    SetMessageData((int)AudioMessages.AudioWork);
                    WriteMasterInfo(machine);
                    SetMessageDataPtr(machine.CMachinePtr);
                    SetMessageData(numChannels);
                    SetMessageData(numSamples * numChannels);
                    SetMessageData((int)mode);

                    if (numChannels == 1)
                    {
                        SetMessageData(samples, numSamples, false);
                    }
                    else if (numChannels == 2)
                    {
                        SetMessageData(samples, numSamples, true);
                    }
                    if (DoSendMessage(machine) == null)
                    {
                        return false;
                    }

                    bool isAudioOutputted = GetMessageByte() == 1;
                    if (isAudioOutputted)
                    {
                        if (numChannels == 1)
                        {
                            GetMessageSamples(samples, numSamples, false);
                        }
                        else if (numChannels == 2)
                        {
                            GetMessageSamples(samples, numSamples, true);
                        }
                    }
                    return isAudioOutputted;
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
                return false;
            }
        }

        internal bool AudioWorkMonoToStereo(MachineCore machine, Sample[] samples, int nSamples, BuzzWorkMode mode, bool gotPin)
        {
            if (machine.DLL.IsCrashed)
            {
                return false;
            }

            try
            {
                lock (AudioMessageLock)
                {
                    Reset();
                    SetMessageData((int)AudioMessages.AudioWorkMonoToStereo);
                    WriteMasterInfo(machine);
                    SetMessageDataPtr(machine.CMachinePtr);
                    SetMessageData(nSamples);
                    SetMessageData((int)mode);
                    SetMessageData(gotPin);
                    if (gotPin)
                    {
                        SetMessageData(samples, nSamples, false);
                    }
                    DoSendMessage(machine);

                    bool isAudioOutputted = GetMessageBool();

                    if (isAudioOutputted)
                    {
                        GetMessageSamples(samples, nSamples, true);
                    }

                    return isAudioOutputted;
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
                return false;
            }
        }

        internal void AudioInput(MachineCore machine, Sample[] samples, int nSamples, float amp, bool hasInput)
        {
            if (machine.DLL.IsCrashed)
            {
                return;
            }

            try
            {
                lock (AudioMessageLock)
                {
                    Reset();
                    SetMessageData((int)AudioMessages.AudioInput);
                    SetMessageDataPtr(machine.CMachinePtr);
                    SetMessageData(nSamples);
                    SetMessageData(hasInput);
                    if (hasInput)
                    {
                        SetMessageData(samples, nSamples, true);
                    }
                    SetMessageData(amp);
                    DoSendMessage(machine);
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
            }
        }

        private readonly int WAVE_MAX = 200;
        internal void AudioBeginBlock(MachineCore machine, WavetableCore wt)
        {
            //return;
            if (machine.DLL.IsCrashed)
            {
                return;
            }

            try
            {
                lock (AudioMessageLock)
                {
                    Reset();
                    SetMessageData((int)AudioMessages.AudioBeginBlock);

                    for (int wi = 0; wi < WAVE_MAX; wi++)
                    {
                        bool allocated = false;
                        var wave = wt.WavesList[wi];
                        if (wave != null)
                        {
                            if (wave.Layers.Count > 0)
                                allocated = true;
                        }

                        SetMessageData(allocated);

                        if (allocated)
                        {
                            SetMessageData(wave.Name);
                            // Info
                            SetMessageData((int)wave.Flags);
                            SetMessageData(wave.Volume);

                            SetMessageData(wave.Layers.Count);

                            for (int i = 0; i < wave.LayersList.Count; i++)
                            {
                                var layer = wave.LayersList[i];

                                SetMessageData(layer.mappedFileId != null ? layer.mappedFileId : "");

                                SetMessageData(layer.SampleCount16Bit);
                                SetMessageDataPtr(IntPtr.Zero);
                                SetMessageData(layer.RootNote);
                                SetMessageData(layer.SampleRate);
                                SetMessageData(layer.LoopStart16Bit);
                                SetMessageData(layer.LoopEnd16Bit);
                            }

                            // Envcount == 0
                            SetMessageData((int)0);
                            /*
                                    int envcount = (int)r.ReadDWORD();
                                    if (envcount != w.Envelopes.size())
                                        w.Envelopes.resize(envcount);

                                    for (int i = 0; i < envcount; i++)
                                    {
                                        Envelope & e = w.Envelopes[i];
                                        int pointcount = (int)r.ReadDWORD();
                                        if (pointcount != e.Points.size()) e.Points.resize(pointcount);
                                        if (pointcount > 0) r.Read(&e.Points[0], pointcount * sizeof(int));
                                        r.Read(e.Enabled);
                                    }
                            */
                        }
                    }

                    DoSendMessage(machine);
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
            }
        }

        internal void AudioBeginFrame(MachineCore machine)
        {
            try
            {
                lock (AudioMessageLock)
                {
                    Reset();
                    SetMessageData((int)AudioMessages.AudioBeginFrame);
                    WriteGlobalState();
                    DoSendMessage(machine);
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
            }
        }

        readonly Sample[] dumpSamples = new Sample[256];
        internal bool AudioMultiWork(MachineCore machine, int numSamples, List<Sample[]> samplesIn, List<Sample[]> samplesOut, bool gotInputs, bool gotOutputs)
        {
            if (machine.DLL.IsCrashed)
            {
                return false;
            }

            try
            {
                lock (AudioMessageLock)
                {
                    Reset();
                    SetMessageData((int)AudioMessages.AudioMultiWork);
                    WriteMasterInfo(machine);
                    SetMessageDataPtr(machine.CMachinePtr);
                    SetMessageData(numSamples);
                    SetMessageData(samplesIn.Count);
                    SetMessageData(samplesOut.Count);
                    SetMessageData(gotInputs);

                    if (gotInputs)
                    {
                        for (int i = 0; i < samplesIn.Count; i++)
                        {
                            bool gotPtr = samplesIn[i] != null;
                            SetMessageData(gotPtr);
                            if (gotPtr)
                            {
                                Sample[] samples = samplesIn[i];
                                // Stereo
                                SetMessageData(samples, numSamples, true);
                            }
                        }
                    }

                    SetMessageData(gotOutputs);
                    if (gotOutputs)
                    {
                        for (int i = 0; i < samplesOut.Count; i++)
                        {
                            bool gotPtr = samplesOut[i] != null;
                            SetMessageData(gotPtr);
                        }
                    }

                    DoSendMessage(machine);

                    bool isOutout = false;
                    for (int i = 0; i < samplesOut.Count; i++)
                    {
                        bool gotPtr = GetMessageBool();
                        isOutout |= gotPtr;
                        if (gotPtr)
                        {
                            Sample[] samples = samplesOut[i] == null ? dumpSamples : samplesOut[i];

                            GetMessageSamples(samples, numSamples, true);
                        }
                    }

                    return isOutout;
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
                return false;
            }
        }

        internal int AudioGetLatency(MachineCore machine)
        {
            int latency = 0;
            try
            {
                lock (AudioMessageLock)
                {
                    Reset();
                    SetMessageData((int)AudioMessages.AudioGetLatency);
                    SetMessageDataPtr(machine.CMachinePtr);
                    DoSendMessage(machine);
                    latency = GetMessageData<int>();
                }
            }
            catch (Exception e)
            {
                 MachineCrashed(machine, e);
            }

            return latency;
        }
    }
}
