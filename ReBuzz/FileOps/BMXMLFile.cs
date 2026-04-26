using BespokeFusion;
using BuzzGUI.Common;
using BuzzGUI.Common.DSP;
using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.Common.Settings;
using BuzzGUI.Interfaces;
using ReBuzz.Core;
using ReBuzz.Core.Actions.GraphActions;
using ReBuzz.MachineManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using static ReBuzz.FileOps.BMXFile;

namespace ReBuzz.FileOps
{
    internal class BMXMLFile : IReBuzzFile
    {
        private readonly ReBuzzCore buzz;
        private readonly string fileName;
        private Dictionary<string, SubSection> subSections = new Dictionary<string, SubSection>();
        readonly Dictionary<string, string> importDictionaryAll = new Dictionary<string, string>();
        readonly Dictionary<string, string> importDictionaryNonHidden = new Dictionary<string, string>();
        public event Action<FileEventType, string, object> FileEvent;
        private Dictionary<int, int> remappedWaveReferences = new Dictionary<int, int>();
        private ImportSongAction importAction;

        List<MachineCore> machines;
        private readonly string buzzPath;
        private readonly IUiDispatcher dispatcher;
        private readonly EngineSettings engineSettings;

        public BMXMLFile(ReBuzzCore buzz, string buzzPath, IUiDispatcher dispatcher, EngineSettings engineSettings)
        {
            this.buzzPath = buzzPath;
            this.buzz = buzz;
            machines = new List<MachineCore>();
            this.dispatcher = dispatcher;
            this.engineSettings = engineSettings;
        }

        void FileOpsEvent(FileEventType type, string text, object o = null)
        {
            buzz.DCWriteLine(text);
            FileEvent?.Invoke(type, text, o);
        }

        public void Load(string path, float x = 0, float y = 0, ImportSongAction importAction = null)
        {
            bool import = importAction != null;
            this.importAction = importAction;
            FileStream input = null;
            XmlReader r = null;
            Exception exc = null;
            try
            {
                input = new FileStream(path, FileMode.Open);
                FileOpsEvent(FileEventType.Open, path);
                if (input == null)
                {
                    EndFileOperation(false);
                    return;
                }
                var s = new XmlSerializer(typeof(BMXMLSong));

                r = XmlReader.Create(input);
                object o = null;

                o = s.Deserialize(r);
                var t = o as BMXMLSong;
                Load(t, x, y, import);
            }
            catch (Exception e)
            {
                exc = e;
            }
            finally
            {
                r?.Close();
                input?.Close();
                EndFileOperation(import);
            }

            if (exc != null)
            {
                throw exc;
            }
        }

        public void Load(BMXMLSong songData, float xOffset = 0, float yOffset = 0, bool import = false)
        {
            subSections.Clear();
            // Initiate and move data from BMXMLSong to ReBuzz

            // Build info
            FileOpsEvent(FileEventType.StatusUpdate, "Load File Info...");
            buzz.DCWriteLine(songData.BuildString);
            buzz.Speed = songData.Speed;

            // SubSections
            FileOpsEvent(FileEventType.StatusUpdate, "Load SubSections...");
            foreach (var subs in songData.SubSenctions)
            {
                subSections[subs.Name] = new SubSection() { Name = subs.Name, Data = subs.Data };
            }

            // Load Wavetable
            FileOpsEvent(FileEventType.StatusUpdate, "Load WaveTable...");
            foreach (var w in songData.Waves)
            {
                if (w.Index < 0 || w.Index >= 200)
                    continue;

                int newIndex = w.Index;
                while (buzz.SongCore.WavetableCore.WavesList[newIndex] != null && newIndex < 200)
                {
                    newIndex++;
                }

                // Can't fit imported waves to new slots
                if (newIndex >= 200)
                {
                    break;
                }

                remappedWaveReferences[w.Index] = newIndex;
                if (importAction != null)
                    importAction.AddWaveIndex(newIndex);
                WaveCore wave = buzz.SongCore.WavetableCore.CreateWave((ushort)newIndex);

                wave.FileName = w.FileName;
                wave.Name = w.Name;
                wave.Volume = w.Volume;
                wave.Flags = w.Flags;
                wave.Index = newIndex;

                var layers = w.Layers;
                if (layers != null)
                {
                    foreach (var layer in layers)
                    {
                        WaveLayerCore waveLayer = new WaveLayerCore();
                        waveLayer.extended = (layer.Format != WaveFormat.Int16);
                        waveLayer.WaveFormatData = (int)layer.Format;
                        waveLayer.ChannelCount = wave.Flags.HasFlag(WaveFlags.Stereo) ? 2 : 1;
                        waveLayer.SampleCount16Bit = waveLayer.ReverseAdjustSampleCount(layer.SampleCount);
                        waveLayer.LoopStart = layer.LoopStart;
                        waveLayer.LoopEnd = layer.LoopEnd;
                        waveLayer.SampleRate = layer.SampleRate;
                        waveLayer.RootNote = layer.RootNote;
                        waveLayer.Wave = wave;
                        wave.LayersList.Add(waveLayer);
                    }
                }
            }

            // Load Machines
            Dictionary<MachineCore, MachineInitData> dictInitData = new Dictionary<MachineCore, MachineInitData>();
            foreach (var machineData in songData.Machines)
            {
                FileOpsEvent(FileEventType.StatusUpdate, "Load Machines...");

                float x, y;
                int tracks = machineData.ParameterGroups[2].TrackCount;

                MachineDLL machineDLL = new MachineDLL();
                MachineCore machineProto = new MachineCore(buzz.SongCore, buzzPath, dispatcher, engineSettings);
                string name = XmlConvert.DecodeName(machineData.Name);
                if (Encoding.ASCII.GetBytes(name)[0] == 1)
                {
                    // Editor etc
                    machineProto.Hidden = true;
                }
                else
                {
                    FileOpsEvent(FileEventType.StatusUpdate, "Load Machine: " + name + "...");
                }

                string machineLibrary = machineData.Library;

                if (machineData.Type != MachineType.Master)
                {
                    if (buzz.MachineDLLs.ContainsKey(machineLibrary))
                    {
                        machineDLL = buzz.MachineDLLs[machineLibrary] as MachineDLL;
                        machineProto.MachineDLL = machineDLL;
                        machineProto.MachineDLL.MachineInfo.Type = machineData.Type;
                    }
                    else
                    {
                        machineDLL = new MachineDLL();
                        machineDLL.Name = machineLibrary;
                        machineDLL.IsMissing = true;
                        machineProto.MachineDLL = machineDLL;
                        machineProto.MachineDLL.MachineInfo.Type = machineData.Type;
                    }
                }
                else
                {
                    machineProto = buzz.SongCore.MachinesList.FirstOrDefault(m => m.DLL.Info.Type == MachineType.Master);
                }

                x = machineData.X; y = machineData.Y;

                if (import)
                {
                    x += xOffset;
                    y += yOffset;
                }
                byte[] data = machineData.Data;

                // If editor and missing, replace with default
                if (machineDLL.IsMissing && machineProto.Hidden)
                {
                    if (!buzz.Gear.HasSameDataFormat(machineLibrary, buzz.DefaultPatternEditor))
                    {
                        // Clear pattern editor data if not compatible with MPE
                        data = null;
                    }
                    machineDLL = (MachineDLL)buzz.MachineDLLs[buzz.DefaultPatternEditor];
                }

                // Create machine instances and replace dummies
                if (machineProto.DLL.Info.Type == MachineType.Master)
                {
                    if (!import)
                    {
                        // Update Position
                        machineProto.Position = new Tuple<float, float>(x, y);

                        // Update tpb & bpm immediately if machines read these during creation
                        var masterGlobals = machineProto.ParameterGroups[1];
                        buzz.MasterVolume = 1.0 - (masterGlobals.Parameters[0].GetValue(0) / (double)masterGlobals.Parameters[0].MaxValue);
                        buzz.BPM = masterGlobals.Parameters[1].GetValue(0);
                        buzz.TPB = masterGlobals.Parameters[2].GetValue(0);
                    }
                }
                else
                {
                    // Don't call Init for native machines yet. Wait until all machines are loaded and then call init. Control machines might need machine info.
                    var machineNew = buzz.MachineManager.CreateMachine(machineDLL.Name, machineDLL.Path, null, data, tracks, x, y, machineProto.Hidden, name, false);

                    // Saved machine parameter count/indexes might be a different from the machine that is currently available for ReBuzz. Create parameter mappings
                    RemapLoadedMachineParameterIndex(machineNew, machineProto);

                    if (!machineNew.DLL.IsManaged)
                    {
                        dictInitData[machineNew] = new MachineInitData() { data = data, tracks = tracks };
                    }

                    // Copy stuff from proto to real. Attributes can have same names?? Just iterate.
                    for (int i = 0; i < machineNew.AttributesList.Count; i++)
                    {
                        var ma = machineNew.AttributesList[i];
                        var at = i < machineData.Attributes.Count() ? machineData.Attributes[i] : null;
                        if (at != null)
                        {
                            ma.Value = at.Value;
                        }
                    }

                    if (machineNew.DLL.IsCrashed)
                    {
                        machineNew.ParameterGroupsList = new List<ParameterGroup>() { machineNew.ParameterGroupsList[0] };
                        AddGroup(machineData.ParameterGroups[1], machineNew, 1);
                        AddGroup(machineData.ParameterGroups[2], machineNew, 2);
                    }
                    else if (!machineNew.DLL.IsMissing)
                    {
                        // Copy parametervalues
                        CopyParameters(machineData.ParameterGroups[0], machineNew, 0, 0);
                        CopyParameters(machineData.ParameterGroups[1], machineNew, 1, 0);
                        for (int m = 0; m < tracks; m++)
                        {
                            CopyParameters(machineData.ParameterGroups[2], machineNew, 2, m);
                        }
                    }
                    else
                    {
                        AddGroup(machineData.ParameterGroups[1], machineNew, 1);
                        AddGroup(machineData.ParameterGroups[2], machineNew, 2);
                        machineNew.MachineDLL.MachineInfo.Type = machineData.Type;
                    }
                    if (import)
                    {
                        // Imported machines might get new names, so machines need to update their data
                        // Includes hidden machines.
                        importDictionaryAll[name] = machineNew.Name;

                        if (!machineNew.Hidden)
                        {
                            // Keep track of machines imported, so we can undo them
                            importAction.AddMachine(machineNew);

                            importDictionaryNonHidden[name] = machineNew.Name;
                        }
                    }

                    machines.Add(machineNew);
                }
            }

            bool useMultithreading = false;// Global.GeneralSettings.MultithreadSongLoading;
            // Native control machines need to have all machines "visible" before calling init
            InitMachines(dictInitData.Where(kv => !kv.Key.DLL.Info.Flags.HasFlag(MachineInfoFlags.CONTROL_MACHINE)), useMultithreading);
            InitMachines(dictInitData.Where(kv => kv.Key.DLL.Info.Flags.HasFlag(MachineInfoFlags.CONTROL_MACHINE)), useMultithreading);

            // Pattern Editor Connections
            foreach (var machineData in songData.Machines)
            {
                string editorName = GetImportedName(XmlConvert.DecodeName(machineData.EditorMachine));
                if (editorName != null && editorName !="")
                {
                    string machineName = GetImportedName(XmlConvert.DecodeName(machineData.Name));
                    var machine = buzz.SongCore.MachinesList.FirstOrDefault(m => m.Name == machineName);
                    var editorMachine = buzz.SongCore.MachinesList.FirstOrDefault(m => m.Name == editorName);

                    // Don't replace Master editor if importing song
                    if (machine != null && editorMachine != null)
                    {
                        if (import && machine.DLL.Info.Type == MachineType.Master)
                        {
                            buzz.RemoveMachine(editorMachine);
                        }
                        else
                        {
                            machine.EditorMachine = editorMachine;
                        }
                    }
                }
            }

            // Load connections
            FileOpsEvent(FileEventType.StatusUpdate, "Load Connections...");
            foreach (var cData in songData.MachineConnections)
            {
                MachineCore machineFrom = buzz.SongCore.MachinesList.FirstOrDefault(m => m.Name == GetImportedName(XmlConvert.DecodeName(cData.Source)));
                MachineCore machineTo = buzz.SongCore.MachinesList.FirstOrDefault(m => m.Name == GetImportedName(XmlConvert.DecodeName(cData.Destination)));
                if (machineFrom == null || machineTo == null)
                    continue;

                MachineConnectionCore connection = new MachineConnectionCore(dispatcher, engineSettings);
                connection.Amp = cData.Amp;
                connection.Pan = cData.Pan;
                connection.SourceChannel = cData.SourceChannel;
                connection.DestinationChannel = cData.DestinationChannel;
                connection.Source = machineFrom;
                connection.Destination = machineTo;
                connection.HasPan = machineTo.HasStereoInput;

                if (machineFrom.AllOutputs.FirstOrDefault(c => c.Destination == machineTo) == null)
                {
                    // Ensure missing machine has outputs
                    if (machineFrom.DLL.IsMissing && machineFrom.OutputChannelCount == 0)
                    {
                        machineFrom.OutputChannelCount = 1;
                    }
                    // Ensure missing machine has inputs
                    if (machineTo.DLL.IsMissing && machineTo.InputChannelCount == 0)
                    {
                        machineTo.InputChannelCount = 1;
                    }
                    new ConnectMachinesAction(buzz, connection, dispatcher, engineSettings).Do();
                }
            }

            // Load Patterns
            FileOpsEvent(FileEventType.StatusUpdate, "Load Patterns...");
            foreach (var machineData in songData.Machines)
            {
                string machineName = GetImportedName(XmlConvert.DecodeName(machineData.Name));
                var machine = buzz.SongCore.MachinesList.FirstOrDefault(c => c.Name == machineName);
                if (machine != null)
                {
                    foreach (var p in machineData.Patterns)
                    {
                        machine.CreatePattern(p.Name, p.Length);
                    }
                }
            }

            // Load Pattern Columns
            FileOpsEvent(FileEventType.StatusUpdate, "Load Patterns...");
            foreach (var machineData in songData.Machines)
            {
                string machineName = GetImportedName(XmlConvert.DecodeName(machineData.Name));
                var machine = buzz.SongCore.MachinesList.FirstOrDefault(c => c.Name == machineName);
                if (machine != null)
                {
                    foreach (var p in machineData.Patterns)
                    {
                        var pattern = machine.Patterns.FirstOrDefault(pat => pat.Name == p.Name);
                        if (!machine.DLL.IsMissing && pattern != null && p.Columns != null)
                        {
                            // Columns
                            int j = 0;
                            foreach (var c in p.Columns)
                            {
                                var targetMachine = buzz.SongCore.MachinesList.FirstOrDefault(m => m.Name == GetImportedName(XmlConvert.DecodeName(c.Machine)));
                                int group = c.Group;
                                int track = c.Track;
                                int indexInGroup = c.IndexInGroup;

                                IParameter targetParameter = targetMachine != null && (group != -1 && indexInGroup != -1) ? targetMachine.ParameterGroups[group].Parameters[indexInGroup] : ParameterCore.GetMidiParameter(targetMachine, dispatcher);
                                pattern.InsertColumn(j, targetParameter, track);
                                var column = pattern.Columns[j];

                                List<PatternEvent> events = new List<PatternEvent>();
                                foreach (var pe in c.Events)
                                {
                                    PatternEvent cpe = new PatternEvent()
                                    {
                                        Time = pe.Time,
                                        Value = pe.Value,
                                        Duration = pe.Duration
                                    };
                                    events.Add(cpe);
                                }
                                column.SetEvents(events, true);

                                Dictionary<string, string> md = new Dictionary<string, string>();
                                foreach (var meta in c.Metadata)
                                {
                                    column.Metadata[meta.Key] = meta.Value;
                                }

                                j++;
                            }
                        }
                    }
                }
            }

            // Load Sequences
            FileOpsEvent(FileEventType.StatusUpdate, "Load Sequences...");
            if (!import)
            {
                buzz.Song.LoopStart = songData.LoopStart;
                buzz.Song.LoopEnd = songData.LoopEnd;
                buzz.Song.SongEnd = songData.SongEnd;
            }
            int seqIndex = 0;
            foreach (var seq in songData.Sequences)
            {
                var machine = buzz.SongCore.Machines.FirstOrDefault(m => m.Name == GetImportedName(XmlConvert.DecodeName(seq.Machine)));
                if (machine != null)
                {
                    buzz.SongCore.AddSequence(machine, seqIndex);
                    var s = buzz.SongCore.Sequences[seqIndex];
                    foreach (var e in seq.Events)
                    {
                        var pat = machine.Patterns.FirstOrDefault(p => p.Name == e.Pattern);
                        s.SetEvent(e.Time, new SequenceEvent(e.Type, pat, e.Span));
                    }
                    s.IsDisabled = seq.IsDisabled;
                    seqIndex++;
                }
            }

            // Load waves and notify Machines / GUI
            FileOpsEvent(FileEventType.StatusUpdate, "Load Waves...");
            foreach (var w in songData.Waves)
            {
                if (w.Index < 0 || w.Index >= 200)
                    continue;

                int index = remappedWaveReferences[w.Index];
                WaveCore wave = buzz.SongCore.WavetableCore.WavesList[index];

                var layers = w.Layers;
                if (layers != null)
                {
                    for (int i = 0; i < layers.Length; i++)
                    {
                        var layer = layers[i];
                        WaveLayerCore waveLayer = wave.LayersList[i];

                        // Convert byte [] to float [] and copy to ReBuzz
                        waveLayer.Init(waveLayer.Path, waveLayer.Format, waveLayer.RootNote, waveLayer.ChannelCount == 2, waveLayer.SampleCount);

                        float[] floatBuffer = new float[waveLayer.SampleCount * waveLayer.ChannelCount];
                        Buffer.BlockCopy(layer.Samples, 0, floatBuffer, 0, waveLayer.SampleCount * waveLayer.ChannelCount * sizeof(float));
                        DSP.Scale(floatBuffer, 1 / 32768.0f);
                        if (waveLayer.ChannelCount == 1)
                        {
                            waveLayer.SetDataAsFloat(floatBuffer, 0, 1, 0, 0, waveLayer.SampleCount);
                        }
                        if (waveLayer.ChannelCount == 2)
                        {
                            waveLayer.SetDataAsFloat(floatBuffer, 0, 2, 0, 0, waveLayer.SampleCount);
                            waveLayer.SetDataAsFloat(floatBuffer, 1, 2, 1, 0, waveLayer.SampleCount);
                        }
                        wave.Invalidate();
                    }
                }
            }

            // Machine Properties
            FileOpsEvent(FileEventType.StatusUpdate, "Load Machine Properties...");
            foreach (var machineData in songData.Machines)
            {
                string machineName = GetImportedName(XmlConvert.DecodeName(machineData.Name));
                var machine = buzz.SongCore.MachinesList.FirstOrDefault(c => c.Name == machineName);
                if (machine != null)
                {
                    machine.IsMuted = machineData.IsMuted;
                    machine.IsBypassed = machineData.IsBypassed;
                    machine.MIDIInputChannel = machineData.MIDIInputChannel;
                    machine.OverrideLatency = machineData.OverrideLatency;
                    machine.OversampleFactor = machineData.OversampleFactor;
                    machine.IsWireless = machineData.IsWireless;
                    machine.BaseOctave = machineData.BaseOctave;
                }
            }

            FileOpsEvent(FileEventType.StatusUpdate, "Load Machine Windows...");
            foreach (var machineData in songData.Machines)
            {
                string machineName = GetImportedName(XmlConvert.DecodeName(machineData.Name));
                var machine = buzz.SongCore.MachinesList.FirstOrDefault(c => c.Name == machineName);
                if (machine != null)
                {
                    if (machineData.ParameterWindowVisible && machineData.ParameterWindow != null)
                    {
                        var pw = machineData.ParameterWindow;
                        machine.OpenParameterWindow(new Rect(pw.Left, pw.Top, pw.Right - pw.Left, pw.Bottom - pw.Top));
                    }

                    if (machineData.GUIWindowVisible && machineData.GUIWindow != null)
                    {
                        var gw = machineData.GUIWindow;
                        machine.OpenWindowedGUI(new Rect(gw.Left, gw.Top, gw.Right - gw.Left, gw.Bottom - gw.Top));
                    }
                }
            }

            var songCore = buzz.SongCore;
            // Machine Groups
            FileOpsEvent(FileEventType.StatusUpdate, "Load Machine Groups...");
            foreach (var machineGroup in songData.MachineGroup)
            {
                string name = GetImportedName(XmlConvert.DecodeName(machineGroup.Name));
                name =  songCore.GetNewGroupName(name);
                float x = machineGroup.X + xOffset;
                float y = machineGroup.Y + yOffset;
                bool isGrouped = machineGroup.IsGrouped;

                var group = buzz.CreateMachineGroup(name, x, y);
                name = machineGroup.MainInputMachineName;
                name = name != null && importDictionaryNonHidden.ContainsKey(name) ? importDictionaryNonHidden[name] : name;
                group.MainInputMachine = songCore.Machines.FirstOrDefault(m => m.Name == name);
                name = machineGroup.MainOutputMachineName;
                name = name != null && importDictionaryNonHidden.ContainsKey(name) ? importDictionaryNonHidden[name] : name;
                group.MainOutputMachine = songCore.Machines.FirstOrDefault(m => m.Name == name);
                group.IsGrouped = isGrouped;

                foreach (var mgm in machineGroup.Machines)
                {
                    name = mgm.Name;
                    name = importDictionaryNonHidden.ContainsKey(name) ? importDictionaryNonHidden[name] : name;
                    x = mgm.X + xOffset;
                    y = mgm.Y + yOffset;
                    var machine = songCore.Machines.FirstOrDefault(m => m.Name == name);
                    if (machine != null)
                    {
                        songCore.AddMachineToGroup(machine, group);
                        songCore.InvokeImportGroupedMachinePositions(machine, x, y);
                        songCore.GroupedMachinePositions.Add(machine, new Tuple<float, float>(x, y));
                    }
                }

                if (import)
                {
                    importAction.AddGroupMachine(group);
                }
            }

            // Info text
            buzz.InfoText = songData.InfoText;

            foreach (var machine in machines)
            {
                // Assign editor to pattern before calling UpdateWaveReferences
                if (machine.EditorMachine != null)
                {
                    buzz.MachineManager.SetPatternEditorPattern(machine.EditorMachine, machine.Patterns.FirstOrDefault());

                    // Notify machines that waveReferences have changed due to import
                    buzz.MachineManager.UpdateWaveReferences(machine.EditorMachine, machine, remappedWaveReferences);
                }
                else
                {
                    buzz.MachineManager.UpdateWaveReferences(machine, machine, remappedWaveReferences);
                }

                // Notify import finished and machine name changes
                buzz.MachineManager.ImportFinished(machine, importDictionaryNonHidden);
            }
        }

        internal void InitMachines(IEnumerable<KeyValuePair<MachineCore, MachineInitData>> dictInitData, bool multiThread)
        {
            bool askSkip = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
            List<Task> initTasks = new List<Task>();

            // Native control machines need to have all machines "visible" before calling init
            foreach (var kvMachine in dictInitData)
            {
                var machine = kvMachine.Key;

                FileOpsEvent(FileEventType.StatusUpdate, "Init Machine: " + machine.Name + "...");

                // Update machine names in ReBuzzEngine
                buzz.MachineManager.RemapMachineNames(machine, importDictionaryNonHidden);

                if (multiThread)
                {
                    var task = Task.Factory.StartNew(() =>
                    {
                        var val = kvMachine.Value;
                        buzz.MachineManager.CallInit(machine, val.data, val.tracks, askSkip);
                    });

                    initTasks.Add(task);
                }
                else
                {
                    var val = kvMachine.Value;
                    buzz.MachineManager.CallInit(machine, val.data, val.tracks, askSkip);
                }
            }

            // Wait max xx seconds
            while (!Task.WaitAll(initTasks.ToArray(), (int)Global.GeneralSettings.SongLoadWait * 1000))
            {
                List<MachineCore> badMachinesList = new List<MachineCore>();
                string machinesMessage = "";
                foreach (var t in initTasks)
                {
                    if (!t.IsCompleted)
                    {
                        int index = initTasks.IndexOf(t);
                        var badMachine = dictInitData.ElementAt(index).Key;
                        badMachinesList.Add(badMachine);
                        machinesMessage += badMachine.Name + " (" + badMachine.DLL.Name + ")\n";
                    }
                }

                if (MessageBoxWindow.ShowYesNoWindow("Wait more?", "Loading machines below is taking a long time. Do you want to wait more?\n\n" + machinesMessage) == MessageBoxResult.No)
                {
                    // Init on some machine was not completed
                    foreach (var badMachine in badMachinesList)
                    {
                        badMachine.MachineDLL.IsCrashed = true;
                        badMachine.Ready = false;
                        string error = "Init call failed for machine: " + badMachine.Name;
                        buzz.DCWriteLine(error, DCLogLevel.Error);

                        buzz.MachineManager.DeleteMachine(badMachine);
                    }
                    break;
                }
            }
        }

        string GetImportedName(string name)
        {
            if (name == null)
                return null;
            else if (importDictionaryAll.ContainsKey(name))
                return importDictionaryAll[name];
            else return name;
        }

        private void CopyParameters(BMXMLParameterGroup pgFrom, MachineCore machineNew, int group, int track)
        {
            var pgTo = machineNew.ParameterGroupsList[group];
            for (int i = 0; i < pgTo.ParametersList.Count; i++)
            {
                var pTo = pgTo.ParametersList[i];
                var pFrom = i < pgFrom.Parameters.Count() ? pgFrom.Parameters[i] : null;
                if (pFrom != null)
                {
                    var val = pFrom.Values.FirstOrDefault(t => t.Track == track);
                    if (val != null)
                    {
                        pTo.SetValue(track, val.Value);
                    }
                }
            }
        }

        private void AddGroup(BMXMLParameterGroup pgFrom, MachineCore machineNew, int group)
        {
            ParameterGroupType type = ParameterGroupType.Global;
            if (group == 2)
                type = ParameterGroupType.Track;

            ParameterGroup pgTo = new ParameterGroup(machineNew, type);
            int index = 0;
            foreach (var pFrom in pgFrom.Parameters)
            {
                ParameterCore pTo = new ParameterCore(dispatcher);
                pTo.Group = pgTo;
                pTo.IndexInGroup = index;
                pTo.Name = XmlConvert.DecodeName(pFrom.Name);
                pTo.Description = XmlConvert.DecodeName(pFrom.Description);
                pTo.MinValue = pFrom.MinValue;
                pTo.MaxValue = pFrom.MaxValue;
                pTo.DefValue = pFrom.DefValue;
                pTo.NoValue = pFrom.NoValue;
                pTo.Description = pFrom.Description;
                pTo.Type = pFrom.Type;
                pTo.Flags = pFrom.Flags;

                pgTo.AddParameter(pTo);
            }

            machineNew.ParameterGroupsList.Add(pgTo);
        }

        private void RemapLoadedMachineParameterIndex(MachineCore machine, MachineCore savedMachine)
        {
            Dictionary<int, int> paramMappings = new Dictionary<int, int>();

            var machineParameters = machine.AllParameters().ToArray();
            var savedParameters = savedMachine.AllParameters().ToArray();

            for (int i = 0; i < savedParameters.Length; i++)
            {
                bool found = false;
                for (int j = 0; j < machineParameters.Length; j++)
                {
                    if (savedParameters[i].Name == machineParameters[j].Name)
                    {
                        paramMappings[i] = j;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    // Machine loaded into ReBuzz does not have the same param
                    paramMappings[i] = -1;
                }
            }

            machine.remappedLoadedMachineParameterIndexes = paramMappings;
        }

        public void Save(string path, object obj)
        {
            FileStream output = null;
            XmlWriter w = null;
            Exception exc = null;
            try
            {
                output = new FileStream(path, FileMode.Create);
                var ws = new XmlWriterSettings() { NamespaceHandling = System.Xml.NamespaceHandling.OmitDuplicates, NewLineOnAttributes = false, Indent = true };
                w = XmlWriter.Create(output, ws);

                var s = new XmlSerializer(obj.GetType());
                s.Serialize(w, obj);
            }
            catch (Exception e)
            {
                exc = e;
            }
            finally
            {
                w?.Close();
                output?.Close();
            }

            if (exc != null)
            {
                throw exc;
            }
        }


        public void Save(string path)
        {
            // Create data structure
            BMXMLSong file = new BMXMLSong();

            // Build info
            file.BuildString = buzz.BuildString;
            file.Name = Path.GetFileNameWithoutExtension(path);
            file.Build = buzz.BuildNumber;
            file.LoopStart = buzz.Song.LoopStart;
            file.LoopEnd = buzz.Song.LoopEnd;
            file.SongEnd = buzz.Song.SongEnd;
            file.Speed = buzz.Speed;

            // Machines
            List<BMXMLMachine> machines = new List<BMXMLMachine>();
            foreach (var m in buzz.SongCore.MachinesList)
            {
                BMXMLMachine bmxm = new BMXMLMachine();
                bmxm.Name = XmlConvert.EncodeName(m.Name);
                bmxm.Library = m.DLL.Name;
                bmxm.Data = m.Data;
                bmxm.Type = m.DLL.Info.Type;
                bmxm.X = m.Position.Item1;
                bmxm.Y = m.Position.Item2;

                bmxm.IsMuted = m.IsMuted;
                bmxm.IsBypassed = m.IsBypassed;
                bmxm.MIDIInputChannel = m.MIDIInputChannel;
                bmxm.OverrideLatency = m.OverrideLatency;
                bmxm.OversampleFactor = m.OversampleFactor;
                bmxm.IsWireless = m.IsWireless;
                bmxm.BaseOctave = m.BaseOctave;

                if (m.EditorMachine != null)
                {
                    bmxm.EditorMachine = XmlConvert.EncodeName(m.EditorMachine.Name);
                }

                List<BMXMLParameterGroup> pgs = new List<BMXMLParameterGroup>();
                // Groups
                foreach (var g in m.ParameterGroupsList)
                {
                    BMXMLParameterGroup pg = new BMXMLParameterGroup();
                    pg.Type = g.Type;
                    List<BMXMLParameter> parameters = new List<BMXMLParameter>();

                    // Parameters
                    foreach (var p in g.Parameters)
                    {
                        BMXMLParameter ppg = new BMXMLParameter();
                        ppg.Type = p.Type;
                        ppg.Name = XmlConvert.EncodeName(p.Name);
                        ppg.Description = XmlConvert.EncodeName(p.Description);
                        ppg.MinValue = p.MinValue;
                        ppg.MaxValue = p.MaxValue;
                        ppg.NoValue = p.NoValue;
                        ppg.DefValue = p.DefValue;
                        ppg.Flags = p.Flags;

                        // Values
                        List<BMXMLParameterValue> values = new List<BMXMLParameterValue>();
                        for (int i = 0; i < g.TrackCount; i++)
                        {
                            values.Add(new BMXMLParameterValue()
                            {
                                Track = i,
                                Value = p.GetValue(i)
                            });
                        }
                        ppg.Values = values.ToArray();

                        parameters.Add(ppg);
                    }
                    pg.Parameters = parameters.ToArray();
                    pg.TrackCount = g.TrackCount;

                    pgs.Add(pg);
                }

                bmxm.ParameterGroups = pgs.ToArray();

                // Attributes
                List<BMXMLAttribute> attributes = new List<BMXMLAttribute>();
                foreach (var attribute in m.AttributesList)
                {
                    BMXMLAttribute a = new BMXMLAttribute();
                    a.Name = attribute.Name;
                    a.Value = attribute.Value;
                    attributes.Add(a);
                }
                bmxm.Attributes = attributes.ToArray();

                // Patterns
                List<BMXMLPattern> patterns = new List<BMXMLPattern>();

                foreach (var p in m.PatternsList)
                {
                    BMXMLPattern bmxmPattern = new BMXMLPattern();
                    bmxmPattern.Name = p.Name;
                    bmxmPattern.Length = p.Length;

                    List<BMXMLPatternColumn> columns = new List<BMXMLPatternColumn>();
                    // Columns
                    foreach (var column in p.Columns)
                    {
                        var param = column.Parameter as ParameterCore;
                        BMXMLPatternColumn bPatternColumn = new BMXMLPatternColumn();

                        var targetMachine = column.Parameter.Group != null ? column.Parameter.Group.Machine as MachineCore : null;
                        if (targetMachine == null && param.Machine != null)
                        {
                            targetMachine = param.Machine;
                        }

                        bPatternColumn.Machine = targetMachine != null ? XmlConvert.EncodeName(targetMachine.Name) : "";
                        bPatternColumn.Group = m.ParameterGroups.IndexOf(param.Group);
                        bPatternColumn.IndexInGroup = param.IndexInGroup;
                        bPatternColumn.Track = column.Track;

                        List<BMXMLColumnEvent> events = new List<BMXMLColumnEvent>();
                        foreach (var pe in column.GetEvents(0, int.MaxValue))
                        {
                            BMXMLColumnEvent mce = new BMXMLColumnEvent()
                            {
                                Time = pe.Time,
                                Value = pe.Value,
                                Duration = pe.Duration
                            };
                            events.Add(mce);
                        }
                        bPatternColumn.Events = events.ToArray();

                        List<BMXMLMetadata> md = new List<BMXMLMetadata>();
                        foreach (var meta in column.Metadata)
                        {
                            BMXMLMetadata bmd = new BMXMLMetadata()
                            {
                                Key = meta.Key,
                                Value = meta.Value
                            };
                            md.Add(bmd);
                        }
                        bPatternColumn.Metadata = md.ToArray();

                        columns.Add(bPatternColumn);
                    }
                    bmxmPattern.Columns = columns.ToArray();

                    patterns.Add(bmxmPattern);
                }

                bmxm.Patterns = patterns.ToArray();

                // Machine Windows
                var pw = m.ParameterWindow;
                var mGUI = m.MachineGUIWindow;

                bmxm.ParameterWindowVisible = pw != null && pw.Visibility == System.Windows.Visibility.Visible;
                bmxm.GUIWindowVisible = mGUI != null && mGUI.Visibility == System.Windows.Visibility.Visible;

                if (bmxm.ParameterWindowVisible)
                {
                    BMXMLMachineWindow w = new BMXMLMachineWindow();
                    w.Left = pw.Left;
                    w.Top = pw.Top;
                    w.Right = pw.Left + pw.ActualWidth;
                    w.Bottom = pw.Top + pw.ActualHeight;
                    bmxm.ParameterWindow = w;
                }

                if (bmxm.GUIWindowVisible)
                {
                    BMXMLMachineWindow w = new BMXMLMachineWindow();
                    w.Left = mGUI.Left;
                    w.Top = mGUI.Top;
                    w.Right = mGUI.Left + mGUI.ActualWidth;
                    w.Bottom = mGUI.Top + mGUI.ActualHeight;
                    bmxm.GUIWindow = w;
                }

                machines.Add(bmxm);
            }

            file.Machines = machines.ToArray();

            // Sequences
            List<BMXMLSequence> sequences = new List<BMXMLSequence>();
            foreach (var s in buzz.SongCore.Sequences)
            {
                BMXMLSequence bs = new BMXMLSequence();
                bs.Machine = XmlConvert.EncodeName(s.Machine.Name);

                // Events
                List<BMXMLSequenceEvent> events = new List<BMXMLSequenceEvent>();
                foreach (var e in s.Events)
                {
                    BMXMLSequenceEvent se = new BMXMLSequenceEvent();
                    se.Time = e.Key;
                    se.Span = e.Value.Span;
                    se.Type = e.Value.Type;
                    se.Pattern = e.Value.Pattern?.Name;
                    events.Add(se);
                }
                bs.Events = events.ToArray();

                bs.IsDisabled = s.IsDisabled;
                sequences.Add(bs);
            }

            file.Sequences = sequences.ToArray();

            // Wavetable
            List<BMXMLWave> waves = new List<BMXMLWave>();
            foreach (var w in buzz.SongCore.WavetableCore.WavesList)
            {
                if (w != null)
                {
                    BMXMLWave wave = new BMXMLWave();
                    wave.Name = w.Name;
                    wave.FileName = w.FileName;
                    wave.Volume = w.Volume;
                    wave.Index = w.Index;
                    wave.Flags = w.Flags;

                    // Layers
                    List<BMXMLWaveLayer> bMXMLWaveLayers = new List<BMXMLWaveLayer>();
                    foreach (var l in w.Layers)
                    {
                        BMXMLWaveLayer layer = new BMXMLWaveLayer();
                        layer.Path = l.Path;
                        layer.RootNote = l.RootNote;
                        layer.LoopStart = l.LoopStart;
                        layer.LoopEnd = l.LoopEnd;
                        layer.ChannelCount = l.ChannelCount;
                        layer.Format = l.Format;
                        layer.SampleCount = l.SampleCount;
                        layer.SampleRate = l.SampleRate;
                        layer.Samples = new byte[l.SampleCount * l.ChannelCount * sizeof(float)];

                        int totalSamplesLeft = l.SampleCount;
                        int readOffset = 0;
                        while (totalSamplesLeft > 0)
                        {
                            int samplesToRead = Math.Min(totalSamplesLeft, 1000);
                            float[] readBuffer = new float[samplesToRead * l.ChannelCount];
                            l.GetDataAsFloat(readBuffer, 0, l.ChannelCount, 0, readOffset, samplesToRead);
                            if (l.ChannelCount == 2)
                            {
                                l.GetDataAsFloat(readBuffer, 1, l.ChannelCount, 1, readOffset, samplesToRead);
                            }

                            DSP.Scale(readBuffer, 32768.0f);
                            Buffer.BlockCopy(readBuffer, 0, layer.Samples, readOffset * l.ChannelCount * sizeof(float), samplesToRead * l.ChannelCount * sizeof(float));

                            totalSamplesLeft -= samplesToRead;
                            readOffset += samplesToRead;
                        }

                        bMXMLWaveLayers.Add(layer);
                    }

                    wave.Layers = bMXMLWaveLayers.ToArray();
                    waves.Add(wave);
                }
            }

            Dictionary<MachineConnectionCore, bool> connections = new Dictionary<MachineConnectionCore, bool>();
            foreach (var machine in buzz.SongCore.MachinesList)
            {
                foreach (var input in machine.AllInputs)
                {
                    connections[input as MachineConnectionCore] = true;
                }
                foreach (var output in machine.AllOutputs)
                {
                    connections[output as MachineConnectionCore] = true;
                }
            }

            List<BMXMLMachineConnection> bMXMLMachineConnections = new List<BMXMLMachineConnection>();
            foreach (var connection in connections.Keys)
            {
                var c = new BMXMLMachineConnection();
                c.Source = XmlConvert.EncodeName(connection.Source.Name);
                c.Destination = XmlConvert.EncodeName(connection.Destination.Name);
                c.Amp = connection.Amp;
                c.Pan = connection.Pan;
                c.SourceChannel = connection.SourceChannel;
                c.DestinationChannel = connection.DestinationChannel;

                bMXMLMachineConnections.Add(c);
            }

            file.MachineConnections = bMXMLMachineConnections.ToArray();

            
            List<BMXMLMachineGroup> bMXMLMachineGroups = new List<BMXMLMachineGroup>();
            // Machine Groups
            foreach (var machineGroup in buzz.SongCore.MachineGroups)
            {
                BMXMLMachineGroup group = new BMXMLMachineGroup();
                group.Name = machineGroup.Name;
                group.X = machineGroup.Position.Item1;
                group.Y = machineGroup.Position.Item2;
                group.IsGrouped = machineGroup.IsGrouped;
                group.MainInputMachineName = machineGroup.MainInputMachine != null ? machineGroup.MainInputMachine.Name : null;
                group.MainOutputMachineName = machineGroup.MainOutputMachine != null ? machineGroup.MainOutputMachine.Name : null;

                List<BMXMLMachineGroupMachine> mgms = new List<BMXMLMachineGroupMachine>();

                foreach (var mgmbuzz in buzz.SongCore.MachineToGroupDict.Where(kv => kv.Value == machineGroup))
                {
                    BMXMLMachineGroupMachine mgm = new BMXMLMachineGroupMachine();
                    var machine = mgmbuzz.Key;
                    mgm.Name = machine.Name;

                    Tuple<float, float> pos = machine.Position;
                    if (buzz.SongCore.GroupedMachinePositions.ContainsKey(machine))
                    {
                        pos = buzz.SongCore.GroupedMachinePositions[machine];
                    }

                    mgm.X = pos.Item1;
                    mgm.Y = pos.Item2;
                    mgms.Add(mgm);
                }
                group.Machines = mgms.ToArray();
                bMXMLMachineGroups.Add(group);
            }

            file.MachineGroup = bMXMLMachineGroups.ToArray();

            // SubSections
            List<BMXMLSubSection> bMXMLSubSections = new List<BMXMLSubSection>();
            foreach (var val in subSections.Values)
            {
                bMXMLSubSections.Add(new BMXMLSubSection()
                {
                    Name = val.Name,
                    Data = val.Data
                });
            }
            file.SubSenctions = bMXMLSubSections.ToArray();

            file.Waves = waves.ToArray();

            // Info Text
            file.InfoText = buzz.InfoText == null ? "" : buzz.InfoText;

            Save(path, file);
        }

        public void SetSubSections(SaveSongCore ss)
        {
            subSections = new Dictionary<string, SubSection>();
            foreach (var stream in ss.GetStreamsDict())
            {
                SubSection sect = new SubSection();
                sect.Name = stream.Key;
                sect.Data = stream.Value.ToArray();
                subSections[stream.Key] = sect;
            }
        }

        public Dictionary<string, MemoryStream> GetSubSections()
        {
            return subSections.Values.Select(s => new { s.Name, Value = new MemoryStream(s.Data, false) }).ToDictionary(s => s.Name, s => s.Value);
        }

        public void EndFileOperation(bool import)
        {
            IEnumerable<MachineCore> laodedMachines = null;

            if (import)
            {
                laodedMachines = machines.Where(m => !m.Hidden && m.DLL.Info.Type != MachineType.Master);
            }

            subSections.Clear();
            FileOpsEvent(FileEventType.Close, fileName, laodedMachines);
        }
    }

    // XML import/export structures]
    [XmlRoot(ElementName = "ReBuzzSong")]
    public class BMXMLSong
    {
        [XmlAttribute]
        public string Name { get; set; }
        [XmlAttribute]
        public int Build { get; set; }
        [XmlAttribute]
        public string BuildString { get; set; }

        public BMXMLMachine[] Machines { get; set; }

        public BMXMLMachineGroup[] MachineGroup { get; set; }

        public BMXMLSequence[] Sequences { get; set; }
        public BMXMLWave[] Waves { get; set; }

        public BMXMLSubSection[] SubSenctions;

        public BMXMLMachineConnection[] MachineConnections { get; set; }
        public string InfoText { get; set; }
        public int LoopStart { get; set; }
        public int LoopEnd { get; set; }
        public int SongEnd { get; set; }
        public int Speed { get; set; }

    }

    [XmlType(TypeName = "Machine")]
    public class BMXMLMachine
    {
        public string Library { get; set; }
        public byte[] Data { get; set; }
        public string EditorMachine { get; set; }
        public string Name { get; set; }
        public BMXMLPattern[] Patterns { get; set; }
        public BMXMLParameterGroup[] ParameterGroups { get; set; }

        public BMXMLAttribute[] Attributes { get; set; }
        public MachineType Type { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public bool IsMuted { get; set; }
        public int MIDIInputChannel { get; set; }
        public int OverrideLatency { get; set; }
        public int OversampleFactor { get; set; }
        public bool IsWireless { get; set; }
        public int BaseOctave { get; set; }
        public bool ParameterWindowVisible { get; set; }
        public bool GUIWindowVisible { get; set; }
        public BMXMLMachineWindow GUIWindow { get; set; }
        public BMXMLMachineWindow ParameterWindow { get; set; }
        public bool IsBypassed { get; set; }
    }

    [XmlType(TypeName = "MachineGroup")]
    public class BMXMLMachineGroup
    {
        public string Name { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public bool IsGrouped { get; set; }
        public string MainInputMachineName { get; set; }
        public string MainOutputMachineName { get; set; }

        public BMXMLMachineGroupMachine[] Machines { get; set; }
    }

    [XmlType(TypeName = "MachineGroupMachine")]
    public class BMXMLMachineGroupMachine
    {
        public string Name { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
    }

    [XmlType(TypeName = "ParameterGroup")]
    public class BMXMLParameterGroup
    {
        public ParameterGroupType Type { get; set; }
        public BMXMLParameter[] Parameters { get; set; }
        public int TrackCount { get; set; }
    }

    [XmlType(TypeName = "Parameter")]
    public class BMXMLParameter
    {
        public ParameterType Type { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public int MinValue { get; set; }

        public int MaxValue { get; set; }

        public int NoValue { get; set; }

        public ParameterFlags Flags { get; set; }

        public int DefValue { get; set; }

        public BMXMLParameterValue[] Values;
    }

    [XmlType(TypeName = "Value")]
    public class BMXMLParameterValue
    {
        public int Track { get; set; }
        public int Value { get; set; }
    }

    [XmlType(TypeName = "Sequence")]
    public class BMXMLSequence
    {
        public BMXMLSequenceEvent[] Events { get; set; }
        public string Machine { get; set; }
        public bool IsDisabled { get; set; }
    }

    [XmlType(TypeName = "Event")]
    public class BMXMLSequenceEvent
    {
        public int Time { get; set; }
        public SequenceEventType Type { get; set; }
        public int Span { get; set; }
        public string Pattern { get; set; }
    }

    [XmlType(TypeName = "Info")]
    public class BMXMLMachineInfo
    {
        [XmlAttribute]
        public MachineType Type { get; set; }
        [XmlAttribute]
        public int Version { get; set; }
        [XmlAttribute]
        public int InternalVersion { get; set; }
        [XmlAttribute]
        public MachineInfoFlags Flags { get; set; }
        [XmlAttribute]
        public int MinTracks { get; set; }
        [XmlAttribute]
        public int MaxTracks { get; set; }
        [XmlAttribute]
        public string Name { get; set; }
        [XmlAttribute]
        public string ShortName { get; set; }
        [XmlAttribute]
        public string Author { get; set; }
    }

    [XmlType(TypeName = "Pattern")]
    public class BMXMLPattern
    {
        public string Name { get; set; }
        public int Length { get; set; }
        public BMXMLPatternColumn[] Columns { get; set; }
    }

    [XmlType(TypeName = "Wave")]
    public class BMXMLWave
    {
        public string Name { get; set; }
        public int Index { get; set; }
        public BMXMLWaveLayer[] Layers { get; set; }
        public float Volume { get; set; }
        public WaveFlags Flags { get; set; }
        public string FileName { get; set; }
    }

    [XmlType(TypeName = "Layer")]
    public class BMXMLWaveLayer
    {
        public string Path { get; set; }
        public int RootNote { get; set; }
        public int LoopStart { get; set; }
        public int LoopEnd { get; set; }
        public int ChannelCount { get; set; }
        public WaveFormat Format { get; set; }
        public int SampleCount { get; set; }
        public int SampleRate { get; set; }
        public byte[] Samples { get; set; }
    }

    [XmlType(TypeName = "SubSection")]
    public class BMXMLSubSection
    {
        public string Name { get; set; }
        public byte[] Data { get; set; }
    }

    [XmlType(TypeName = "MachineConnection")]
    public class BMXMLMachineConnection
    {
        public string Source { get; set; }
        public string Destination { get; set; }
        public int Amp { get; set; }
        public int Pan { get; set; }
        public int SourceChannel { get; set; }
        public int DestinationChannel { get; set; }
    }

    [XmlType(TypeName = "Attribute")]
    public class BMXMLAttribute
    {
        public string Name { get; set; }
        public int Value { get; set; }
        /*
        public int MinValue { get; set; }
        public int MaxValue { get; set; }
        public int DefValue { get; set; }

        public bool HasUserDefValue { get; set; }
        public int UserDefValue { get; set; }
        public bool UserDefValueOverridesPreset { get; set; }
        */
    }

    [XmlType(TypeName = "PatternColumn")]
    public class BMXMLPatternColumn
    {
        public string Machine { get; set; }
        public int Group { get; set; }
        public int IndexInGroup { get; set; }
        public int Track { get; set; }
        public BMXMLColumnEvent[] Events { get; set; }
        public BMXMLMetadata[] Metadata { get; set; }
    }

    [XmlType(TypeName = "ColumnEvent")]
    public class BMXMLColumnEvent
    {
        public int Time { get; set; }
        public int Value { get; set; }
        public int Duration { get; set; }
    }

    [XmlType(TypeName = "Metadata")]
    public class BMXMLMetadata
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

    [XmlType(TypeName = "Window")]
    public class BMXMLMachineWindow
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Right { get; set; }
        public double Bottom { get; set; }
    }
}
