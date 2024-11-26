using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using Microsoft.Win32;
using ReBuzz.Core;
using System.Collections.Generic;
using System.Linq;

namespace ReBuzz.Midi
{
    internal class MidiControllerAssignments
    {
        private readonly IBuzz buzz;
        private readonly List<MidiController> midiControllers = new List<MidiController>();
        private readonly List<MidiController> midiReBuzzControllers = new List<MidiController>();

        public IList<MidiController> ReBuzzMIDIControllers { get { return midiReBuzzControllers; } }
        public IList<MidiController> MIDIControllers { get { return midiControllers; } }
        internal MidiControllerAssignments(IBuzz reBuzzCore)
        {
            buzz = reBuzzCore;
            LoadAssignments();
            buzz.MIDIInput += SendMidi;
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

            int index = 0;
            foreach (var mc in midiControllers)
            {
                if (mc.Channel == channel && mc.Contoller == data1 && commandCode == MIDI.ControlChange)
                {
                    foreach (var cb in contollerBindings)
                    {
                        if (cb.McIndex == index)
                        {
                            float pos = data2 / 127f;
                            int value = (int)(cb.Parameter.MaxValue * pos);
                            cb.Parameter.SetValue(cb.Track, value);
                        }
                    }
                }
                index++;
            }
        }

        internal void ClearAll()
        {
            RegClearAllMidiControllers(midiControllers.Count);

            midiReBuzzControllers.Clear();
            midiControllers.Clear();
            contollerBindings.Clear();
        }

        internal void LoadAssignments()
        {
            var controller = RegGetController("MidiControllerPlay");
            if (controller != null)
            {
                midiReBuzzControllers.Add(controller);
            }
            controller = RegGetController("MidiControllerStop");
            if (controller != null)
            {
                midiReBuzzControllers.Add(controller);
            }
            controller = RegGetController("MidiControllerRecord");
            if (controller != null)
            {
                midiReBuzzControllers.Add(controller);
            }
            controller = RegGetController("MidiControllerForward");
            if (controller != null)
            {
                midiReBuzzControllers.Add(controller);
            }
            controller = RegGetController("MidiControllerBackward");
            if (controller != null)
            {
                midiReBuzzControllers.Add(controller);
            }

            int numControllers = RegGetNumberOfMidiControllers();
            for (int i = 0; i < numControllers; i++)
            {
                MidiController mc = RegGetControllerById(i);
                if (mc != null)
                {
                    midiControllers.Add(mc);
                }
            }
        }

        // Return true if controller assignment found
        public static MidiController RegGetController(string regKey)
        {
            MidiController mc = new MidiController();

            string val = RegistryEx.Read(regKey, "", "Settings");

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

        public static MidiController RegGetControllerById(int id)
        {
            string regKey = "MidiController" + id;
            return RegGetController(regKey);
        }

        public static void RegSetController(string regKey, string name, int channel, int controller)
        {
            var values = name + "," + channel + "," + controller;
            RegistryEx.Write(regKey, values, "Settings");
        }

        public static void RegSetControllerById(int id, string name, int channel, int controller)
        {
            string regKey = "MidiController" + id;
            RegSetController(regKey, name, channel, controller);
        }

        public static int RegGetNumberOfMidiControllers()
        {
            return RegistryEx.Read("numMidiControllers", 0, "Settings");
        }

        public static void RegSetNumberOfMidiControllers(int num)
        {
            RegistryEx.Write("numMidiControllers", num, "Settings");
        }

        public static void RegClearAllMidiControllers(int numControllers)
        {
            string regKeyBase = Global.RegistryRoot + "Settings\\MidiController";
            int id = 0;

            for (int i = 0; i < numControllers; i++)
            {
                string key = regKeyBase + id;
                try
                {
                    Registry.CurrentUser.DeleteSubKey(key);
                }
                catch { }

                id++;
            }

            RegSetNumberOfMidiControllers(0);
        }

        internal IList<string> GetMidiControllerNames()
        {
            List<string> names = new List<string>();
            midiControllers.ForEach(m => names.Add(m.Name));
            return names;
        }

        internal void Add(string name, int channel, int controller, int value)
        {
            int index = midiControllers.Count;
            MidiController midiController = new MidiController();
            midiController.Name = name;
            midiController.Contoller = controller;
            midiController.Channel = channel;
            midiController.Value = value;

            midiControllers.Add(midiController);
            RegSetControllerById(index, name, channel, controller);
            RegSetNumberOfMidiControllers(midiControllers.Count);
        }

        internal int GetAssignmentIndex(int ctrl, int channel, int value)
        {
            int index = 0;
            foreach (MidiController midiController in midiControllers)
            {
                if (midiController.Contoller == ctrl && midiController.Channel == channel)
                {
                    return index;
                }
                index++;
            }
            return -1;
        }

        readonly List<ContollerBinding> contollerBindings = new List<ContollerBinding>();
        internal void BindParameter(ParameterCore parameterCore, int track, int mcindex)
        {
            ContollerBinding contollerBind = new ContollerBinding(parameterCore, track, mcindex);
            contollerBindings.Add(contollerBind);
        }

        internal void UnbindAllMIDIControllers(IMachine machineCore)
        {
            contollerBindings.RemoveAll(cb => cb.Parameter.Group.Machine == machineCore);
        }

        internal class ContollerBinding
        {
            internal ParameterCore Parameter { get; }
            internal int Track { get; }
            internal int McIndex { get; }

            internal ContollerBinding(ParameterCore parameter, int track, int mcIndex)
            {
                Parameter = parameter;
                Track = track;
                McIndex = mcIndex;
            }
        }
    }
}
