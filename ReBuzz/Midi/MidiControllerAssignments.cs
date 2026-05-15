using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using ReBuzz.Core;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

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

            foreach (var cb in ContollerBindings)
            {
                cb.Update(channel, data1, data2);
            }
        }

        internal void ClearAll()
        {
            RegClearAllMidiControllers(registryEx, predefinedMIDIControllers.Count, registryRoot);

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
            var controller = RegGetController(registryEx, "MidiControllerPlay");
            if (controller != null)
            {
                reBuzzMIDIControllers.Add(controller);
            }
            controller = RegGetController(registryEx, "MidiControllerStop");
            if (controller != null)
            {
                reBuzzMIDIControllers.Add(controller);
            }
            controller = RegGetController(registryEx, "MidiControllerRecord");
            if (controller != null)
            {
                reBuzzMIDIControllers.Add(controller);
            }
            controller = RegGetController(registryEx, "MidiControllerForward");
            if (controller != null)
            {
                reBuzzMIDIControllers.Add(controller);
            }
            controller = RegGetController(registryEx, "MidiControllerBackward");
            if (controller != null)
            {
                reBuzzMIDIControllers.Add(controller);
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
        public static MidiController RegGetController(IRegistryEx registryEx, string regKey)
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

                mc.Name = values[0].Trim();
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

        public static void RegSetController(
          IRegistryEx registryEx, string regKey, string name, int channel, int controller)
        {
            var values = name + "," + channel + "," + controller;
            registryEx.Write(regKey, values, "Settings");
        }

        public static void RegSetControllerById(
          IRegistryEx registryEx, int id, string name, int channel, int controller)
        {
            string regKey = "MidiController" + id;
            RegSetController(registryEx, regKey, name, channel, controller);
        }

        public static int RegGetNumberOfMidiControllers(IRegistryEx registryEx)
        {
            return registryEx.Read("numMidiControllers", 0, "Settings");
        }

        public static void RegSetNumberOfMidiControllers(IRegistryEx registryEx, int num)
        {
            registryEx.Write("numMidiControllers", num, "Settings");
        }

        public static void RegClearAllMidiControllers(
          IRegistryEx registryEx, int numControllers, string registryRoot)
        {
            string regKeyBase = registryRoot + "Settings\\MidiController";
            int id = 0;

            for (int i = 0; i < numControllers; i++)
            {
                string key = regKeyBase + id;
                try
                {
                    registryEx.DeleteCurrentUserSubKey(key);
                }
                catch { }

                id++;
            }

            RegSetNumberOfMidiControllers(registryEx, 0);
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
