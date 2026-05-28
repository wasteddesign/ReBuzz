using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using ReBuzz.Core;
using Sanford.Multimedia.Midi;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Linq;

namespace ReBuzz.Midi
{   
    internal class MidiControllerAssignments : INotifyPropertyChanged
    {
        private readonly IBuzz buzz;
        private readonly List<MidiController> predefinedMIDIControllers = new List<MidiController>();
        private readonly List<MidiController> reBuzzMIDIControllers = new List<MidiController>();
        private readonly List<ContollerBinding> contollerBindings = new List<ContollerBinding>();
        private readonly string registryRoot;

        public IList<MidiController> ReBuzzMIDIControllers { get { return reBuzzMIDIControllers; } }
        public IList<MidiController> PredefinedMIDIControllers { get { return predefinedMIDIControllers; } }
        internal List<ContollerBinding> ContollerBindings => contollerBindings;

        internal MidiControllerAssignments(IBuzz reBuzzCore, IRegistryEx registryEx, string registryRoot)
        {
            this.registryEx = registryEx;
            buzz = reBuzzCore;
            LoadAssignments();
            buzz.MIDIInput += SendMidi;
            this.registryRoot = registryRoot;
        }

        SongCore song;
        public SongCore Song
        {
            get => song;
            set
            {
                if (song != null)
                {
                    song.MachineRemoved -= SongCore_MachineRemoved;
                }
                song = value;
                if (song != null)
                {
                    song.MachineRemoved += SongCore_MachineRemoved;
                }
            }
        }

        private void SongCore_MachineRemoved(IMachine obj)
        {
            UnbindAllMIDIControllers(obj);
        }

        internal void SendMidi(int data)
        {
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

            if (commandCode != MIDI.ControlChange)
                return;

            foreach (var cb in ContollerBindings)
            {
                cb.Update(channel, data1, data2);
            }

            bool activate = data2 == 127;
            if (activate)
            {
                foreach (var cb in ReBuzzMIDIControllers)
                {
                    if (cb.Contoller == data1 && cb.Channel == channel)
                    {
                        switch (cb.ControllerType)
                        {
                            case ReBuzzMIDIControllerType.Play:
                                buzz.Playing = !buzz.Playing;
                                break;
                            case ReBuzzMIDIControllerType.Stop:
                                buzz.Playing = false;
                                break;
                            case ReBuzzMIDIControllerType.Record:
                                buzz.Recording = !buzz.Recording;
                                break;
                            case ReBuzzMIDIControllerType.Forward:
                                buzz.Song.PlayPosition += 4;
                                break;
                            case ReBuzzMIDIControllerType.Backward:
                                buzz.Song.PlayPosition -= 4;
                                break;
                            case ReBuzzMIDIControllerType.Loop:
                                buzz.Looping = !buzz.Looping;
                                break;
                            case ReBuzzMIDIControllerType.Beginning:
                                buzz.Song.PlayPosition = buzz.Song.LoopStart;
                                break;
                            case ReBuzzMIDIControllerType.SpeedUp:
                                buzz.Speed += 1;
                                break;
                            case ReBuzzMIDIControllerType.SpeedDown:
                                buzz.Speed -= 1;
                                break;
                        }
                    }
                }
            }
        }

        internal void ClearAll()
        {
            RegClearAllMidiControllers(registryEx, RegGetNumberOfMidiControllers(registryEx), registryRoot);
            RegClearAllMidiDAWControllers(registryEx, RegGetNumberOfMidiDAWControllers(registryEx), registryRoot);

            reBuzzMIDIControllers.Clear();
            predefinedMIDIControllers.Clear();
            ContollerBindings.Clear();
        }

        internal void ClearControllerBindings()
        {
            foreach (var cb in ContollerBindings.ToList())
            {
                BindParameter(cb.Parameter, cb.Track, -1, -1);
            }
        }

        internal void LoadAssignments()
        {
            int numDAWControllers = RegGetNumberOfMidiDAWControllers(registryEx);
            for (int i = 0; i < numDAWControllers; i++)
            {
                MidiController mc = RegGetDAWControllerById(registryEx, i);
                if (mc != null)
                {
                    reBuzzMIDIControllers.Add(mc);
                }
            }

            int numControllers = RegGetNumberOfMidiControllers(registryEx);
            for (int i = 0; i < numControllers; i++)
            {
                MidiController mc = RegGetControllerById(registryEx, i);
                if (mc != null)
                {
                    predefinedMIDIControllers.Add(mc);
                }
            }
        }

        // Return true if controller assignment found
        public static MidiController RegGetController(IRegistryEx registryEx, string regKey, bool dawController = false)
        {
            MidiController mc = new MidiController();

            string val = registryEx.Read(regKey, "", "Settings");

            if (val == "")
            {
                return null;
            }
            else
            {
                var values = val.Split(',');

                if (values.Length < 3)
                {
                    return null;
                }

                if (dawController)
                {
                    mc.ControllerType = (ReBuzzMIDIControllerType)int.Parse(values[0]);
                }
                else
                {
                    mc.Name = values[0].Trim();
                }
                
                mc.Channel = int.Parse(values[1]);
                mc.Contoller = int.Parse(values[2]);

                return mc;
            }
        }

        public static MidiController RegGetControllerById(IRegistryEx registryEx, int id)
        {
            string regKey = "MidiController" + id;
            return RegGetController(registryEx, regKey);
        }

        public static MidiController RegGetDAWControllerById(IRegistryEx registryEx, int id)
        {
            string regKey = "MidiDAWController" + id;
            return RegGetController(registryEx, regKey, true);
        }

        public static void RegSetController(
          IRegistryEx registryEx, string regKey, string name, int channel, int controller)
        {
            var values = name + "," + channel + "," + controller;
            registryEx.Write(regKey, values, "Settings");
        }

        public static void RegSetController(
  IRegistryEx registryEx, string regKey, ReBuzzMIDIControllerType t, int channel, int controller)
        {
            var values = (int)t + "," + channel + "," + controller;
            registryEx.Write(regKey, values, "Settings");
        }

        public static void RegSetControllerById(
          IRegistryEx registryEx, int id, string name, int channel, int controller)
        {
            string regKey = "MidiController" + id;
            RegSetController(registryEx, regKey, name, channel, controller);
        }

        public static void RegSetDAWControllerById(
          IRegistryEx registryEx, int id, ReBuzzMIDIControllerType controllerType, int channel, int controller)
        {
            string regKey = "MidiDAWController" + id;
            RegSetController(registryEx, regKey, controllerType, channel, controller);
        }

        public static int RegGetNumberOfMidiControllers(IRegistryEx registryEx)
        {
            return registryEx.Read("numMidiControllers", 0, "Settings");
        }

        public static int RegGetNumberOfMidiDAWControllers(IRegistryEx registryEx)
        {
            return registryEx.Read("numMidiDAWControllers", 0, "Settings");
        }

        public static void RegSetNumberOfMidiControllers(IRegistryEx registryEx, int num)
        {
            registryEx.Write("numMidiControllers", num, "Settings");
        }

        public static void RegSetNumberOfMidiDAWControllers(IRegistryEx registryEx, int num)
        {
            registryEx.Write("numMidiDAWControllers", num, "Settings");
        }

        public static void RegClearAllMidiControllers(
          IRegistryEx registryEx, int numControllers, string registryRoot)
        {
            string regKeyBase = registryRoot + "Settings";
            int id = 0;

            for (int i = 0; i < numControllers; i++)
            {
                string key = regKeyBase + id;
                try
                {
                    registryEx.DeleteCurrentUserValue("MidiController" + i, regKeyBase);
                }
                catch { }

                id++;
            }

            RegSetNumberOfMidiControllers(registryEx, 0);
        }

        public static void RegClearAllMidiDAWControllers(
  IRegistryEx registryEx, int numControllers, string registryRoot)
        {
            string regKeyBase = registryRoot + "Settings";
            int id = 0;

            for (int i = 0; i < numControllers; i++)
            {
                string key = regKeyBase + id;
                try
                {
                    registryEx.DeleteCurrentUserValue("MidiDAWController" + i, regKeyBase);
                }
                catch { }

                id++;
            }

            RegSetNumberOfMidiDAWControllers(registryEx, 0);
        }

        internal IList<string> GetMidiControllerNames()
        {
            List<string> names = new List<string>();
            predefinedMIDIControllers.ForEach(m => names.Add(m.Name));
            return names;
        }

        internal void Add(string name, int channel, int controller, int value)
        {
            int index = predefinedMIDIControllers.Count;
            MidiController midiController = new MidiController();
            midiController.Name = name;
            midiController.Contoller = controller;
            midiController.Channel = channel;
            midiController.Value = value;

            predefinedMIDIControllers.Add(midiController);
            RegSetControllerById(registryEx, index, name, channel, controller);
            RegSetNumberOfMidiControllers(registryEx, predefinedMIDIControllers.Count);
        }

        internal void AddDAWController(ReBuzzMIDIControllerType controllerType, int channel, int controller, int value)
        {
            int index = reBuzzMIDIControllers.Count;
            MidiController midiController = new MidiController();
            midiController.ControllerType = controllerType;
            midiController.Contoller = controller;
            midiController.Channel = channel;
            midiController.Value = value;

            this.reBuzzMIDIControllers.Add(midiController);
            RegSetDAWControllerById(registryEx, index, controllerType, channel, controller);
            RegSetNumberOfMidiDAWControllers(registryEx, reBuzzMIDIControllers.Count);
        }

        private readonly IRegistryEx registryEx;

        public event PropertyChangedEventHandler PropertyChanged;

        internal void BindParameter(ParameterCore parameterCore, int track, int mcindex)
        {
            if (mcindex < 0 || mcindex >= predefinedMIDIControllers.Count)
            {
                ContollerBindings.RemoveAll(cb => cb.Parameter == parameterCore && cb.Track == track);
                parameterCore.RemoveMIDIBinding(track);
                return;
            }

            var c = predefinedMIDIControllers[mcindex];
            BindParameter(parameterCore, track, c.Channel, c.Contoller);
        }

        internal void BindParameter(ParameterCore parameterCore, int track, int midiChannel, int midiController)
        {
            if (midiChannel < 0 || midiController < 0)
            {
                ContollerBindings.RemoveAll(cb => cb.Parameter == parameterCore && cb.Track == track);
                parameterCore.RemoveMIDIBinding(track);
                return;
            }

            if (track < parameterCore.Group.TrackCount)
            {
                ContollerBinding contollerBind = new ContollerBinding(parameterCore, track, midiChannel, midiController, Global.MIDISettings.ParameterSoftTakeover);
                ContollerBindings.Add(contollerBind);
                parameterCore.SetMIDIBindingValues(track, midiChannel, midiController);
            }
        }

        internal void UnbindAllMIDIControllers(IMachine machineCore)
        {
            foreach (var cb in ContollerBindings.Where(b => b.Parameter.Group.Machine == machineCore))
            {
                cb.Parameter.RemoveMIDIBinding(cb.Track);
            }
            ContollerBindings.RemoveAll(cb => cb.Parameter.Group.Machine == machineCore);
        }

        internal class ContollerBinding
        {
            internal ParameterCore Parameter { get; }
            internal int Track { get; }
            internal int MidiChannel { get; }
            internal int MidiController { get; }

            private bool softTakeover;
            private bool isActive;
            private bool initialized;
            private bool below;

            internal ContollerBinding(ParameterCore parameter, int track, int midiChannel, int midiCC, bool softTakeover = true)
            {
                Parameter = parameter;
                Track = track;
                MidiChannel = midiChannel;
                MidiController = midiCC;

                this.softTakeover = softTakeover;
            }

            internal void Update(int channel, int controller, int value)
            {
                if (MidiChannel != channel || MidiController != controller)
                {
                    return;
                }

                float pos = value / 127f;
                int paramValue = (int)((Parameter.MaxValue - Parameter.MinValue) * pos + Parameter.MinValue);
                int paramCurrentValue = Parameter.GetValue(Track);

                if (softTakeover)
                {
                    if (!isActive)
                    {
                        if (!initialized)
                        {
                            initialized = true;
                            below = paramValue < paramCurrentValue ? true : false;

                            if (paramValue == paramCurrentValue)
                                isActive = true;

                            return;
                        }

                        if (paramValue >= paramCurrentValue && below)
                        {
                            isActive = true;
                        }
                        else if (paramValue <= paramCurrentValue && !below)
                        {
                            isActive = true;
                        }
                        else
                        {
                            return;
                        }
                    }
                }

                Parameter.SetValue(Track, paramValue);
            }
        }
    }
}
