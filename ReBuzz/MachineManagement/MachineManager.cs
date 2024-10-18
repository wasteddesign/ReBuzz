using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using ReBuzz.Core;
using ReBuzz.ManagedMachine;
using ReBuzz.NativeMachine;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ReBuzz.MachineManagement
{
    public enum BuzzWorkMode
    {
        WM_NOIO = 0,
        WM_READ,
        WM_WRITE,
        WM_READWRITE
    }

    public struct MachineInitData
    {
        public byte[] data;
        public int tracks;
    }
    public class MachineManager
    {
        ReBuzzCore buzz;
        public ReBuzzCore Buzz
        {
            get => buzz;
            set
            {
                if (buzz != null)
                {
                    foreach (var nm in NativeMachines.Values)
                    {
                        nm.Dispose();
                    }
                }

                buzz = value;

                if (buzz != null)
                {
                }
            }
        }

        // Adjust these to support old machines
        public static readonly int BUZZ_MACHINE_INTERFACE_VERSION_12 = 12;
        public static readonly int BUZZ_MACHINE_INTERFACE_VERSION_15 = 15; //buzz v1.2
        public static readonly int BUZZ_MACHINE_INTERFACE_VERSION_42 = 42;

        readonly Dictionary<MachineCore, NativeMachineHost> nativeMachines = new Dictionary<MachineCore, NativeMachineHost>();
        internal Dictionary<MachineCore, NativeMachineHost> NativeMachines { get => nativeMachines; }

        readonly Dictionary<MachineCore, ManagedMachineHost> managedMachines = new Dictionary<MachineCore, ManagedMachineHost>();
        internal Dictionary<MachineCore, ManagedMachineHost> ManagedMachines { get => managedMachines; }

        readonly ConcurrentDictionary<MachineCore, MachineWorkInstance> workInstances = new ConcurrentDictionary<MachineCore, MachineWorkInstance>();
        public bool IsSingleProcessMode { get; internal set; }

        private readonly SongCore song;

        internal MachineManager(SongCore song)
        {
            this.song = song;
            IsSingleProcessMode = false;
        }

        // instrumentPath == null or "" if instruments are not supported
        public MachineCore CreateMachine(string libName, string path, string instrument, byte[] data, int trackCount, float x, float y, bool hidden, string machineName = null, bool callInit = true)
        {
            //lock (ReBuzzCore.AudioLock)
            {
                MachineCore machine = new MachineCore(song);
                machine.InstrumentName = instrument;
                machine.Position = new Tuple<float, float>(x, y);
                machine.Hidden = hidden;

                // If missing
                if (!buzz.MachineDLLs.ContainsKey(libName))
                {
                    var machineDLL = machine.MachineDLL;
                    machineDLL.IsMissing = true;
                    machineDLL.Name = libName;
                    machineDLL.Path = path;
                    machine.MachineDLL = machineDLL;
                    machine.MachineDLL.Buzz = buzz;
                    machine.Name = GetNewMachineName(machineName);
                    machine.TrackCount = trackCount;

                    buzz.AddMachine(machine);
                    machine.Ready = true;
                    return machine;
                }

                var mDll = buzz.MachineDLLs[libName] as MachineDLL;
                machine.MachineDLL = mDll;
                machine.MachineDLL.Buzz = buzz;

                string name = machineName != null ? machineName : mDll.Info.ShortName;
                buzz.RenameMachine(machine, name);

                if (mDll.IsManaged)
                {
                    CreateManagedMachine(machine, trackCount, data);
                }
                else
                {
                    CreateNativeMachine(machine, instrument, trackCount, data, callInit);
                }
                machine.invalidateWaves = true;
                return machine;
            }
        }

        public void CreateManagedMachine(MachineCore machine, int trackcount, byte[] data)
        {
            ManagedMachineDLL managedMachineDLL = new ManagedMachineDLL();
            managedMachineDLL.LoadManagedMachine(machine.MachineDLL.Path);
            ManagedMachineHost managedMachineHost = new ManagedMachineHost(managedMachineDLL);
            machine.ManagedMachine = managedMachineHost.ManagedMachine;
            UpdateMasterAndSubTickInfo(managedMachineHost); // Needs to be updated before AddMachine
            managedMachineHost.Machine = machine;

            managedMachineDLL.UpdateMachineDllInfo(machine);
            machine.IsControlMachine = managedMachineHost.IsControlMachine || machine.DLL.Info.Flags.HasFlag(MachineInfoFlags.CONTROL_MACHINE);

            // Create Parameters
            managedMachineDLL.CreateMachineDllParameters(machine);

            machine.MachineDLL.IsLoaded = true;
            machine.TrackCount = trackcount;
            machine.MachineDLL.ManagedDLL = managedMachineDLL;

            ManagedMachines.Add(machine, managedMachineHost);

            machine.SetCommands();

            if (managedMachineDLL.machineInfo.OutputCount > 0 && machine.DLL.Info.Flags.HasFlag(MachineInfoFlags.MULTI_IO))
            {
                machine.OutputChannelCount = managedMachineDLL.machineInfo.OutputCount;
            }
            if (managedMachineDLL.machineInfo.InputCount > 0 && machine.DLL.Info.Flags.HasFlag(MachineInfoFlags.MULTI_IO) &&
                machine.DLL.Info.Type == MachineType.Effect)
            {
                machine.InputChannelCount = managedMachineDLL.machineInfo.InputCount;
            }

            if (machine.DLL.Info.Type == MachineType.Effect)
            {
                machine.MachineDLL.MachineInfo.Flags |= MachineInfoFlags.STEREO_EFFECT;
            }

            if (machine.DLL.Info.Flags.HasFlag(MachineInfoFlags.PATTERN_EDITOR))
            {
                managedMachineHost.SetPatternEditorData(data);
            }
            else
            {
                machine.Data = data;
            }

            buzz.AddMachine(machine);

            // Set default values
            managedMachineHost.SetParameterDefaults(machine);
            machine.Ready = true;
        }

        NativeMachineHost nativeMachineHostSingleProcess32;
        NativeMachineHost nativeMachineHostSingleProcess64;

        void CreateNativeMachine(MachineCore machine, string instrument, int trackCount, byte[] data, bool callInit = true)
        {
            NativeMachineHost nativeMachineHost;
            if (IsSingleProcessMode)
            {
                if (machine.MachineDLL.Is64Bit)
                {
                    if (nativeMachineHostSingleProcess64 == null)
                    {
                        nativeMachineHostSingleProcess64 = new NativeMachineHost("ReBuzzConnectID");
                        nativeMachineHostSingleProcess64.InitHost(Buzz, machine.MachineDLL.Is64Bit);
                    }
                    nativeMachineHost = nativeMachineHostSingleProcess64;
                }
                else
                {
                    if (nativeMachineHostSingleProcess32 == null)
                    {
                        nativeMachineHostSingleProcess32 = new NativeMachineHost("ReBuzzConnectID");
                        nativeMachineHostSingleProcess32.InitHost(Buzz, machine.MachineDLL.Is64Bit);
                    }
                    nativeMachineHost = nativeMachineHostSingleProcess32;
                }
            }
            else
            {
                nativeMachineHost = new NativeMachineHost("ReBuzzConnectID");
                nativeMachineHost.InitHost(Buzz, machine.MachineDLL.Is64Bit);
            }

            machine.InstrumentName = instrument;
            var uiMessage = nativeMachineHost.UIMessage;
            var audioMessage = nativeMachineHost.AudioMessage;
            uiMessage.SendMessageBuzzInitSync(Buzz.MainWindowHandle, machine.MachineDLL.Is64Bit);
            uiMessage.UIDSPInitSync(ReBuzzCore.masterInfo.SamplesPerSec);

            uiMessage.UILoadLibrarySync(buzz, machine, machine.DLL.Name, machine.DLL.Path);

            GetChannels(machine, out int inputs, out int outputs);

            if (machine.DLL.Info.Type == MachineType.Effect)
            {
                machine.OutputChannelCount = 1;
                machine.InputChannelCount = 1;
            }
            else if (machine.DLL.Info.Type == MachineType.Generator)
            {
                machine.OutputChannelCount = 1;
            }

            machine.HasStereoInput = inputs > 1;
            machine.HasStereoOutput = outputs > 1;

            uiMessage.UINewMISync(machine, machine.DLL.Name);

            buzz.AddMachine(machine);

            // Machine Skin
            uiMessage.UIGetResources(machine, out BitmapSource skin, out BitmapSource led, out Point ledPosition);

            machine.MachineDLL.Skin = skin;
            machine.MachineDLL.SkinLED = led;
            if (led != null)
            {
                machine.MachineDLL.SkinLEDSize = new Size(led.Width, led.Height);
            }

            machine.IsControlMachine = machine.MachineDLL.Info.Flags.HasFlag(MachineInfoFlags.CONTROL_MACHINE);
            machine.MachineDLL.SkinLEDPosition = ledPosition;

            nativeMachines.Add(machine, nativeMachineHost);

            if (callInit)
            {
                CallInit(machine, data, trackCount);
            }
        }

        internal void CallInit(MachineCore machine, byte[] data, int trackCount)
        {
            var nativeMachineHost = nativeMachines[machine];
            var uiMessage = nativeMachineHost.UIMessage;
            var audioMessage = nativeMachineHost.AudioMessage;

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) && machine.DLL.Name.StartsWith("Polac"))
            {
                // Debug
                if (MessageBox.Show("Skip initializing " + machine.Name + "?", "Safe load machine?", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    uiMessage.UIInit(machine, new byte[0]);
                }
                else
                {
                    uiMessage.UIInit(machine, data != null ? data : new byte[0]);
                }
            }
            else
            {
                uiMessage.UIInit(machine, data != null ? data : new byte[0]);
            }

            var instrument = machine.InstrumentName;
            if (instrument != null && instrument != "")
            {
                uiMessage.UISetInstrument(machine, instrument);
            }

            // Call Attributes changed
            uiMessage.UIAttributesChanged(machine);
            // Set number of tracks
            machine.TrackCount = trackCount;
            audioMessage.AudioSetNumTracks(machine, trackCount);

            machine.Ready = true;
        }

        internal IMachineDLL GetPatternEditorDLL(MachineCore machine)
        {
            // ToDo: Check register for default machines.
            return buzz.MachineDLLs["Modern Pattern Editor"];
        }

        internal void GetChannels(MachineCore machine, out int inputChannels, out int outputChannels)
        {
            int inputs = 0, outputs = 0;
            var flags = machine.MachineDLL.Info.Flags;
            if (flags.HasFlag(MachineInfoFlags.MULTI_IO))
            {
                if (machine.MachineDLL.Info.Type == MachineType.Effect)
                {
                    inputs = outputs = 2; // stereo
                }
                else if (machine.MachineDLL.Info.Type == MachineType.Generator && !flags.HasFlag(MachineInfoFlags.NO_OUTPUT))
                {
                    outputs = 2;
                }
            }
            else if (flags.HasFlag(MachineInfoFlags.MONO_TO_STEREO) && !flags.HasFlag(MachineInfoFlags.DOES_INPUT_MIXING))
            {
                if (machine.MachineDLL.Info.Type == MachineType.Effect)
                {
                    inputs = 1; outputs = 2; // mono to stereo
                }
                else if (machine.MachineDLL.Info.Type == MachineType.Generator && !flags.HasFlag(MachineInfoFlags.NO_OUTPUT))
                {
                    outputs = 2;
                }
            }
            else if (flags.HasFlag(MachineInfoFlags.DOES_INPUT_MIXING) ||
                flags.HasFlag(MachineInfoFlags.STEREO_EFFECT))
            {
                if (machine.MachineDLL.Info.Type == MachineType.Effect)
                {
                    inputs = outputs = 2; // stereo
                }
                else if (machine.MachineDLL.Info.Type == MachineType.Generator && !flags.HasFlag(MachineInfoFlags.NO_OUTPUT))
                {
                    outputs = 2;
                }
            }
            else
            {
                if (machine.MachineDLL.Info.Type == MachineType.Effect)
                {
                    inputs = outputs = 1; // mono
                }
                else if (machine.MachineDLL.Info.Type == MachineType.Generator && !flags.HasFlag(MachineInfoFlags.NO_OUTPUT))
                {
                    outputs = 1;
                }
            }

            inputChannels = inputs;
            outputChannels = outputs;
        }

        public string GetNewMachineName(string name)
        {
            while (Buzz.SongCore.MachinesList.FirstOrDefault(m => m.Name == name) != null)
            {
                var result = string.Concat(name.ToArray().Reverse().TakeWhile(char.IsNumber).Reverse());
                if (result.Length > 0 &&
                    name.Length > result.Length &&
                    name.Substring(name.Length - result.Length - 3).StartsWith(" | "))
                {
                    int number = int.Parse(result);
                    number++;
                    name = name.Substring(0, name.Length - result.Length) + number;
                }
                else
                {
                    name += " | 2";
                }
            }

            return name;
        }

        public MachineCore GetMaster(ReBuzzCore buzz)
        {
            MachineCore machine = new MachineCore(song);
            machine.Name = "Master";
            machine.InputChannelCount = 1;
            machine.OutputChannelCount = 0;
            //machine.TrackCount = 0;
            machine.Graph = song;
            machine.HasStereoOutput = true;
            machine.HasStereoInput = true;

            var dll = machine.MachineDLL;
            dll.IsCrashed = false;
            dll.IsLoaded = true;
            dll.IsManaged = true;
            dll.IsMissing = false;
            dll.IsOutOfProcess = false;
            dll.Is64Bit = true;
            dll.Buzz = buzz;
            dll.Path = Global.BuzzPath + "\\Gear";
            dll.Name = "Master";

            //dll.TextColor = Global.Buzz.ThemeColors["MV Machine Text"] != null ? Global.Buzz.ThemeColors["MV Machine Text"] : Colors.GhostWhite;


            var mi = machine.MachineDLL.MachineInfo;
            mi.ShortName = "Master";
            mi.Author = "WDE";
            mi.MinTracks = 0;
            mi.MaxTracks = 0;
            mi.Version = 100;
            mi.Name = "Master";
            mi.InternalVersion = 100;
            mi.Flags = MachineInfoFlags.NO_OUTPUT;
            mi.Type = MachineType.Master;
            machine.MIDIInputChannel = -1;

            ParameterGroup pgGlobal = new ParameterGroup(machine, ParameterGroupType.Global);
            pgGlobal.TrackCount = 1;
            pgGlobal.Type = ParameterGroupType.Global;

            var parameter = new ParameterCore();
            parameter.Name = "Volume";
            parameter.Description = "Master Volume (0=0 dB, 4000=-80 dB)";
            parameter.MinValue = 0;
            parameter.MaxValue = 0x4000;
            parameter.DefValue = 0;
            parameter.NoValue = 65535;
            parameter.Type = ParameterType.Word;
            parameter.Flags = ParameterFlags.State;
            parameter.SetValue(0, parameter.DefValue);
            parameter.IndexInGroup = 0;
            pgGlobal.AddParameter(parameter);

            parameter = new ParameterCore();
            parameter.Name = "BPM";
            parameter.Description = "Beats Per Minute (10-200 hex)";
            parameter.MinValue = 10;
            parameter.MaxValue = 0x200;
            parameter.DefValue = 126;
            parameter.NoValue = 65535;
            parameter.Type = ParameterType.Word;
            parameter.Flags = ParameterFlags.State;
            parameter.SetValue(0, parameter.DefValue);
            parameter.IndexInGroup = 1;
            pgGlobal.AddParameter(parameter);

            parameter = new ParameterCore();
            parameter.Name = "TPB";
            parameter.Description = "Ticks Per Beat (1-20 hex)";
            parameter.MinValue = 1;
            parameter.MaxValue = 0x20;
            parameter.DefValue = 4;
            parameter.NoValue = 255;
            parameter.Type = ParameterType.Byte;
            parameter.Flags = ParameterFlags.State;
            parameter.SetValue(0, parameter.DefValue);
            parameter.IndexInGroup = 2;
            pgGlobal.AddParameter(parameter);

            machine.ParameterGroupsList.Add(pgGlobal);

            ParameterGroup pgTracks = new ParameterGroup(machine, ParameterGroupType.Track);
            pgTracks.TrackCount = 0;

            machine.ParameterGroupsList.Add(pgTracks);
            return machine;
        }

        internal static int PatternEditorNumber = 1;
        internal string GetPEName()
        {
            string name = System.Text.Encoding.UTF8.GetString(new byte[1] { 1 }, 0, 1);
            name += "pe" + PatternEditorNumber;
            PatternEditorNumber++;
            return name;
        }

        // Old machines expect this when pos in tick == 0
        internal void Tick(MachineCore machine)
        {
            var wi = GetMachineWorkInstance(machine);
            wi.Tick(true);
        }

        internal void AudioBeginFrame(MachineCore machine)
        {
            if (nativeMachines.ContainsKey(machine))
            {
                var audiom = nativeMachines[machine].AudioMessage;
                audiom.AudioBeginFrame(machine);
            }
        }

        internal bool MachineEvent(MachineCore machine, CMachineEvent me, int v)
        {
            bool ret = false;
            if (nativeMachines.ContainsKey(machine))
            {
                ret = nativeMachines[machine].UIMessage.UISendEvent(machine, me, v);
            }

            return ret;
        }

        internal string DescribeValue(MachineCore machine, int index, int value)
        {
            string str = value.ToString();

            if (managedMachines.ContainsKey(machine))
            {
                str = managedMachines[machine].DescribeParameterValue(index, value);
            }
            if (nativeMachines.ContainsKey(machine))
            {
                str = nativeMachines[machine].UIMessage.UIDescribeValue(machine, index, value);
            }

            return str;
        }

        internal void Command(MachineCore machine, int x)
        {
            if (managedMachines.ContainsKey(machine))
            {
                managedMachines[machine].ExecuteCommad((BuzzCommand)x);
            }
            else if (nativeMachines.ContainsKey(machine))
            {
                nativeMachines[machine].UIMessage.UICommand(machine, x);
            }
        }

        internal void AudioBeginBlock(MachineCore machine)
        {
            if (nativeMachines.ContainsKey(machine))
            {
                nativeMachines[machine].AudioMessage.AudioBeginBlock(machine, null);
            }
        }

        internal void DeleteMachine(MachineCore machine)
        {
            if (machine == null)
                return;

            lock (ReBuzzCore.AudioLock)
            {
                machine.Ready = false;
                machine.ClearEvents();

                if (nativeMachines.ContainsKey(machine))
                {
                    /*
                    if (!machine.DLL.IsCrashed)
                    {
                        nativeMachines[machine].UIMessage.UIDeleteMI(machine);
                        if (!IsSingleProcessMode)
                        {
                            nativeMachines[machine].Dispose();
                        }
                    }
                    */
                    if (!IsSingleProcessMode)
                    {
                        // Just kill the process
                        nativeMachines[machine].Dispose();
                    }
                    else if (!machine.DLL.IsCrashed)
                    {
                        nativeMachines[machine].UIMessage.UIDeleteMI(machine);
                    }

                    nativeMachines.Remove(machine);
                }
                else if (managedMachines.ContainsKey(machine))
                {
                    var machineHost = managedMachines[machine];
                    machineHost.Release();
                    managedMachines.Remove(machine);
                }

                if (workInstances.ContainsKey(machine))
                {
                    workInstances.TryRemove(machine, out var dummy);
                }
            }
        }

        internal void SendMidiInput(IMachine machine, int data, bool polacConversion)
        {
            //lock (machine)
            //lock (ReBuzzCore.AudioLock)
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

                var mc = machine as MachineCore;
                if (managedMachines.ContainsKey(mc))
                {
                    var mmh = managedMachines[mc];
                    if (commandCode == MIDI.NoteOn)
                    {
                        mmh.MidiNote(channel, data1, data2);
                    }
                    else if (commandCode == MIDI.NoteOff)
                    {
                        mmh.MidiNote(channel, data1, 0);
                    }
                    else if (commandCode == MIDI.ControlChange)
                    {
                        mmh.MidiControlChange(data1, channel, data2);
                    }
                    else if (commandCode == MIDI.PitchWheel)
                    {
                        int val = (data1 | (data2 << 7));
                        if (polacConversion)
                            mmh.MidiControlChange(0xff, channel, val);
                        else
                            mmh.MidiControlChange(commandCode, channel, val);
                    }
                    else if (commandCode == 208) // Aftertouch
                    {
                        if (polacConversion)
                            mmh.MidiControlChange(0xfc, channel, data1);
                        else
                            mmh.MidiControlChange(commandCode, channel, data1);
                    }
                }
                if (nativeMachines.ContainsKey(mc))
                {
                    var nmh = nativeMachines[mc];

                    if (commandCode == MIDI.NoteOn)
                    {
                        nmh.MidiMessage.MidiNote(mc, channel, data1, data2);
                    }
                    else if (commandCode == MIDI.NoteOff)
                    {
                        nmh.MidiMessage.MidiNote(mc, channel, data1, 0);
                    }
                    else if (commandCode == MIDI.ControlChange)
                    {
                        nmh.MidiMessage.MidiControlChange(mc, data1, channel, data2);
                    }
                    else if (commandCode == MIDI.PitchWheel)
                    {
                        int val = (data1 | (data2 << 7));
                        if (polacConversion)
                            nmh.MidiMessage.MidiControlChange(mc, 0xff, channel, val);
                        else
                            nmh.MidiMessage.MidiControlChange(mc, commandCode, channel, val);
                    }
                    else if (commandCode == 208) // Aftertouch
                    {
                        if (polacConversion)
                            nmh.MidiMessage.MidiControlChange(mc, 0xfc, channel, data1);
                        else
                            nmh.MidiMessage.MidiControlChange(mc, commandCode, channel, data1);
                    }
                }
            }
        }

        internal byte[] SendGUIMessage(MachineCore machine, byte[] message)
        {
            if (nativeMachines.ContainsKey(machine))
            {
                // TODO: Check if machine locks are needed elsewhere
                lock (machine)
                {
                    return nativeMachines[machine].UIMessage.UIHandleGUIMessage(machine, message);
                }
            }

            return null;
        }

        internal void SetNumTracks(MachineCore machine, int trackCount)
        {
            if (nativeMachines.ContainsKey(machine))
            {
                lock (ReBuzzCore.AudioLock)
                {
                    nativeMachines[machine].AudioMessage.AudioSetNumTracks(machine, trackCount);
                }
            }
        }

        internal string GetChannelName(MachineCore machine, bool input, int index)
        {
            string ret = "";

            if (managedMachines.ContainsKey(machine))
            {
                ret = managedMachines[machine].GetChannelName(input, index);
            }
            if (nativeMachines.ContainsKey(machine))
            {
                ret = nativeMachines[machine].UIMessage.UIGetChannelName(machine, input, index);
            }

            return ret;
        }

        internal UserControl GetPatternEditorControl(MachineCore machine)
        {
            UserControl ret = null;
            if (machine != null && machine.DLL.IsManaged && managedMachines.ContainsKey(machine))
            {
                var machineHost = ManagedMachines[machine];
                ret = machineHost.PatternEditorControl;
            }
            return ret;
        }

        internal void SetPatternEditorPattern(MachineCore machine, IPattern p)
        {
            if (machine.DLL.IsManaged && managedMachines.ContainsKey(machine) && p != null)
            {
                try
                {
                    var machineHost = ManagedMachines[machine];
                    machineHost.SetEditorPattern = p;
                }
                catch { }
            }
        }

        internal void RecordContolChange(MachineCore machine, ParameterCore parameter, int track, int value)
        {
            if (machine.DLL.IsManaged && managedMachines.ContainsKey(machine))
            {
                var machineHost = ManagedMachines[machine];
                machineHost.RecordControlChange(parameter, track, value);
            }
        }

        internal void CreatePatternCopy(MachineCore machine, IPattern newp, IPattern p)
        {
            if (machine.DLL.IsManaged && managedMachines.ContainsKey(machine))
            {
                var machineHost = ManagedMachines[machine];
                machineHost.CreatePatternCopy(newp, p);
            }
        }

        internal byte[] GetMachineData(MachineCore machine)
        {
            // ToDo: Add checks to all machine calls and check if machine has crashed.
            if (machine.DLL.IsCrashed)
                return null;

            if (machine.DLL.IsManaged && managedMachines.ContainsKey(machine))
            {
                var machineHost = ManagedMachines[machine];
                if (machine.DLL.Info.Flags.HasFlag(MachineInfoFlags.PATTERN_EDITOR))
                {
                    return machineHost.GetPatternEditorData();
                }
                else
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        var data = machineHost.MachineState;
                        ms.Write(BitConverter.GetBytes(2), 0, 1);      // Version
                        int size = data != null ? data.Length : 0;
                        ms.Write(BitConverter.GetBytes(size), 0, 4);         // Size
                        if (data != null)
                            ms.Write(data, 0, size);                          // Content
                        return ms.ToArray();
                    }
                }
            }
            else if (nativeMachines.ContainsKey(machine))
            {
                //lock (ReBuzzCore.AudioLock)
                {
                    return nativeMachines[machine].UIMessage.UISave(machine);
                }
            }
            return null;
        }

        internal void SetMachineData(MachineCore machine, byte[] value)
        {
            if (machine.DLL.IsCrashed)
                return;

            if (machine.DLL.IsManaged && managedMachines.ContainsKey(machine))
            {
                var machineHost = ManagedMachines[machine];
                if (machine.DLL.Info.Flags.HasFlag(MachineInfoFlags.PATTERN_EDITOR))
                {
                    machineHost.SetPatternEditorData(value);
                }
                else if (value != null)
                {
                    machineHost.MachineState = value.Skip(5).ToArray();
                }
            }
            else if (nativeMachines.ContainsKey(machine))
            {
                // Implement Load if needed
                nativeMachines[machine].UIMessage.UILoad(machine, value);
            }
        }

        internal void Stop(MachineCore machine)
        {
            if (machine.DLL.IsCrashed)
                return;

            machine.IsSeqThru = machine.IsSeqMute = false;

            if (machine.DLL.IsManaged && managedMachines.ContainsKey(machine))
            {
                var machineHost = ManagedMachines[machine];
                machineHost.Stop();
            }
            else if (nativeMachines.ContainsKey(machine))
            {
                nativeMachines[machine].UIMessage.UIStop(machine);
            }

            foreach (var pg in machine.ParameterGroupsList)
                foreach (var p in pg.ParametersList)
                    p.ClearPVal();
        }

        internal void UpdateMasterAndSubTickInfoToHost()
        {
            foreach (var machineHost in managedMachines.Values)
            {
                if (machineHost != null)
                    UpdateMasterAndSubTickInfo(machineHost);
            }
        }

        internal void UpdateMasterAndSubTickInfo(ManagedMachineHost machineHost)
        {
            var masterInfo = ReBuzzCore.masterInfo;
            var hostInfo = machineHost.MasterInfo;
            hostInfo.PosInTick = masterInfo.PosInTick;
            hostInfo.SamplesPerTick = masterInfo.SamplesPerTick;
            hostInfo.BeatsPerMin = masterInfo.BeatsPerMin;
            hostInfo.TicksPerBeat = masterInfo.TicksPerBeat;
            hostInfo.SamplesPerSec = masterInfo.SamplesPerSec;
            hostInfo.TicksPerSec = masterInfo.TicksPerSec;

            var subTickInfo = ReBuzzCore.subTickInfo;
            var hostSubTick = machineHost.SubTickInfo;
            hostSubTick.CurrentSubTick = subTickInfo.CurrentSubTick;
            hostSubTick.SubTicksPerTick = subTickInfo.SubTicksPerTick;
            hostSubTick.SamplesPerSubTick = subTickInfo.SamplesPerSubTick;
            hostSubTick.PosInSubTick = subTickInfo.PosInSubTick;
        }

        internal void ResetMachines()
        {
            foreach (var machine in buzz.SongCore.MachinesList)
            {
                if (nativeMachines.ContainsKey(machine))
                {
                    var host = nativeMachines[machine];
                    host.UIMessage.UIDSPInitSync(buzz.SelectedAudioDriverSampleRate);
                }
            }
        }

        internal MachineWorkInstance GetMachineWorkInstance(MachineCore machine)
        {
            if (!workInstances.ContainsKey(machine))
            {
                var mwi = new MachineWorkInstance(machine, Buzz);
                workInstances[machine] = mwi;
                return mwi;
            }
            else
            {
                return workInstances[machine];
            }
        }

        internal string[] GetSubMenu(MachineCore machine, int i)
        {
            string[] res = null;
            if (machine.DLL.IsManaged && managedMachines.ContainsKey(machine))
            {
                // Implement Load if needed
            }
            else if (nativeMachines.ContainsKey(machine))
            {
                var machineHost = NativeMachines[machine];
                res = machineHost.UIMessage.GetSubMenu(machine, i);

            }

            return res;
        }

        internal void SendMIDIControlChange(MachineCore machine, int ctrl, int channel, int value)
        {
            if (machine.DLL.IsManaged && managedMachines.ContainsKey(machine))
            {
                var machineHost = ManagedMachines[machine];
                machineHost.MidiControlChange(ctrl, channel, value);
            }
            else if (nativeMachines.ContainsKey(machine) && machine.DLL.Info.Version >= BUZZ_MACHINE_INTERFACE_VERSION_42) // Check if the machine supports CMachineInterfaceEx
            {
                var machineHost = NativeMachines[machine];
                machineHost.MidiMessage.MidiControlChange(machine, ctrl, channel, value);
            }
        }

        internal void SendMIDINote(MachineCore machine, int channel, int value, int velocity)
        {
            if (machine.DLL.IsManaged && managedMachines.ContainsKey(machine))
            {
                var machineHost = ManagedMachines[machine];
                machineHost.MidiNote(channel, value, velocity);
            }
            else if (nativeMachines.ContainsKey(machine))
            {
                var machineHost = NativeMachines[machine];
                machineHost.MidiMessage.MidiNote(machine, channel, value, velocity);
            }
        }

        internal void ImportFinished(MachineCore machine, IDictionary<string, string> importDictionary)
        {
            if (machine.DLL.IsManaged && managedMachines.ContainsKey(machine))
            {
                var machineHost = ManagedMachines[machine];
                machineHost.ImportFinished(importDictionary);
            }
            else if (nativeMachines.ContainsKey(machine) && machine.DLL.Info.Version > BUZZ_MACHINE_INTERFACE_VERSION_15)
            {
                var machineHost = NativeMachines[machine];
                machineHost.UIMessage.ImportFinished(machine);
            }
        }

        internal IEnumerable<IMenuItem> GetCommands(MachineCore machine)
        {
            if (machine.DLL.IsManaged && managedMachines.ContainsKey(machine))
            {
                var machineHost = ManagedMachines[machine];
                return machineHost.Commands;
            }
            else if (nativeMachines.ContainsKey(machine))
            {
                var machineHost = NativeMachines[machine];
                return machineHost.UIMessage.GetCommands(machine);
            }
            return null;
        }

        internal void SetValue(MachineCore machine, ParameterCore parameter, int track, int value)
        {
            if (machine.DLL.IsManaged && managedMachines.ContainsKey(machine))
            {
                if (parameter.Group.Type == ParameterGroupType.Input)
                {
                    return;
                }
                var machineHost = ManagedMachines[machine];
                int index = parameter.IndexInGroup;
                if (parameter.Group.Type == ParameterGroupType.Track)
                    index += machine.ParameterGroups[1].Parameters.Count;
                machineHost.SetParameterValue(index, track, value);
            }
        }

        internal int[] GetPatternEditorMachineMIDIEvents(MachineCore machine, PatternCore pattern)
        {
            var editor = machine.EditorMachine;
            if (managedMachines.ContainsKey(editor))
            {
                var machineHost = ManagedMachines[editor];
                return machineHost.GetPatternEditorMachineMIDIEvents(pattern);
            }
            return new int[0];
        }

        internal void SetPatternEditorMachineMIDIEvents(MachineCore machine, PatternCore pattern, int[] data)
        {
            var editor = machine.EditorMachine;
            if (managedMachines.ContainsKey(editor))
            {
                var machineHost = ManagedMachines[editor];
                machineHost.SetPatternEditorMachineMIDIEvents(pattern, data);
            }
        }

        internal IEnumerable<IPatternEditorColumn> PatternEditorMachineEvents(MachineCore machine, PatternCore patternCore)
        {
            var editor = machine.EditorMachine;
            if (managedMachines.ContainsKey(editor))
            {
                var machineHost = ManagedMachines[editor];
                return machineHost.GetPatternCloumnEvents(patternCore, 0, int.MaxValue);
            }
            return new List<IPatternEditorColumn>();
        }

        internal void ActivateEditor(MachineCore em)
        {
            if (em != null && managedMachines.ContainsKey(em))
            {
                var machineHost = ManagedMachines[em];
                machineHost.Activate();
            }
        }

        internal void LostMidiFocus(MachineCore machine)
        {
            if (nativeMachines.ContainsKey(machine) && machine.DLL.Info.Version >= BUZZ_MACHINE_INTERFACE_VERSION_42)
            {
                var host = nativeMachines[machine];
                // Ignoring for now. Deadlock.
                //host.UIMessage.UILostMidiFocus(machine);
            }
        }

        internal void GotMidiFocus(MachineCore machine)
        {
            if (nativeMachines.ContainsKey(machine) && machine.DLL.Info.Version >= BUZZ_MACHINE_INTERFACE_VERSION_42)
            {
                var host = nativeMachines[machine];

                // Ignoring for now. Deadlock.
                //host.UIMessage.UIGotMidiFocus(machine);
            }
        }

        internal void SetInstrument(MachineCore machine, string newInstrument)
        {
            if (nativeMachines.ContainsKey(machine) && machine.DLL.Info.Version >= BUZZ_MACHINE_INTERFACE_VERSION_42)
            {
                var host = nativeMachines[machine];
                host.UIMessage.UISetInstrument(machine, newInstrument);
            }
        }

        internal void AddInput(MachineCore machine, IMachine source, bool stereo)
        {
            lock (ReBuzzCore.AudioLock)
            {
                // Check also if machine has MIF_DOES_INPUT_MIXING?
                if (nativeMachines.ContainsKey(machine) && machine.DLL.Info.Version >= BUZZ_MACHINE_INTERFACE_VERSION_15 /* && machine.DLL.Info.Flags.HasFlag(MachineInfoFlags.DOES_INPUT_MIXING) */)
                {
                    var host = nativeMachines[machine];
                    host.UIMessage.UIAddInput(machine, source.Name, stereo);
                }
            }
        }
        internal void DeleteInput(MachineCore machine, IMachine source)
        {
            if (nativeMachines.ContainsKey(machine) && machine.DLL.Info.Version >= BUZZ_MACHINE_INTERFACE_VERSION_15 /* && machine.DLL.Info.Flags.HasFlag(MachineInfoFlags.DOES_INPUT_MIXING) */)
            {
                var host = nativeMachines[machine];
                host.UIMessage.UIDeleteInput(machine, source.Name);
            }
        }

        internal void RenameInput(MachineCore machine, string oldName, string newName)
        {
            if (nativeMachines.ContainsKey(machine) && machine.DLL.Info.Version >= BUZZ_MACHINE_INTERFACE_VERSION_15 /* && machine.DLL.Info.Flags.HasFlag(MachineInfoFlags.DOES_INPUT_MIXING) */)
            {
                var host = nativeMachines[machine];
                host.UIMessage.UIRenameInput(machine, oldName, newName);
            }
        }

        internal void SetInputChannels(MachineCore machine, string name, bool stereo)
        {
            if (nativeMachines.ContainsKey(machine) && machine.DLL.Info.Version >= BUZZ_MACHINE_INTERFACE_VERSION_15 && machine.DLL.Info.Flags.HasFlag(MachineInfoFlags.DOES_INPUT_MIXING))
            {
                var host = nativeMachines[machine];
                host.UIMessage.UISetInputChannels(machine, name, stereo);
            }
        }

        internal void AttributesChanged(MachineCore machine)
        {
            if (machine != null && nativeMachines.ContainsKey(machine) && machine.Ready)
            {
                var host = nativeMachines[machine];
                host.UIMessage.UIAttributesChanged(machine);
            }
        }

        internal int GetTicksPerBeat(MachineCore machine, IPattern pattern, int pp)
        {
            if (machine.EditorMachine != null)
            {
                var host = managedMachines[machine.EditorMachine];
                return host.GetTicksPerBeat(machine, pattern, pp);
            }
            return 4; // Buzz ticks per beat
        }

        internal void RemapMachineNames(MachineCore machine, Dictionary<string, string> dict)
        {
            if (machine != null && nativeMachines.ContainsKey(machine) && machine.Ready)
            {
                var host = nativeMachines[machine];
                host.UIMessage.UIRemapMachineNames(machine, dict);
            }
        }

        internal void InvalidateWaves()
        {
            foreach (var machine in nativeMachines)
            {
                machine.Key.invalidateWaves = true;
            }
        }

        internal void UpdateWaveReferences(MachineCore machine, MachineCore editorTargetMachine, Dictionary<int, int> remappedWaveReferences)
        {
            if (machine.DLL.IsManaged && managedMachines.ContainsKey(machine))
            {
                var machineHost = ManagedMachines[machine];
                machineHost.UpdateWaveReferences(machine, editorTargetMachine, remappedWaveReferences);
            }
            else if (nativeMachines.ContainsKey(machine) && machine.DLL.Info.Version >= BUZZ_MACHINE_INTERFACE_VERSION_15)
            {
                var machineHost = NativeMachines[machine];
                machineHost.UIMessage.UpdateWaveReferences(machine, editorTargetMachine, remappedWaveReferences);
            }
        }
    }
}
