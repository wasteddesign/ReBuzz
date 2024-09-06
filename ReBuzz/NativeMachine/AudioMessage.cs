using Buzz.MachineInterface;
using BuzzGUI.Interfaces;
using ReBuzz.Core;
using ReBuzz.MachineManagement;
using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;

namespace ReBuzz.NativeMachine
{
    // All audio message calls should return immediately. Otherwise they are considered crashed.
    internal class AudioMessage : NativeMessage
    {
        private readonly object AudioMessageLock = new object();

        public AudioMessage(ChannelType channel, MemoryMappedViewAccessor accessor, NativeMachineHost nativeMachineHost) : base(channel, accessor, nativeMachineHost)
        {
        }

        public override event EventHandler<EventArgs> MessageEvent;

        public override void ReceaveMessage()
        {
            DoReveiveIncomingMessage();
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

            lock (AudioMessageLock)
            {
                Reset();
                SetMessageData((int)AudioMessages.AudioSetNumTracks);
                SetMessageDataPtr(machine.CMachinePtr);
                SetMessageData(numTracks);
                DoSendMessage(machine);
            }
        }

        internal void AudioTick(MachineCore machine)
        {
            if (machine.DLL.IsCrashed)
            {
                return;
            }

            lock (AudioMessageLock)
            {
                List<byte> globalVals = new List<byte>();
                List<byte> trackVals = new List<byte>();

                foreach (var p in machine.ParameterGroupsList[1].ParametersList)
                {
                    int typeSize = p.GetTypeSize();
                    int pValue = p.GetValue(0);
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
                        int pValue = p.GetValue(i);
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
                WriteMasterInfo();
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

        internal bool AudioWork(MachineCore machine, int numChannels, Sample[] samples, int numSamples, BuzzWorkMode mode)
        {
            if (machine.DLL.IsCrashed)
            {
                return false;
            }

            lock (AudioMessageLock)
            {
                Reset();
                SetMessageData((int)AudioMessages.AudioWork);
                WriteMasterInfo();
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

        internal bool AudioWorkMonoToStereo(MachineCore machine, Sample[] samples, int nSamples, BuzzWorkMode mode, bool gotPin)
        {
            if (machine.DLL.IsCrashed)
            {
                return false;
            }

            lock (AudioMessageLock)
            {
                Reset();
                SetMessageData((int)AudioMessages.AudioWorkMonoToStereo);
                WriteMasterInfo();
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

        internal void AudioInput(MachineCore machine, Sample[] samples, int nSamples, float amp, bool hasInput)
        {
            if (machine.DLL.IsCrashed)
            {
                return;
            }

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

        internal void AudioBeginBlock(MachineCore machine, IWavetable wt, float amp, bool hasInput)
        {
            if (machine.DLL.IsCrashed)
            {
                return;
            }

            lock (AudioMessageLock)
            {
                Reset();
                SetMessageData((int)AudioMessages.AudioBeginBlock);

                for (int wi = 0; wi < 200; wi++)
                {
                    SetMessageData(false); // Nothing is allocated
                }

                DoSendMessage(machine);

                // Big ToDo!
                // Samples are stored in MapFile, need to implement that on dotnet side

                /*
                for (int wi = 0; wi < WAVE_MAX; wi++)
                {
                    Wave &w = wavetable[wi];

                    r.Read(w.Allocated);

                    if (w.Allocated != NULL)
                    {
                        w.Name = r.ReadString();
                        r.Read(w.Info);
                        int levelcount = (int)r.ReadDWORD();
                        if (levelcount != w.Levels.size())
                        {
                            w.Levels.resize(levelcount);
                        }

                        for (int i = 0; i < levelcount; i++)
                        {
                            Level &l = w.Levels[i];
                            short *poldsamples = l.pSamples;

                            HANDLE hShared;
                            r.Read(hShared);
                            r.Read(&w.Levels[i], sizeof(CWaveLevel));

                            if (hShared != l.hShared)
                            {
                                if (l.hShared != NULL) ::UnmapViewOfFile(poldsamples);
                                l.hShared = hShared;
                                if (hShared != NULL) l.pSamples = (short *)::MapViewOfFile(hShared, FILE_MAP_WRITE | FILE_MAP_READ, 0, 0, 0);
                            }
                            else
                            {
                                l.pSamples = poldsamples;
                            }
                        }

                        int envcount = (int)r.ReadDWORD();
                        if (envcount != w.Envelopes.size())
                            w.Envelopes.resize(envcount);

                        for (int i = 0; i < envcount; i++)
                        {
                            Envelope &e = w.Envelopes[i];
                            int pointcount = (int)r.ReadDWORD();
                            if (pointcount != e.Points.size()) e.Points.resize(pointcount);
                            if (pointcount > 0) r.Read(&e.Points[0], pointcount * sizeof(int));
                            r.Read(e.Enabled);
                        }
                    }
                    else
                    {
                        w.Levels.resize(0);
                    } 
                 */
            }
        }
        internal void AudioBeginFrame(MachineCore machine)
        {
            lock (AudioMessageLock)
            {
                Reset();
                SetMessageData((int)AudioMessages.AudioBeginFrame);
                WriteGlobalState();
                DoSendMessage(machine);
            }
        }

        readonly Sample[] dumpSamples = new Sample[256];
        internal bool AudioMultiWork(MachineCore machine, int numSamples, List<Sample[]> samplesIn, List<Sample[]> samplesOut, bool gotInputs, bool gotOutputs)
        {
            if (machine.DLL.IsCrashed)
            {
                return false;
            }

            lock (AudioMessageLock)
            {
                Reset();
                SetMessageData((int)AudioMessages.AudioMultiWork);
                WriteMasterInfo();
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
    }
}
