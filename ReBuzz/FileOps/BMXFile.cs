using BuzzGUI.Common;
using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.Common.Templates;
using BuzzGUI.Interfaces;
using ReBuzz.Core;
using ReBuzz.Core.Actions.GraphActions;
using ReBuzz.MachineManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Windows.Controls;

namespace ReBuzz.FileOps
{
    public enum FileEventType
    {
        Open,
        StatusUpdate,
        Close
    }

    internal class BMXFile : IReBuzzFile
    {
        readonly int WaveFlagsEnvelope = 0x80;
        public class Section
        {
            public uint Magic;
            public uint Offset;
            public uint Size;
            public byte[] Data;
        }

        public class SubSection
        {
            public string Name;
            public byte[] Data;
        }

        FileStream fs;

        Dictionary<SectionType, Section> sections;
        Dictionary<string, SubSection> subSections;
        readonly List<MachineConnectionCore> connections;
        readonly Dictionary<string, string> importDictionary = new Dictionary<string, string>();
        readonly Dictionary<string, bool> builtInPeDictionary = new Dictionary<string, bool>();

        public event Action<FileEventType, string, object> FileEvent;

        private readonly ReBuzzCore buzz;
        List<MachineCore> machines;
        private ImportSongAction importAction;
        private int masterInputCount;
        private Dictionary<int,int> remappedWaveReferences;
        private readonly List<SequenceCore> bmxSequences;
        private readonly string buzzPath;
        private readonly IUiDispatcher dispatcher;

        enum SectionType
        {
            Buzz = 0x7a7a7542, // Ok
            MACH = 0x4843414d, // Ok
            CONN = 0x4E4E4F43, // Ok
            PATT = 0x54544150, // Ok
            SEQU = 0x55514553, // Ok
            WAVT = 0x54564157, // Ok
            CWAV = 0x56415743,
            WAVE = 0x45564157,
            BLAH = 0x48414c42, // Info text
            PARA = 0x41524150, // Ok
            MIDI = 0x4944494D,
            CON2 = 0x324E4F43,
            REVB = 0x52455642, // Buzz Build info
            GLDP = 0x474C4450, // Dialog states?
            XNOC = 0x584E4F43, // MultiIO Connections
            XCAM = 0x5843414D, // Machine properties
            XTAP = 0x58544150, // Link pattern to editor machine
            TAP2 = 0x32544150, // Experimental pattern editor data. Dropped?
            IUGB = 0x49554742, // Ok, SubSections
            RBSG = 0x52425347,  // ReBuzz Song Settings
            XQES = 0x58514553  // Sequence properties
        }

        public BMXFile(ReBuzzCore buzz, string buzzPath, IUiDispatcher dispatcher)
        {
            this.buzz = buzz;
            machines = new List<MachineCore>();
            connections = new List<MachineConnectionCore>();
            bmxSequences = new List<SequenceCore>();
            remappedWaveReferences = new Dictionary<int, int>();
            this.buzzPath = buzzPath;
            this.dispatcher = dispatcher;
        }

        public void Load(string path, float x = 0, float y = 0, ImportSongAction importAction = null)
        {
            bool import = importAction != null;
            this.importAction = importAction;
            masterInputCount = 0;
            //lock (ReBuzzCore.AudioLock)
            {
                Open(path, FileMode.Open);

                LoadSections();
                LoadFileInfo();
                LoadPara();
                LoadSubSections();
                LoadWaveTable();
                LoadMachines(x, y, import);
                LoadConnections();
                LoadXNOC();
                LoadXTAP(import);
                LoadPatterns();
                LoadSequences(import);
                LoadXCAM();
                LoadWaves();
                LoadInfoText();
                LoadTAP2();
                if (!import)
                    LoadReBuzzSongSettings();
                LoadDialogProperties();
                LoadXQES();

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
                    buzz.MachineManager.ImportFinished(machine, importDictionary);
                }

                EndFileOperation(import);
            }
        }

        void FileOpsEvent(FileEventType type, string text, object o = null)
        {
            buzz.DCWriteLine(text);
            FileEvent?.Invoke(type, text, o);
        }

        public void Open(string path, FileMode mode)
        {
            fs = new FileStream(path, mode, mode == FileMode.Create ? FileAccess.Write : FileAccess.Read, mode == FileMode.Create ? FileShare.Write : FileShare.Read);
            FileOpsEvent(FileEventType.Open, path);
        }

        public void EndFileOperation(bool import)
        {
            IEnumerable<MachineCore> loadedMachines = null;

            if (import)
            {
                loadedMachines = machines.Where(m => !m.Hidden && m.DLL.Info.Type != MachineType.Master);
            }
            FileOpsEvent(FileEventType.Close, fs.Name, loadedMachines);

            bmxSequences.Clear();
            machines.Clear();
            connections.Clear();

            fs?.Close();
        }

        public void LoadSections()
        {
            sections = new Dictionary<SectionType, Section>();

            FileOpsEvent(FileEventType.StatusUpdate, "Load Sections...");

            uint magic = ReadUInt(fs);
            uint sectionCount = ReadUInt(fs);

            if (magic != (uint)SectionType.Buzz)
            {
                throw new Exception("Not a valid Buzz file");
            }

            for (int i = 0; i < sectionCount; i++)
            {
                Section section = new Section();
                section.Magic = ReadUInt(fs);
                section.Offset = ReadUInt(fs);
                section.Size = ReadUInt(fs);
                sections[(SectionType)section.Magic] = section;
            }
        }

        private void LoadFileInfo()
        {
            Section section;
            if (sections.TryGetValue(SectionType.REVB, out section))
            {
                FileOpsEvent(FileEventType.StatusUpdate, "Load File Info...");

                fs.Position = section.Offset;
                var fileInfo = ReadString(fs);
                buzz.DCWriteLine(fileInfo);
            }
        }

        private void LoadInfoText()
        {
            Section section;
            if (sections.TryGetValue(SectionType.BLAH, out section))
            {
                FileOpsEvent(FileEventType.StatusUpdate, "Load Info Text...");

                fs.Position = section.Offset;
                uint lenght = ReadUInt(fs);
                byte[] buffer = new byte[lenght];

                for (int i = 0; i < lenght; i++)
                {
                    buffer[i] = (byte)ReadVal(fs, 1);
                }

                string s = Encoding.UTF8.GetString(buffer, 0, buffer.Length);
                buzz.InfoText = s;
            }
        }

        public void LoadPara()
        {
            Section section;
            if (sections.TryGetValue(SectionType.PARA, out section))
            {
                FileOpsEvent(FileEventType.StatusUpdate, "Load Parameters...");

                fs.Position = section.Offset;

                uint numMachines = ReadUInt(fs);
                for (int i = 0; i < numMachines; i++)
                {
                    string machineName = ReadString(fs);
                    string dllName = ReadString(fs);
                    uint numGlobals = ReadUInt(fs);
                    uint numTrackParams = ReadUInt(fs);

                    MachineCore machine = GetMachine(machineName);
                    machine.MachineDLL.Name = dllName;

                    FileOpsEvent(FileEventType.StatusUpdate, "Load Parameters: " + machineName + "...");

                    ParameterGroup groupGlobal = new ParameterGroup(machine, ParameterGroupType.Global);
                    ParameterGroup groupTrack = new ParameterGroup(machine, ParameterGroupType.Track);

                    for (int j = 0; j < numGlobals + numTrackParams; j++)
                    {
                        ParameterCore parameter = new ParameterCore(dispatcher);
                        parameter.Type = (ParameterType)ReadByte(fs);
                        parameter.Name = ReadString(fs);
                        parameter.MinValue = ReadInt(fs);
                        parameter.MaxValue = ReadInt(fs);
                        parameter.NoValue = ReadInt(fs);
                        parameter.Flags = (ParameterFlags)ReadInt(fs);
                        parameter.DefValue = ReadInt(fs);

                        if (j < numGlobals)
                        {
                            groupGlobal.AddParameter(parameter);
                        }
                        else
                        {
                            groupTrack.AddParameter(parameter);
                        }
                    }

                    machine.AddParameterGroup(groupGlobal);
                    machine.AddParameterGroup(groupTrack);
                }
            }
        }

        private void LoadMachines(float xOffset, float yOffset, bool import)
        {
            Section section;
            if (!sections.TryGetValue(SectionType.MACH, out section))
            {
                throw new Exception("Error: Cannot find MACH section.");
            }
            FileOpsEvent(FileEventType.StatusUpdate, "Load Machines...");

            fs.Position = section.Offset;

            Dictionary<MachineCore, MachineInitData> dictInitData = new Dictionary<MachineCore, MachineInitData>();

            ushort machineCount = ReadUShort(fs);

            for (int j = 0; j < machineCount; j++)
            {
                string name = ReadString(fs);
                byte type;
                string machineLibrary;
                float x, y;
                int dataSize;
                ushort attributeCount, tracks;

                MachineCore machineProto = machines[j];// GetMachine(name);
                machines.Remove(machineProto);
                machines.Insert(j, machineProto); // Reposition these accoring to machine order in bmx
                type = ReadByte(fs);

                if (Encoding.ASCII.GetBytes(name)[0] == 1)
                {
                    // Editor etc
                    machineProto.Hidden = true;
                }
                else
                {
                    FileOpsEvent(FileEventType.StatusUpdate, "Load Machine: " + name + "...");
                }

                MachineDLL machineDLL = new MachineDLL();
                if (type != 0)
                {
                    machineLibrary = ReadString(fs);
                    if (buzz.MachineDLLs.ContainsKey(machineLibrary))
                    {
                        machineDLL = buzz.MachineDLLs[machineLibrary] as MachineDLL;
                        machineProto.MachineDLL = machineDLL;
                        machineProto.MachineDLL.MachineInfo.Type = (MachineType)type;
                    }
                    else
                    {
                        machineDLL = new MachineDLL();
                        machineDLL.Name = machineLibrary;
                        machineDLL.IsMissing = true;
                        machineProto.MachineDLL = machineDLL;
                        machineProto.MachineDLL.MachineInfo.Type = (MachineType)type;
                    }
                }
                else
                {
                    machineLibrary = "Master";
                    machineProto = buzz.SongCore.MachinesList.FirstOrDefault(m => m.DLL.Info.Type == MachineType.Master);
                }

                x = ReadFloat(fs); y = ReadFloat(fs);

                if (import)
                {
                    x += xOffset;
                    y += yOffset;
                }

                dataSize = ReadInt(fs);
                byte[] data = ReadBytes(fs, dataSize);

                attributeCount = ReadUShort(fs);

                for (int k = 0; k < (short)attributeCount; k++)
                {
                    AttributeCore attribute = new AttributeCore(null);
                    attribute.Name = ReadString(fs);
                    attribute.Value = ReadInt(fs);
                    machineProto.AttributesList.Add(attribute);
                }

                int globalParameterCount = machineProto.ParameterGroups[1].Parameters.Count;

                for (int k = 0; k < globalParameterCount; k++)
                {
                    var parameter = machineProto.ParameterGroupsList[1].ParametersList[k];

                    if (parameter.GetTypeSize() == 1)
                    {
                        parameter.SetValue(0, ReadByte(fs));
                    }
                    else if (parameter.GetTypeSize() == 2)
                    {
                        parameter.SetValue(0, ReadUShort(fs));
                    }
                }

                tracks = ReadUShort(fs);

                for (int l = 0; l < tracks; l++)
                {
                    int trackParamCount = machineProto.ParameterGroups[2].Parameters.Count;
                    for (int k = 0; k < trackParamCount; k++)
                    {
                        var parameter = machineProto.ParameterGroupsList[2].ParametersList[k];

                        if (parameter.GetTypeSize() == 1)
                        {
                            parameter.SetValue(l, ReadByte(fs));
                        }
                        else if (parameter.GetTypeSize() == 2)
                        {
                            parameter.SetValue(l, ReadUShort(fs));
                        }
                    }
                }

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
                    }
                    machines[j] = machineProto;
                }
                else
                {
                    // Ignore?: Don't call Init for native machines yet. Wait until all machines are loaded and then call init. Control machines might need machine info.
                    var machineNew = buzz.MachineManager.CreateMachine(machineDLL.Name, machineDLL.Path, null, data, tracks, x, y, machineProto.Hidden, name, true);

                    // Saved machine parameter count/indexes might be a different from the machine that is currently available for ReBuzz. Create parameter mappings
                    RemapLoadedMachineParameterIndex(machineNew, machineProto);

                    dictInitData[machineNew] = new MachineInitData() { data = data, tracks = tracks };

                    // Copy stuff from proto to real. ToDo: Clean this up;
                    machineNew.AttributesList = machineProto.AttributesList;

                    if (!machineNew.DLL.IsMissing)
                    {
                        // Copy parametervalues
                        CopyParameters(machineProto, machineNew, 0, 0);
                        CopyParameters(machineProto, machineNew, 1, 0);
                        for (int m = 0; m < tracks; m++)
                        {
                            CopyParameters(machineProto, machineNew, 2, m);
                        }
                    }
                    else
                    {
                        AddGroup(machineProto, machineNew, 1);
                        AddGroup(machineProto, machineNew, 2);
                        machineNew.MachineDLL.MachineInfo.Type = (MachineType)type;
                        machineNew.Ready = true;
                    }
                    machines[j] = machineNew;

                    // Imported machines might get new names, so machines need to update their data
                    importDictionary[name] = machineNew.Name;

                    if (import)
                    {
                        if (!machineNew.Hidden)
                        {
                            // Keep track of machines imported, so we can undo them
                            importAction.AddMachine(machineNew);
                        }
                    }
                }
            }

            // Send machine names to native machines before adding Patterns.
            // Some machines can remap machine names.
            //foreach (var machine in machines.Where(m => !m.DLL.IsMissing))
            //{
                #region Init Machine Section
                // This region can be moved to the loop end of this method if init needs to be called after every machine has been created.
                //if (!machine.DLL.IsManaged)
                //{
                    //var idata = dictInitData[machine];
                    //FileOpsEvent(FileEventType.StatusUpdate, "Init Machine: " + machine.Name + "...");
                    // Call Init
                    //buzz.MachineManager.CallInit(machine, idata.data, idata.tracks);
                //}
                #endregion

                // Call remap machine names
                //buzz.MachineManager.RemapMachineNames(machine, importDictionary);
            //}
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

        internal static void CopyParameters(MachineCore machineFrom, MachineCore machineTo, int group, int track)
        {
            var paramtersTo = machineTo.ParameterGroupsList[group].ParametersList;
            var paramtersFrom = machineFrom.ParameterGroupsList[group].ParametersList;
            for (int i = 0; i < paramtersTo.Count; i++)
            {
                // Set the defaul/saved state of parameters. Skip non-state, notes and input group
                if (paramtersTo[i].Flags.HasFlag(ParameterFlags.State) && paramtersTo[i].Type != ParameterType.Note && group != 0)
                {
                    if (machineTo.DLL.IsManaged)
                    {
                        paramtersTo[i].SetValue(track, paramtersFrom[i].GetValue(track));
                    }
                    else
                    {
                        paramtersTo[i].DirectSetValue(track, paramtersFrom[i].GetValue(track));
                    }
                }
            }
        }

        internal static void AddGroup(MachineCore machineFrom, MachineCore machineTo, int group)
        {
            machineTo.ParameterGroupsList.Add(machineFrom.ParameterGroupsList[group]);
            var paramtersTo = machineTo.ParameterGroupsList[group].ParametersList;
            machineTo.ParameterGroupsList[group].Machine = machineTo;

            if (group == 1 && machineTo.ParameterGroupsList[group].Parameters.Count > 0)
            {
                machineTo.ParameterGroupsList[group].TrackCount = 1;
            }
        }

        public void LoadSubSections()
        {
            subSections = new Dictionary<string, SubSection>();

            Section section;
            if (sections.TryGetValue(SectionType.IUGB, out section))
            {
                FileOpsEvent(FileEventType.StatusUpdate, "Load SubSections...");

                fs.Position = section.Offset;

                while (true)
                {
                    SubSection subSection = new SubSection();
                    string name = ReadString(fs);
                    if (name == "")
                        break;

                    subSection.Name = name;
                    int length = ReadInt(fs);
                    subSection.Data = ReadBytes(fs, length);

                    subSections[subSection.Name] = subSection;
                }
            }
        }

        public void LoadXNOC()
        {
            Section section;
            if (sections.TryGetValue(SectionType.XNOC, out section))
            {
                FileOpsEvent(FileEventType.StatusUpdate, "Load MultiIO Connection Info...");

                fs.Position = section.Offset;

                ushort numConnections = ReadUShort(fs);
                for (int i = 0; i < numConnections; i++)
                {
                    ushort fromIndex = ReadUShort(fs);
                    ushort toIndex = ReadUShort(fs);
                    int multiIO = ReadInt(fs);

                    if (multiIO == 1)
                    {
                        var fromMachine = machines[fromIndex];
                        var toMachine = machines[toIndex];
                        var connection = fromMachine.AllOutputs.FirstOrDefault(c => c.Destination == toMachine) as MachineConnectionCore;
                        if (connection != null)
                        {
                            int sourceChannel = ReadInt(fs); // Source channel
                            int destinationChannel = ReadInt(fs); // Destination channel

                            connection.SourceChannel = sourceChannel;
                            connection.DestinationChannel = destinationChannel;
                        }
                    }
                }
            }
        }

        private void LoadConnections()
        {
            Section section;
            if (sections.TryGetValue(SectionType.CONN, out section))
            {
                FileOpsEvent(FileEventType.StatusUpdate, "Load Connections...");

                fs.Position = section.Offset;
                ushort conns = ReadUShort(fs);

                for (int i = 0; i < conns; i++)
                {
                    int index1 = ReadUShort(fs);
                    int index2 = ReadUShort(fs);
                    ushort amp, pan;
                    amp = ReadUShort(fs);
                    pan = ReadUShort(fs);

                    MachineCore machineFrom = machines[index1]; // Ignore Master index
                    MachineCore machineTo = machines[index2];

                    if (index2 == 0 && !machineFrom.Hidden)
                    {
                        masterInputCount++;
                    }

                    MachineConnectionCore connection = new MachineConnectionCore();
                    connection.Amp = amp;
                    connection.Pan = pan;
                    connection.Source = machineFrom;
                    connection.Destination = machineTo;
                    connection.HasPan = machineTo.HasStereoInput;

                    connections.Add(connection);

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
                        new ConnectMachinesAction(buzz, connection).Do();
                    }
                }
            }
        }

        private bool LoadPatterns()
        {
            Section section;
            if (sections.TryGetValue(SectionType.PATT, out section))
            {
                FileOpsEvent(FileEventType.StatusUpdate, "Load Patterns...");
                fs.Position = section.Offset;

                foreach (MachineCore machine in machines)
                {
                    List<Tuple<string, int>> patternsAndRows = new List<Tuple<string, int>>();
                    ushort patterns = ReadUShort(fs);
                    ushort tracks = ReadUShort(fs);

                    // Use this to convert built in pattern editor data to PXP
                    MemoryStream msConvertToPXP = new MemoryStream();
                    WriteByte(msConvertToPXP, 3);                               // Write PXP Version
                    WriteInt(msConvertToPXP, patterns);                         // Write pattern count

                    for (int j = 0; j < patterns; j++)
                    {
                        string name = ReadString(fs);
                        ushort rows = ReadUShort(fs);

                        // We only use name and rows for non-built in editors. Read other stuff from the file to convert to PXP
                        patternsAndRows.Add(new Tuple<string, int>(name, rows));

                        WriteString(msConvertToPXP, name);                      // Write pattern name
                        WriteInt(msConvertToPXP, 4);                            // Write rows per beat

                        // ----
                        // Rest of the code used by built in buzz PE. We convert the data to PXP format
                        // ----

                        // Read input columns. Input data gets lost in conversion (not supported by PXP format).
                        int inputCount = machine.AllInputs.Where(input => !(input.Source as MachineCore).Hidden).Count();
                        if (machine.DLL.Info.Type == MachineType.Master)
                        {
                            inputCount = masterInputCount;
                        }

                        for (int k = 0; k < inputCount; k++)
                        {
                            ushort sourceMachineIndex = ReadUShort(fs);

                            if (sourceMachineIndex >= machines.Count)
                            {
                                return false;
                            }

                            var sourceMachine = machines[sourceMachineIndex];
                            var conn = machine.AllInputs.FirstOrDefault(mc => mc.Source == sourceMachine);

                            if (conn != null)
                            {
                                List<PatternEvent> ampEvents = new List<PatternEvent>();
                                List<PatternEvent> panEvents = new List<PatternEvent>();
                                for (int k1 = 0; k1 < rows; k1++)
                                {
                                    ushort ampvalue = ReadUShort(fs);
                                    ushort panvalue = ReadUShort(fs);

                                    if (ampvalue != 65535)
                                    {
                                        ampEvents.Add(new PatternEvent(k1 * PatternEvent.TimeBase, ampvalue));
                                    }
                                    if (panvalue != 65535)
                                    {
                                        panEvents.Add(new PatternEvent(k1 * PatternEvent.TimeBase, panvalue));
                                    }
                                }
                            }
                            else
                            {
                                // If we come here the connection was saved, but not successfully reconnected.
                                // Just skip it.
                                for (int k1 = 0; k1 < rows; k1++)
                                {
                                    ushort ampvalue = ReadUShort(fs);
                                    ushort panvalue = ReadUShort(fs);
                                }
                            }
                        }

                        int numberOfColumns = machine.ParameterGroups[1].Parameters.Count + machine.ParameterGroups[2].Parameters.Count * tracks;
                        WriteInt(msConvertToPXP, numberOfColumns);              // Write number of columns (global + tracks)

                        // Global
                        ReadTrack(machine, 1, 0, rows, msConvertToPXP);

                        // Tracks
                        for (int l = 0; l < tracks; l++)
                        {
                            ReadTrack(machine, 2, l, rows, msConvertToPXP);
                        }
                    }

                    // Does machine use built in editor?
                    if (builtInPeDictionary.ContainsKey(machine.Name) && patterns > 0)
                    {
                        byte[] data = msConvertToPXP.ToArray();
                        // Create PXP compatible editor and input converted data
                        buzz.CreateEditor(machine, buzz.DefaultPatternEditor, data);
                    }

                    // Create patterns
                    foreach (var nameRow in patternsAndRows)
                    {
                        //if (!machine.DLL.IsMissing)
                        {
                            machine.CreatePattern(nameRow.Item1, nameRow.Item2);
                        }
                    }
                }
            }

            return true;
        }

        private bool ReadTrack(MachineCore machine, int group, int track, int rows, MemoryStream msConvertToPXP)
        {
            Dictionary<int, List<PatternEvent>> columnEventsDictionary = new Dictionary<int, List<PatternEvent>>();
            int columnCount = machine.ParameterGroups[group].Parameters.Count;

            for (int i = 0; i < columnCount; i++)
            {
                columnEventsDictionary[i] = new List<PatternEvent>();
            }

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < machine.ParameterGroups[group].Parameters.Count; j++)
                {
                    List<PatternEvent> events = columnEventsDictionary[j];
                    ParameterCore param = machine.ParameterGroupsList[group].ParametersList[j];

                    int value = 0;
                    int typeSize = param.GetTypeSize();
                    if (typeSize == 1)
                        value = ReadByte(fs);
                    else if (typeSize == 2)
                        value = ReadUShort(fs);

                    if (value != param.NoValue)
                        events.Add(new PatternEvent(i * PatternEvent.TimeBase, value));
                }
            }

            // Convert to PXP data
            int index = group == 1 ? 0 : machine.ParameterGroups[1].Parameters.Count;
            for (int i = 0; i < columnCount; i++)
            {
                WriteString(msConvertToPXP, machine.Name);              // Name
                WriteInt(msConvertToPXP, index);                        // Parameter Index
                WriteInt(msConvertToPXP, track);                        // Track
                WriteByte(msConvertToPXP, 0);                           // Bool graphical

                var columnEvents = columnEventsDictionary[i];
                int eventCount = columnEvents.Count;
                WriteInt(msConvertToPXP, eventCount);                   // Number of events in Column
                for (int j = 0; j < eventCount; j++)
                {
                    var pe = columnEvents[j];
                    WriteInt(msConvertToPXP, pe.Time / PatternEvent.TimeBase);
                    WriteInt(msConvertToPXP, pe.Value);
                }
                index++;
            }

            return true;
        }

        public void LoadXTAP(bool import)
        {
            Section section;
            if (sections.TryGetValue(SectionType.XTAP, out section))
            {
                FileOpsEvent(FileEventType.StatusUpdate, "Load Pattern Editor Connections...");
                fs.Position = section.Offset;
                
                byte version = ReadByte(fs);
                MachineCore masterEditor = null;

                if (version == 1)
                {
                    for (int i = 0; i < machines.Count; i++)
                    {
                        ushort numPatterns = ReadUShort(fs);
                        var machine = machines[i];

                        for (int j = 0; j < numPatterns; j++)
                        {
                            ushort patternEditorIndex = ReadUShort(fs);

                            if (patternEditorIndex == 65535)    // If pattern is assigned to built in editor
                            {
                                // Don't do anything if master and builtin pattern data
                                if (import && i == 0)
                                {
                                }
                                else
                                {
                                    builtInPeDictionary[machine.Name] = true;
                                }
                            }
                            else
                            {
                                // Don't import master editor. Remove the imported editor
                                if (import && i == 0)
                                {
                                    masterEditor = machines[patternEditorIndex];
                                }
                                else
                                {
                                    var editor = machines[patternEditorIndex];
                                    machine.EditorMachine = editor;
                                }
                            }
                        }
                    }

                    if (import && masterEditor != null)
                    {
                        new DisconnectMachinesAction(buzz, masterEditor.AllOutputs[0]).Do();
                        buzz.RemoveMachine(masterEditor);
                    }
                }
            }
            // All machines use built in editor
            else
            {
                for (int i = 0; i < machines.Count; i++)
                {
                    var machine = machines[i];
                    builtInPeDictionary[machine.Name] = true;
                }
            }
        }

        public void LoadTAP2()
        {
            Section section;
            if (sections.TryGetValue(SectionType.TAP2, out section))
            {
                FileOpsEvent(FileEventType.StatusUpdate, "Load New Pattern Editor Data...");
                fs.Position = section.Offset;

                byte version = ReadByte(fs);

                if (version != 2)
                    return;

                foreach (MachineCore machine in machines)
                {
                    ushort numPatterns = ReadUShort(fs);

                    for (ushort i = 0; i < numPatterns; i++)
                    {
                        string patternName = ReadString(fs);
                        var pattern = machine.Patterns.FirstOrDefault(p => p.Name == patternName);

                        int parameterColumnCount = ReadInt(fs);
                        for (int j = 0; j < parameterColumnCount; j++)
                        {
                            ushort pMachineIndex = ReadUShort(fs);
                            var targetMachine = pMachineIndex != 0xFFFF ? machines[pMachineIndex] : null;
                            int group = ReadInt(fs);
                            int indexInGroup = ReadInt(fs);
                            int track = ReadInt(fs);

                            IParameter targetParameter = null;
                            if (targetMachine != null)
                            {
                                if (!targetMachine.DLL.IsMissing)
                                {
                                    // Negative group is Buzz midi column invisible to editors. Used by Note Matrix.
                                    targetParameter = (group != -1 && indexInGroup != -1) ? targetMachine.ParameterGroups[group].Parameters[indexInGroup] : ParameterCore.GetMidiParameter(targetMachine, dispatcher);
                                }
                            }

                            IPatternColumn column = null;
                            if (pattern != null && targetParameter != null)
                            {
                                int newIndex = Math.Max(Math.Min(j, pattern.Columns.Count - 1), 0);
                                pattern.InsertColumn(newIndex, targetParameter, track);
                                column = pattern.Columns[newIndex];
                            }

                            int numberOfevents = ReadInt(fs);
                            List<PatternEvent> events = new List<PatternEvent>();
                            for (int k = 0; k < numberOfevents; k++)
                            {
                                PatternEvent pe = new PatternEvent
                                {
                                    Time = ReadInt(fs),
                                    Value = ReadInt(fs),
                                    Duration = ReadInt(fs)
                                };
                                events.Add(pe);
                            }

                            column?.SetEvents(events, true);

                            int numberOfMetadata = ReadInt(fs);
                            for (int k = 0; k < numberOfMetadata; k++)
                            {
                                string key = ReadString(fs);
                                string value = ReadString(fs);
                                if (column != null)
                                {
                                    column.Metadata[key] = value;
                                }
                            }
                        }
                    }
                }
            }
        }

        private bool LoadSequences(bool import)
        {
            Section section;
            if (sections.TryGetValue(SectionType.SEQU, out section))
            {
                FileOpsEvent(FileEventType.StatusUpdate, "Load Sequences...");
                fs.Position = section.Offset;
                int endSong = ReadInt(fs);
                int beginLoop = ReadInt(fs);
                int endLoop = ReadInt(fs);
                ushort numSequences = ReadUShort(fs);

                var song = buzz.SongCore;
                if (!import)
                {
                    song.LoopStart = beginLoop;
                    song.LoopEnd = endLoop;
                    song.SongEnd = endSong;
                }

                int seqIndex = 0;

                for (int i = 0; i < numSequences; i++)
                {

                    ushort machineIndex = ReadUShort(fs);
                    MachineCore machine = machines[machineIndex];
                    SequenceCore sequence;

                    byte posSize = 0, eventSize = 0;
                    uint events = ReadUInt(fs);
                    if (events > 0)
                    {
                        posSize = ReadByte(fs);
                        eventSize = ReadByte(fs);
                    }

                    if (machine != null)
                    {
                        song.AddSequence(machine, seqIndex);
                        sequence = song.Sequences[seqIndex] as SequenceCore;
                        bmxSequences.Add(sequence);
                        seqIndex++;

                        for (int j = 0; j < events; j++)
                        {
                            ulong pos = ReadVal(fs, posSize);
                            ulong value = ReadVal(fs, eventSize);

                            if (value == 0)
                            {
                                // Mute
                                sequence.SetEvent((int)pos, new SequenceEvent(SequenceEventType.Mute));
                            }
                            else if (value == 1)
                            {
                                // Break
                                sequence.SetEvent((int)pos, new SequenceEvent(SequenceEventType.Break));
                            }
                            else if (value == 2)
                            {
                                // Thru
                                sequence.SetEvent((int)pos, new SequenceEvent(SequenceEventType.Thru));
                            }
                            else if (value >= 0x10)
                            {
                                // machine+value -> pattern id
                                int ptnidx = (int)(value - 0x10);
                                if (ptnidx >= 0 && ptnidx < machine.Patterns.Count)
                                {
                                    var mp = machine.PatternsList[ptnidx];
                                    sequence.SetEvent((int)pos, new SequenceEvent(SequenceEventType.PlayPattern, mp, mp.Length));
                                }
                            }
                        }
                    }
                    else
                    {
                        // plugin does not exists; skip events
                        for (int j = 0; j < events; j++)
                        {
                            ulong pos = ReadVal(fs, posSize);
                            ulong value = ReadVal(fs, eventSize);
                        }
                    }
                }
            }
            return true;
        }

        private bool LoadXCAM()
        {
            Section section;
            if (sections.TryGetValue(SectionType.XCAM, out section))
            {
                FileOpsEvent(FileEventType.StatusUpdate, "Load Machine Properties...");

                fs.Position = section.Offset;
                int machineCount = ReadUShort(fs);
                for (int i = 0; i < machineCount; i++)
                {
                    string machineName = ReadString(fs);
                    MachineCore machine = machines[i];

                    int numberOfItems = ReadInt(fs);

                    for (int j = 0; j < numberOfItems; j++)
                    {
                        string itemName = ReadString(fs);
                        int itemSize = ReadInt(fs);
                        int val = 0;
                        if (itemSize == 1)
                        {
                            val = ReadByte(fs);
                        }
                        else if (itemSize == 4)
                        {
                            val = ReadInt(fs);
                        }

                        if (itemName == "Mute")
                        {
                            machine.IsMuted = val == 1;
                        }
                        else if (itemName == "MIDIInputChannel")
                        {
                            machine.MIDIInputChannel = val;
                        }
                        else if (itemName == "OverrideDelay")
                        {
                            machine.OverrideLatency = val;
                        }
                        else if (itemName == "OversampleFactor ")
                        {
                            machine.OversampleFactor = val;
                        }
                        else if (itemName == "Wireless")
                        {
                            machine.IsWireless = val == 1;
                        }
                    }
                }
            }
            return true;
        }

        // Call this after sequeces have been loaded
        private bool LoadXQES()
        {
            Section section;
            if (sections.TryGetValue(SectionType.XQES, out section))
            {
                FileOpsEvent(FileEventType.StatusUpdate, "Load Sequence Properties...");

                fs.Position = section.Offset;

                int numSequences = ReadInt(fs);

                for (int i = 0; i < numSequences; i++)
                {
                    var seq = bmxSequences[i];
                    int numberOfItems = ReadInt(fs);

                    for (int j = 0; j < numberOfItems; j++)
                    {
                        string itemName = ReadString(fs);
                        int itemSize = ReadInt(fs);
                        int val = 0;
                        if (itemSize == 1)
                        {
                            val = ReadByte(fs);
                        }
                        else if (itemSize == 4)
                        {
                            val = ReadInt(fs);
                        }

                        if (itemName == "IsDisabled")
                        {
                            seq.IsDisabled = val == 1;
                        }
                    }
                }
            }
            return true;
        }

        void LoadReBuzzSongSettings()
        {
            Section section;
            if (sections.TryGetValue(SectionType.RBSG, out section))
            {
                FileOpsEvent(FileEventType.StatusUpdate, "Load ReBuzz Song Settings...");

                fs.Position = section.Offset;
                int numberOfItems = ReadInt(fs);

                for (int j = 0; j < numberOfItems; j++)
                {
                    string itemName = ReadString(fs);
                    int itemSize = ReadInt(fs);
                    int val = 0;
                    if (itemSize == 1)
                    {
                        val = ReadByte(fs);
                    }
                    else if (itemSize == 4)
                    {
                        val = ReadInt(fs);
                    }

                    if (itemName == "Speed")
                    {
                        buzz.Speed = val;
                    }
                }
            }
        }

        public bool LoadWaveTable()
        {
            Section section;
            if (sections.TryGetValue(SectionType.WAVT, out section))
            {
                FileOpsEvent(FileEventType.StatusUpdate, "Load WaveTable...");

                fs.Position = section.Offset;
                ushort waveCount = ReadUShort(fs);
                for (int i = 0; i < waveCount; i++)
                {
                    ushort index = ReadUShort(fs);

                    if (index >= 200)
                    {
                        break;
                    }

                    ushort newIndex = index;
                    while (buzz.SongCore.WavetableCore.WavesList[newIndex] != null && newIndex < 200)
                    {
                        newIndex++;
                    }

                    // Can't fit imported waves to new slots
                    if (newIndex >= 200)
                    {
                        break;
                    }

                    remappedWaveReferences[index] = newIndex;
                    if (importAction != null)
                        importAction.AddWaveIndex(newIndex);
                    WaveCore wave = buzz.SongCore.WavetableCore.CreateWave(newIndex);

                    wave.FileName = ReadString(fs);
                    wave.Name = ReadString(fs);
                    wave.Volume = ReadFloat(fs);
                    wave.Flags = (WaveFlags)ReadByte(fs);
                    wave.Index = newIndex;

                    // Not doing anything with envelopes currently
                    if (((int)wave.Flags & WaveFlagsEnvelope) != 0)
                    {
                        ushort numEnvelopes = ReadUShort(fs);

                        for (int j = 0; j < numEnvelopes; j++)
                        {
                            ushort numPoints = 0;
                            Envelope envelope = new Envelope();

                            envelope.Attack = ReadUShort(fs);   // Attack time 
                            envelope.Decay = ReadUShort(fs);    // Decay time
                            envelope.Sustain = ReadUShort(fs);  // Sustain level
                            envelope.Release = ReadUShort(fs);  // Release time
                            envelope.SubDivide = ReadByte(fs);  // ADSR Subdivide
                            envelope.Flags = ReadByte(fs);      // ADSR Flags
                            numPoints = ReadUShort(fs);         // number of points (can be zero) (bit 15 set = envelope disabled)
                            envelope.IsEnabled = (numPoints & 0x8000) == 0;
                            numPoints &= 0x7FFF;

                            for (int k = 0; k < numPoints; k++)
                            {
                                Tuple<int, int> point = new Tuple<int, int>(0, 0);
                                int x = ReadUShort(fs);
                                int y = ReadUShort(fs);
                                int flags = ReadByte(fs); // Not used?
                                envelope.PointsList.Add(point);
                            }
                            wave.envelopes.Add(envelope);
                        }
                    }

                    byte waveLeyers = ReadByte(fs);

                    for (int j = 0; j < waveLeyers; j++)
                    {
                        WaveLayerCore waveLayer = new WaveLayerCore();
                        waveLayer.ChannelCount = wave.Flags.HasFlag(WaveFlags.Stereo) ? 2 : 1;
                        waveLayer.SampleCount16Bit = ReadInt(fs);
                        waveLayer.LoopStart16Bit = ReadInt(fs);
                        waveLayer.LoopEnd16Bit = ReadInt(fs);
                        waveLayer.SampleRate = ReadInt(fs);
                        waveLayer.RootNote = ReadByte(fs);
                        waveLayer.Wave = wave;
                        wave.LayersList.Add(waveLayer);
                    }
                }
            }
            return true;
        }

        private bool LoadWaves()
        {
            Section section;
            if (!sections.TryGetValue(SectionType.CWAV, out section))
            {
                sections.TryGetValue(SectionType.WAVE, out section);
            }
            if (section != null)
            {
                FileOpsEvent(FileEventType.StatusUpdate, "Load Waves...");
                fs.Position = section.Offset;

                ushort waveCount = ReadUShort(fs);
                for (int i = 0; i < waveCount; i++)
                {
                    ushort index = ReadUShort(fs);
                    index = (ushort)remappedWaveReferences[index];
                    if (index >= 200)
                    {
                        // Index error
                        break;
                    }
                    FileOpsEvent(FileEventType.StatusUpdate, "Load Wave Index: " + index + "...");

                    byte format = ReadByte(fs);
                    WaveCore wave = buzz.SongCore.WavetableCore.WavesList[index];

                    WaveFlags waveflags = wave.Flags;
                    int numchannels = waveflags.HasFlag(WaveFlags.Stereo) ? 2 : 1;
                    int numlevels = wave.Layers.Count;

                    //Determine number of bytes 
                    //This isn't used, so we don't really care about it!
                    long totalBytes = 0;
                    WaveUnpack unpacker = null;
                    switch (format)
                    {
                        case 0: //Uncompressed
                            totalBytes = ReadUInt(fs); // All bytes in all layers
                            break;
                        case 1: //Compressed
                            //Total bytes not known.  Use file postion and size
                            totalBytes = fs.Length - fs.Position;
                            unpacker = new WaveUnpack(fs);
                            break;
                        default:
                            throw new Exception("Unknown BMX format " + format);
                    }

                    for (int j = 0; j < numlevels; j++)
                    {
                        WaveLayerCore waveLayer = wave.LayersList[j];
                        int buffersize = waveLayer.SampleCount16Bit * 2 * numchannels;

                        byte[] buffer;
                        WaveFormat waveFormat = WaveFormat.Int16;
                        if(unpacker == null)
                        {
                            //uncompressed
                            buffer = ReadBytes(fs, buffersize);
                            if (waveflags.HasFlag(WaveFlags.Not16Bit))
                            {
                                waveLayer.extended = true;
                                // Get the extended info (8 bytes). WaveFormat is the only data here?
                                waveFormat = (WaveFormat)buffer[0];
                            }
                            else
                            {
                                waveLayer.extended = false;
                            }
                        }
                        else
                        {
                            //Compressed
                            buffer = unpacker.DecompressWave(waveLayer.SampleCount16Bit, (numchannels == 2));
                        }

                        waveLayer.Init(waveLayer.Path, waveFormat, waveLayer.RootNote, waveLayer.ChannelCount == 2, waveLayer.SampleCount16Bit);
                        waveLayer.SetRawByteData(buffer);
                    }

                    //Tell the unpacker to rewind, giving back any data it over-consumed
                    if (unpacker != null)
                        unpacker.Rewind();

                    wave.Invalidate();
                }
            }
            return true;
        }

        private void LoadDialogProperties()
        {
            Section section;
            if (sections.TryGetValue(SectionType.GLDP, out section))
            {
                FileOpsEvent(FileEventType.StatusUpdate, "Load Dialog Info...");
                fs.Position = section.Offset;

                byte version = ReadByte(fs);
                if (version == 1)
                {
                    while (true)
                    {
                        string name = ReadString(fs);
                        if (name.Length == 0)
                        {
                            break;
                        }

                        name = importDictionary.ContainsKey(name) ? importDictionary[name] : name;

                        int flags = ReadInt(fs);
                        int value2 = ReadInt(fs);
                        int value3 = ReadInt(fs);

                        int leftPos = ReadInt(fs);                // Left
                        int topPos = ReadInt(fs);                // Top?
                        int RightPos = ReadInt(fs);                // Right?
                        int bottomPos = ReadInt(fs);                // Bottom?

                        if (leftPos != -1 && topPos != -1 && RightPos != -1 && bottomPos != -1)
                        {
                            var machine = buzz.SongCore.MachinesList.FirstOrDefault(m => m.Name == name);
                            if (machine != null)
                            {
                                machine.OpenWindowedGUI(new System.Windows.Rect(leftPos, topPos, RightPos - leftPos, bottomPos - topPos));
                            }
                        }

                        leftPos = ReadInt(fs);                  // Left
                        topPos = ReadInt(fs);                   // Top
                        RightPos = ReadInt(fs);                 // Right
                        bottomPos = ReadInt(fs);                // Bottom

                        if (leftPos != -1 && topPos != -1 && RightPos != -1 && bottomPos != -1)
                        {
                            var machine = buzz.SongCore.MachinesList.FirstOrDefault(m => m.Name == name);
                            if (machine != null)
                            {
                                machine.OpenParameterWindow(new System.Windows.Rect(leftPos, topPos, RightPos - leftPos, bottomPos - topPos));
                            }
                        }
                    }
                }
            }
        }

        public void Save(string path)
        {
            ReBuzzCore.SkipAudio = true;
            sections = new Dictionary<SectionType, Section>();
            Open(path, FileMode.Create);
            machines = new List<MachineCore>();

            CreateFileInfoSection();
            CreateParaSection();
            CreateWaveTableSection();
            CreateSubSenctionsSection();
            CreateMachinesSection();
            CreateConnectionsSection();
            CreateXNOC();
            CreatePatternsSection();
            CreateXPATSection();
            CreateSequencesSection();
            CreateXCAMSection();
            CreateWavesSection();
            CreateInfoTextSection();
            CreateReBuzzSongSettings();
            CreateTAP2Section();
            CreateDialogPropertiesSection();
            CreateXQESSection();

            uint offset = 4;                                // Buzz magic
            offset += 4;                                    // Number of sections
            offset += (uint)sections.Count * (4 + 4 + 4);   // reserve space for all sections: magic id + section offset + size

            // Write header
            MemoryStream ms = new MemoryStream();
            WriteUInt(ms, (uint)SectionType.Buzz);
            WriteUInt(ms, (uint)sections.Count);

            foreach (var section in sections.Values)
            {
                WriteUInt(ms, section.Magic);
                WriteUInt(ms, offset);
                WriteUInt(ms, section.Size);

                offset += section.Size;
            }

            byte[] headerData = ms.ToArray();
            fs.Write(headerData, 0, headerData.Length);

            // HEader done. Now write the actual sections
            foreach (var section in sections.Values)
            {
                fs.Write(section.Data, 0, (int)section.Size);
            }
            EndFileOperation(false);
            ReBuzzCore.SkipAudio = false;
        }

        private void CreateReBuzzSongSettings()
        {
            MemoryStream ms = new MemoryStream();

            int numberOfItems = 1;
            WriteInt(ms, numberOfItems);

            WriteString(ms, "Speed"); // Name
            WriteInt(ms, 4); // Size
            WriteInt(ms, buzz.Speed); // Value

            AddSection(ms.ToArray(), SectionType.RBSG);
        }

        void CreateParaSection()
        {
            MemoryStream ms = new MemoryStream();

            uint numMachines = (uint)buzz.SongCore.MachinesList.Count;
            WriteUInt(ms, numMachines);

            foreach (var imac in buzz.SongCore.MachinesList)
            {
                var machine = imac;
                WritePara(ms, machine);
            }

            AddSection(ms.ToArray(), SectionType.PARA);
        }

        void WritePara(MemoryStream ms, MachineCore machine)
        {
            WriteString(ms, machine.Name);
            WriteString(ms, machine.DLL.Name);

            var gParams = machine.ParameterGroupsList[1].ParametersList;
            uint numGlobals = (uint)gParams.Count;

            var tParams = machine.ParameterGroupsList[2].ParametersList;
            uint numTrackParams = (uint)tParams.Count;

            WriteUInt(ms, numGlobals);
            WriteUInt(ms, numTrackParams);

            foreach (var parameter in gParams)
            {
                WriteByte(ms, (byte)parameter.Type);
                WriteString(ms, parameter.Name);
                WriteInt(ms, parameter.MinValue);
                WriteInt(ms, parameter.MaxValue);
                WriteInt(ms, parameter.NoValue);
                WriteInt(ms, (int)parameter.Flags);
                WriteInt(ms, parameter.DefValue);
            }

            foreach (var parameter in tParams)
            {
                WriteByte(ms, (byte)parameter.Type);
                WriteString(ms, parameter.Name);
                WriteInt(ms, parameter.MinValue);
                WriteInt(ms, parameter.MaxValue);
                WriteInt(ms, parameter.NoValue);
                WriteInt(ms, (int)parameter.Flags);
                WriteInt(ms, parameter.DefValue);
            }
        }

        void CreateSubSenctionsSection()
        {
            MemoryStream ms = new MemoryStream();
            foreach (var sub in subSections.Values)
            {
                WriteString(ms, sub.Name);
                WriteInt(ms, sub.Data.Length);
                WriteBytes(ms, sub.Data);
            }
            WriteString(ms, "");
            AddSection(ms.ToArray(), SectionType.IUGB);
        }

        // After call to CreateMachinesSection machines list contains the corrent indexes in file
        void CreateMachinesSection()
        {
            MemoryStream ms = new MemoryStream();

            ushort machineCount = (ushort)buzz.SongCore.MachinesList.Count;

            WriteUShort(ms, machineCount);
            foreach (var machine in buzz.SongCore.MachinesList)
            {
                WriteMachine(ms, machine);
                machines.Add(machine);
            }
            AddSection(ms.ToArray(), SectionType.MACH);
        }

        void WriteMachine(MemoryStream ms, MachineCore machine)
        {
            WriteString(ms, machine.Name);
            WriteByte(ms, (byte)machine.MachineDLL.Info.Type);
            if (machine.MachineDLL.Info.Type != MachineType.Master)
            {
                WriteString(ms, machine.DLL.Name);
            }

            WriteFloat(ms, machine.Position.Item1);
            WriteFloat(ms, machine.Position.Item2);
            var data = machine.Data;
            if (data != null)
            {
                WriteInt(ms, data.Length);
                WriteBytes(ms, data);
            }
            else
            {
                WriteInt(ms, 0);
            }

            WriteUShort(ms, (ushort)machine.Attributes.Count);
            for (int k = 0; k < machine.Attributes.Count; k++)
            {
                AttributeCore attribute = machine.AttributesList[k];
                WriteString(ms, attribute.Name);
                WriteInt(ms, attribute.Value);
            }

            int globalParameterCount = machine.ParameterGroups[1].Parameters.Count;

            for (int k = 0; k < globalParameterCount; k++)
            {
                var parameter = machine.ParameterGroupsList[1].ParametersList[k];

                if (parameter.GetTypeSize() == 1)
                {
                    WriteByte(ms, (byte)parameter.GetValue(0));
                }
                else if (parameter.GetTypeSize() == 2)
                {
                    WriteUShort(ms, (ushort)parameter.GetValue(0));
                }
            }

            ushort tracks = (ushort)machine.ParameterGroups[2].TrackCount;
            WriteUShort(ms, tracks);

            for (int l = 0; l < tracks; l++)
            {
                int trackParamCount = machine.ParameterGroups[2].Parameters.Count;
                for (int k = 0; k < trackParamCount; k++)
                {
                    var parameter = machine.ParameterGroupsList[2].ParametersList[k];

                    if (parameter.GetTypeSize() == 1)
                    {
                        WriteByte(ms, (byte)parameter.GetValue(l));
                    }
                    else if (parameter.GetTypeSize() == 2)
                    {
                        WriteUShort(ms, (ushort)parameter.GetValue(l));
                    }
                }
            }
        }

        void CreateConnectionsSection()
        {
            MemoryStream ms = new MemoryStream();
            Dictionary<IMachineConnection, bool> machineConnectionsDict = new Dictionary<IMachineConnection, bool>();

            // Collect all connections
            foreach (var machine in buzz.SongCore.MachinesList)
            {
                foreach (var input in machine.AllInputs)
                    machineConnectionsDict[input] = true;
            }

            ushort conns = (ushort)machineConnectionsDict.Count;
            WriteUShort(ms, conns);

            foreach (var conn in machineConnectionsDict.Keys)
            {
                var machineFrom = conn.Source as MachineCore;
                ushort index1 = (ushort)buzz.SongCore.MachinesList.IndexOf(machineFrom);
                WriteUShort(ms, index1);

                var machineTo = conn.Destination as MachineCore;
                ushort index2 = (ushort)buzz.SongCore.MachinesList.IndexOf(machineTo);
                WriteUShort(ms, index2);

                WriteUShort(ms, (ushort)conn.Amp);
                WriteUShort(ms, (ushort)conn.Pan);
            }
            AddSection(ms.ToArray(), SectionType.CONN);
        }

        public void CreateXNOC()
        {
            MemoryStream ms = new MemoryStream();

            var song = buzz.SongCore;
            var macs = song.MachinesList.Where(m => m.DLL.Info.Type != MachineType.Master);

            List<IMachineConnection> connections = new List<IMachineConnection>();
            song.MachinesList.Run(m => connections.AddRange(m.AllOutputs));
            ushort numConnections = 0;
            macs.Run(m => numConnections += (ushort)m.AllOutputs.Count);
            WriteUShort(ms, numConnections);
            foreach (var conn in connections)
            {
                var source = conn.Source as MachineCore;
                var destinaltion = conn.Destination as MachineCore;
                int fromIndex = song.MachinesList.IndexOf(source);
                WriteUShort(ms, (ushort)fromIndex);
                int toIndex = song.MachinesList.IndexOf(conn.Destination as MachineCore);
                WriteUShort(ms, (ushort)toIndex);
                int multiIO = source.MachineDLL.Info.Flags.HasFlag(MachineInfoFlags.MULTI_IO) | destinaltion.MachineDLL.Info.Flags.HasFlag(MachineInfoFlags.MULTI_IO) ? 1 : 0;
                WriteInt(ms, multiIO);

                if (multiIO == 1)
                {
                    WriteInt(ms, conn.SourceChannel);
                    WriteInt(ms, conn.DestinationChannel);
                }
            }

            AddSection(ms.ToArray(), SectionType.XNOC);
        }

        void CreatePatternsSection()
        {
            MemoryStream ms = new MemoryStream();

            foreach (MachineCore machine in machines)
            {
                ushort patterns = (ushort)machine.Patterns.Count;
                ushort tracks = (ushort)machine.TrackCount;

                WriteUShort(ms, patterns);
                WriteUShort(ms, tracks);

                foreach (var pattern in machine.PatternsList)
                {
                    WriteString(ms, pattern.Name);
                    ushort rows = (ushort)pattern.Length;
                    WriteUShort(ms, rows);

                    var patternInputs = machine.AllInputs.Where(input => !(input.Source as MachineCore).Hidden).ToArray();
                    int inputCount = patternInputs.Length;
                    for (int k = 0; k < inputCount; k++)
                    {
                        var sourceMachine = patternInputs[k].Source as MachineCore;
                        ushort sourceMachineIndex = (ushort)machines.IndexOf(sourceMachine);

                        WriteUShort(ms, sourceMachineIndex);

                        var conn = machine.AllInputs.FirstOrDefault(mc => mc.Source == sourceMachine);

                        if (conn != null)
                        {
                            for (int k1 = 0; k1 < rows; k1++)
                            {
                                WriteUShort(ms, 65535);
                                WriteUShort(ms, 65535);
                            }
                        }
                        else
                        {
                            // if we come here the connection was saved, but not successfully reconnected.
                            // just skip it.
                            for (int k1 = 0; k1 < rows; k1++)
                            {
                                WriteUShort(ms, 65535);
                                WriteUShort(ms, 65535);
                            }
                        }
                    }

                    // Global
                    WriteTrack(ms, machine, pattern, 1, 0, rows);

                    // Tracks

                    for (int l = 0; l < tracks; l++)
                    {
                        WriteTrack(ms, machine, pattern, 2, l, rows);
                    }
                }
            }

            AddSection(ms.ToArray(), SectionType.PATT);
        }

        private bool WriteTrack(MemoryStream ms, MachineCore machine, PatternCore Pattern, int group, int track, int rows)
        {
            // We don't use these so write no values
            for (int i = 0; i < rows; i++)
            {
                List<PatternEvent> events = new List<PatternEvent>();
                for (int j = 0; j < machine.ParameterGroups[group].Parameters.Count; j++)
                {
                    ParameterCore param = machine.ParameterGroupsList[group].ParametersList[j];

                    //int value = 0;
                    int typeSize = param.GetTypeSize();
                    if (typeSize == 1)
                        WriteByte(ms, (byte)param.NoValue);
                    else if (typeSize == 2)
                        WriteUShort(ms, (ushort)param.NoValue);

                    //if (value != param.NoValue)
                    //    events.Add(new PatternEvent(i * PatternEvent.TimeBase, value));
                    //if (result != 0 && value != zzub_parameter_get_value_none(param))
                    //    insert_pattern_value(result, plugin.plugin, group, track, j, i, value);

                }
            }

            return true;
        }

        public void CreateXPATSection()
        {
            MemoryStream ms = new MemoryStream();

            WriteByte(ms, 1); // version

            for (int i = 0; i < buzz.SongCore.MachinesList.Count; i++)
            {
                var machine = machines[i];
                ushort numPatterns = (ushort)machine.Patterns.Count;

                var editor = machine.EditorMachine;
                int editorIndex = buzz.SongCore.MachinesList.IndexOf(editor);
                if (editorIndex == -1)
                    numPatterns = 0; // Ensure editor is correct. Should not go here.

                WriteUShort(ms, numPatterns);

                for (int j = 0; j < numPatterns; j++)
                {
                    WriteUShort(ms, (ushort)editorIndex);
                }
            }

            AddSection(ms.ToArray(), SectionType.XTAP);
        }

        public void CreateTAP2Section()
        {
            MemoryStream ms = new MemoryStream();

            WriteByte(ms, 2);   // Version

            foreach (MachineCore machine in buzz.SongCore.MachinesList)
            {
                ushort numPatterns = (ushort)machine.Patterns.Count;
                WriteUShort(ms, numPatterns);

                for (ushort i = 0; i < numPatterns; i++)
                {
                    var pattern = machine.Patterns[i];
                    WriteString(ms, pattern.Name);

                    int parameterColumnCount = pattern.Columns.Count;
                    WriteInt(ms, parameterColumnCount);

                    for (int j = 0; j < parameterColumnCount; j++)
                    {
                        var column = pattern.Columns[j];
                        var parameter = column.Parameter as ParameterCore;
                        var targetMachine = column.Parameter.Group != null ? column.Parameter.Group.Machine as MachineCore : null;
                        if (targetMachine == null && parameter.Machine != null)
                        {
                            targetMachine = parameter.Machine;
                        }
                        ushort pMachineIndex = targetMachine != null ? (ushort)buzz.SongCore.MachinesList.IndexOf(targetMachine) : (ushort)0xFFFF;
                        WriteUShort(ms, pMachineIndex);
                        WriteInt(ms, parameter.Group != null ? (int)parameter.Group.Type : -1);
                        WriteInt(ms, parameter.IndexInGroup >= 0 ? parameter.IndexInGroup : -1);
                        WriteInt(ms, column.Track);

                        var events = column.GetEvents(0, int.MaxValue);
                        int numberOfEvents = events.Count();
                        WriteInt(ms, numberOfEvents);
                        foreach (var pe in events)
                        {
                            WriteInt(ms, pe.Time);
                            WriteInt(ms, pe.Value);
                            WriteInt(ms, pe.Duration);
                        }

                        var metadata = column.Metadata.ToArray();
                        WriteInt(ms, metadata.Length);
                        foreach (var mt in metadata)
                        {
                            WriteString(ms, mt.Key);
                            WriteString(ms, mt.Value);
                        }
                    }
                }
            }
            AddSection(ms.ToArray(), SectionType.TAP2);
        }

        void CreateSequencesSection()
        {
            MemoryStream ms = new MemoryStream();

            var song = buzz.SongCore;
            WriteInt(ms, song.SongEnd);
            WriteInt(ms, song.LoopStart);
            WriteInt(ms, song.LoopEnd);

            WriteUShort(ms, (ushort)song.SequencesList.Count);

            foreach (var sequence in song.SequencesList)
            {
                ushort machineIndex = (ushort)machines.IndexOf(sequence.Machine as MachineCore);
                WriteUShort(ms, machineIndex);
                uint events = (uint)sequence.Events.Count;
                WriteUInt(ms, events);
                byte posSize = 2, eventSize = 2;
                if (events > 0)
                {
                    WriteByte(ms, posSize);
                    WriteByte(ms, eventSize);

                    for (int j = 0; j < events; j++)
                    {
                        int pos = sequence.Events.Keys.ElementAt(j);
                        var sequenceEvent = sequence.Events[pos];
                        ulong value;

                        if (sequenceEvent.Type == SequenceEventType.Mute)
                        {
                            // Mute
                            value = 0;
                        }
                        else if (sequenceEvent.Type == SequenceEventType.Break)
                        {
                            // Break
                            value = 1;
                        }
                        else if (sequenceEvent.Type == SequenceEventType.Thru)
                        {
                            // Thru
                            value = 2;
                        }
                        else
                        {
                            // machine+value -> pattern id
                            int ptnidx = sequence.Machine.Patterns.IndexOf(sequenceEvent.Pattern);
                            value = (ulong)ptnidx + 0x10;
                        }

                        WriteUShort(ms, (ushort)pos);
                        WriteUShort(ms, (ushort)value);
                    }
                }
            }

            AddSection(ms.ToArray(), SectionType.SEQU);
        }

        void CreateXCAMSection()
        {
            MemoryStream ms = new MemoryStream();
            var song = buzz.SongCore;

            ushort machineCount = (ushort)song.Machines.Count;
            WriteUShort(ms, machineCount);

            for (int i = 0; i < machineCount; i++)
            {
                var machine = song.Machines[i];
                WriteString(ms, machine.Name);

                int numberOfItems = 5;
                WriteInt(ms, numberOfItems);

                WriteString(ms, "Mute"); // Name
                WriteInt(ms, 1); // Size
                WriteBool(ms, machine.IsMuted); // Value

                WriteString(ms, "MIDIInputChannel");
                WriteInt(ms, 4);
                WriteInt(ms, machine.MIDIInputChannel);

                WriteString(ms, "OverrideDelay");
                WriteInt(ms, 4);
                WriteInt(ms, machine.OverrideLatency);

                WriteString(ms, "OversampleFactor");
                WriteInt(ms, 4);
                WriteInt(ms, machine.OversampleFactor);

                WriteString(ms, "Wireless");
                WriteInt(ms, 1);
                WriteBool(ms, machine.IsWireless);
            }
            AddSection(ms.ToArray(), SectionType.XCAM);
        }

        void CreateXQESSection()
        {
            MemoryStream ms = new MemoryStream();
            var song = buzz.SongCore;

            int numSequences = song.Sequences.Count;
            WriteInt(ms, numSequences);

            for (int i = 0; i < numSequences; i++)
            {
                var seq = song.Sequences[i];
                WriteInt(ms, 1);                    // Number of properties

                WriteString(ms, "IsDisabled");
                WriteInt(ms, 1);                    // Size
                WriteBool(ms, seq.IsDisabled);
            }
            AddSection(ms.ToArray(), SectionType.XQES);
        }

        void CreateWaveTableSection()
        {
            MemoryStream ms = new MemoryStream();

            var wt = buzz.SongCore.WavetableCore;
            ushort waveCount = (ushort)wt.Waves.Where(w => w != null).Count();
            WriteUShort(ms, waveCount);
            foreach (WaveCore wave in wt.WavesList.Where(w => w != null))
            {
                WriteUShort(ms, (ushort)wave.Index);
                WriteString(ms, wave.FileName);
                WriteString(ms, wave.Name);
                WriteFloat(ms, wave.Volume);

                WriteByte(ms, (byte)wave.Flags);

                if (((int)wave.Flags & WaveFlagsEnvelope) != 0)
                {
                    // No envelope support atm
                    // 0 envelopes
                    WriteUShort(ms, 0);
                }

                byte waveLeyers = (byte)wave.LayersList.Count;
                WriteByte(ms, waveLeyers);

                for (int j = 0; j < waveLeyers; j++)
                {
                    WaveLayerCore waveLayer = wave.LayersList[j];

                    WriteInt(ms, waveLayer.SampleCount16Bit);
                    WriteInt(ms, waveLayer.LoopStart16Bit);
                    WriteInt(ms, waveLayer.LoopEnd16Bit);
                    WriteInt(ms, waveLayer.SampleRate);
                    WriteByte(ms, (byte)waveLayer.RootNote);
                }
            }

            AddSection(ms.ToArray(), SectionType.WAVT);
        }

        void CreateWavesSection()
        {
            MemoryStream ms = new MemoryStream();

            var wt = buzz.SongCore.WavetableCore;
            var waves = wt.WavesList.Where(w => w != null);
            ushort waveCount = (ushort)waves.Count();
            WriteUShort(ms, waveCount);

            foreach (var wave in waves)
            {
                // Write index
                WriteUShort(ms, (ushort)wave.Index);

                // Write Format. 0 == 16 bit uncompressed
                WriteByte(ms, 0);

                WaveFlags waveflags = wave.Flags;
                int numchannels = waveflags.HasFlag(WaveFlags.Stereo) ? 2 : 1;
                int numlevels = wave.Layers.Count;

                uint totalBytes = 0;

                foreach (var layer in wave.LayersList)
                {
                    totalBytes += (uint)(layer.SampleCount16Bit * 2 * layer.ChannelCount);
                }

                WriteUInt(ms, totalBytes);

                for (int j = 0; j < numlevels; j++)
                {
                    WaveLayerCore waveLayer = wave.LayersList[j];
                    byte[] buffer = waveLayer.GetRawByteData(); 

                    if (waveflags.HasFlag(WaveFlags.Not16Bit) && waveLayer.LoopStart16Bit == 4)
                    {   
                        buffer[0] = (byte)waveLayer.Format; // Extended wave --> Format byte
                    }

                    WriteBytes(ms, buffer);
                }
            }

            AddSection(ms.ToArray(), SectionType.WAVE);
        }

        void CreateFileInfoSection()
        {
            MemoryStream ms = new MemoryStream();
            var txt = buzz.BuildString;
            WriteString(ms, txt);

            AddSection(ms.ToArray(), SectionType.REVB);
        }

        void CreateInfoTextSection()
        {
            MemoryStream ms = new MemoryStream();
            var txt = buzz.InfoText == null ? "" : buzz.InfoText;
            byte[] buffer = Encoding.UTF8.GetBytes(txt);
            uint lenght = (uint)buffer.Length;

            WriteUInt(ms, lenght);
            WriteBytes(ms, buffer);

            AddSection(ms.ToArray(), SectionType.BLAH);
        }

        private void CreateDialogPropertiesSection()
        {
            MemoryStream ms = new MemoryStream();
            WriteByte(ms, 1);           // Version

            foreach (var m in buzz.Song.Machines)
            {
                var machine = m as MachineCore;
                var pw = machine.ParameterWindow;
                var mGUI = machine.MachineGUIWindow;

                bool parameterWindowVisible = pw != null && pw.Visibility == System.Windows.Visibility.Visible;
                bool guiWindowVisible = mGUI != null && mGUI.Visibility == System.Windows.Visibility.Visible;

                if (parameterWindowVisible || guiWindowVisible)
                {
                    WriteString(ms, machine.Name);

                    WriteInt(ms, 0x2c);                         // flags?
                    WriteInt(ms, 0);                            // ?
                    WriteInt(ms, 1);                            // ?

                    if (guiWindowVisible)
                    {
                        WriteInt(ms, (int)mGUI.Left);                                     // Left
                        WriteInt(ms, (int)mGUI.Top);                                      // Top
                        WriteInt(ms, (int)(mGUI.Left + mGUI.ActualWidth));                  // Right
                        WriteInt(ms, (int)(mGUI.Top + mGUI.ActualHeight));                  // Bottom
                    }
                    else
                    {
                        WriteInt(ms, -1);                           // Left?
                        WriteInt(ms, -1);                           // Top?
                        WriteInt(ms, -1);                           // Right?
                        WriteInt(ms, -1);                           // Bottom?
                    }

                    if (parameterWindowVisible)
                    {
                        WriteInt(ms, (int)pw.Left);                                     // Left
                        WriteInt(ms, (int)pw.Top);                                      // Top
                        WriteInt(ms, (int)(pw.Left + pw.ActualWidth));                  // Right
                        WriteInt(ms, (int)(pw.Top + pw.ActualHeight));                  // Bottom
                    }
                    else
                    {
                        WriteInt(ms, -1);                           // Left?
                        WriteInt(ms, -1);                           // Top?
                        WriteInt(ms, -1);                           // Right?
                        WriteInt(ms, -1);                           // Bottom?
                    }
                }
            }
            WriteByte(ms, 0); // End

            AddSection(ms.ToArray(), SectionType.GLDP);
        }

        void AddSection(byte[] data, SectionType magic)
        {
            Section section = new Section();
            section.Data = data;
            section.Magic = (uint)magic;
            section.Size = (uint)section.Data.Length;
            sections[magic] = section;
        }

        private MachineCore GetMachine(string name)
        {
            MachineCore machine = machines.FirstOrDefault(m => m.Name == name);
            if (machine == null)
            {
                machine = new MachineCore(buzz.SongCore, buzzPath, dispatcher);
                machine.Name = name;
                machines.Add(machine);
            }

            return machine;
        }

        public static int ReadInt(FileStream fs)
        {
            byte[] buffer = new byte[sizeof(int)];

            fs.Read(buffer, 0, sizeof(int));
            return BitConverter.ToInt32(buffer, 0);
        }

        public static uint ReadUInt(FileStream fs)
        {
            byte[] buffer = new byte[sizeof(int)];

            fs.Read(buffer, 0, sizeof(int));
            return BitConverter.ToUInt32(buffer, 0);
        }

        public static ushort ReadUShort(FileStream fs)
        {
            byte[] buffer = new byte[sizeof(ushort)];

            fs.Read(buffer, 0, sizeof(ushort));
            return BitConverter.ToUInt16(buffer, 0);
        }

        public static float ReadFloat(FileStream fs)
        {
            byte[] buffer = new byte[sizeof(float)];

            fs.Read(buffer, 0, sizeof(float));
            return BitConverter.ToSingle(buffer, 0);
        }

        public static byte ReadByte(FileStream fs)
        {
            byte[] buffer = new byte[sizeof(byte)];

            fs.Read(buffer, 0, sizeof(byte));
            return buffer[0];
        }

        public static ulong ReadULong(FileStream fs)
        {
            byte[] buffer = new byte[sizeof(ulong)];

            fs.Read(buffer, 0, sizeof(ulong));
            return BitConverter.ToUInt64(buffer, 0);
        }

        public static ulong ReadVal(FileStream fs, int size)
        {
            switch (size)
            {
                case 1:
                    return ReadByte(fs);
                case 2:
                    return ReadUShort(fs);
                case 4:
                    return ReadUInt(fs);
                case 8:
                    return ReadULong(fs);
                default:
                    return 0;
            }
        }

        public static string ReadString(FileStream fs)
        {
            string ret = "";

            do
            {
                char c = (char)fs.ReadByte();
                if (c == 0)
                    break;
                else if (c == -1)
                {
                    ret = null;
                    break;
                }
                else
                    ret += c;
            } while (true);

            return ret;
        }

        public static byte[] ReadBytes(FileStream fs, int length)
        {
            byte[] buffer = new byte[length];

            fs.Read(buffer, 0, length);
            return buffer;
        }

        public static void WriteUInt(MemoryStream ms, uint value)
        {
            byte[] buffer = null;
            buffer = BitConverter.GetBytes(value);
            ms.Write(buffer, 0, buffer.Length);
        }

        public static void WriteInt(MemoryStream ms, int value)
        {
            byte[] buffer = null;
            buffer = BitConverter.GetBytes(value);
            ms.Write(buffer, 0, buffer.Length);
        }

        public static void WriteUShort(MemoryStream ms, ushort value)
        {
            byte[] buffer = null;
            buffer = BitConverter.GetBytes(value);
            ms.Write(buffer, 0, buffer.Length);
        }

        public static void WriteBool(MemoryStream ms, bool value)
        {
            WriteByte(ms, value == true ? (byte)1 : (byte)0);
        }

        public static void WriteBytes(MemoryStream ms, byte[] buffer)
        {
            ms.Write(buffer, 0, buffer.Length);
        }

        public static void WriteFloat(MemoryStream ms, float value)
        {
            byte[] buffer = null;
            buffer = BitConverter.GetBytes(value);
            ms.Write(buffer, 0, buffer.Length);
        }

        public static void WriteByte(MemoryStream ms, byte value)
        {
            ms.WriteByte(value);
        }

        public static void WriteString(MemoryStream ms, string str)
        {
            byte[] buffer = Encoding.ASCII.GetBytes(str);
            ms.Write(buffer, 0, buffer.Length);
            WriteByte(ms, 0);
        }

        public Dictionary<string, MemoryStream> GetSubSections()
        {
            return subSections.Values.Select(s => new { s.Name, Value = new MemoryStream(s.Data, false) }).ToDictionary(s => s.Name, s => s.Value);
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
    }
}

