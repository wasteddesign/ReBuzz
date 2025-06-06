using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.Common.Templates;
using BuzzGUI.Interfaces;
using ReBuzz.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Serialization;

namespace ReBuzz.ManagedMachine
{
    internal class ManagedMachineHost : IBuzzMachineHost
    {
        private ManagedMachineDLL dll;

        private delegate void ControlWorkDelegate();

        private delegate Sample GeneratorWorkDelegate();

        private delegate Sample EffectWorkDelegate(Sample s);

        private delegate bool GeneratorBlockWorkDelegate(Sample[] output, int n, WorkModes mode);

        private delegate bool GeneratorBlockWorkMultiDelegate(IList<Sample[]> output, int n, WorkModes mode);

        private delegate bool EffectBlockWorkDelegate(Sample[] output, Sample[] input, int n, WorkModes mode);

        private delegate bool EffectBlockWorkMultiDelegate(IList<Sample[]> output, IList<Sample[]> input, int n, WorkModes mode);

        private delegate void StopDelegate();

        private delegate void ImportFinishedDelegate(IDictionary<string, string> d);

        private delegate void MidiNoteDelegate(int channel, int value, int velocity);

        private delegate void MidiControlChangeDelegate(int ctrl, int channel, int value);

        private delegate int GetLatencyDelegate();

        private delegate string DescribeValueDelegate(IParameter parameter, int value);

        private delegate string GetChannelNameDelegate(bool input, int index);

        #region Pattern Editor
        private delegate System.Windows.Controls.UserControl PatternEditorControlDelegate();


        //For machines that do not support .NET 9.0 WPF (such as CLR), and will return
        //a .NET Framework 4.x WinForms UserControl instead.
        private delegate System.Windows.Forms.UserControl OldPatternEditorControlDelegate();

        private delegate void SetEditorPatternDelegate(IPattern pattern);

        private delegate void RecordControlChangeDelegate(IParameter parameter, int track, int value);

        private delegate void CreatePatternCopyDelegate(IPattern pnew, IPattern p);

        private delegate void SetPatternEditorDataDelegate(byte[] data);

        private delegate byte[] GetPatternEditorDataDelegate();

        private delegate bool CanExecuteCommandDelegate(BuzzCommand cmd);

        private delegate void ExecuteCommandDelegate(BuzzCommand cmd);

        private delegate int[] GetPatternEditorMachineMIDIEventsDelegate(IPattern pattern);

        private delegate void SetPatternEditorMachineMIDIEventsDelegate(IPattern pattern, int[] data);

        private delegate IEnumerable<IPatternEditorColumn> GetPatternEditorEventsDelegate(IPattern pattern, int tbegin, int tend);

        private delegate void ActivateDelegate();

        private delegate void ReleaseDelegate();

        private delegate int GetTicksPerBeatDelegate(IPattern pattern, int playPosition);

        private delegate void UpdateWaveReferencesDelegate(IPattern patten, IDictionary<int, int> remap);
        #endregion

        private ControlWorkDelegate ControlWork;

        private GeneratorWorkDelegate GeneratorWork;

        private EffectWorkDelegate EffectWork;

        private GeneratorBlockWorkDelegate GeneratorBlockWork;

        private GeneratorBlockWorkMultiDelegate GeneratorBlockWorkMulti;

        private EffectBlockWorkDelegate EffectBlockWork;

        private EffectBlockWorkMultiDelegate EffectBlockWorkMulti;

        private StopDelegate StopFunction;

        private ImportFinishedDelegate ImportFinishedFunction;

        private MidiNoteDelegate MidiNoteFunction;

        private MidiControlChangeDelegate MidiControlChangeFunction;

        private GetLatencyDelegate GetLatencyFunction;

        private DescribeValueDelegate DescribeValueFunction;

        private GetChannelNameDelegate GetChannelNameFunction;

        #region Pattern Editor

        private PatternEditorControlDelegate PatternEditorControlFunction;

        private SetEditorPatternDelegate SetEditorPatternFunction;

        private RecordControlChangeDelegate RecordControlChangeFunction;

        private CreatePatternCopyDelegate CreatePatternCopyFunction;

        private SetPatternEditorDataDelegate SetPatternEditorDataFunction;

        private GetPatternEditorDataDelegate GetPatternEditorDataFunction;

        private CanExecuteCommandDelegate CanExecuteCommandFunction;

        private ExecuteCommandDelegate ExecuteCommandFunction;

        private GetPatternEditorMachineMIDIEventsDelegate GetPatternEditorMachineMIDIEventsFunction;

        private SetPatternEditorMachineMIDIEventsDelegate SetPatternEditorMachineMIDIEventsFunction;

        private GetPatternEditorEventsDelegate GetPatternEditorEventsFunction;

        private ActivateDelegate ActivateFunction;

        private ReleaseDelegate ReleaseFunction;

        private GetTicksPerBeatDelegate GetTicksPerBeatFunction;

        private UpdateWaveReferencesDelegate UpdateWaveReferencesFunction;

        #endregion

        private IBuzzMachine machine;

        private readonly MachineParameter.Delegates[] parameterDelegates;

        private Sample[] inputBuffer;

        private Sample[] outputBuffer;

        public IMachine Machine { get; set; }

        public MasterInfo MasterInfo { get; private set; }

        public SubTickInfo SubTickInfo { get; private set; }

        public IBuzzMachine ManagedMachine => machine;

        public ManagedMachineHost(ManagedMachineDLL dll)
        {
            this.dll = dll;
            MasterInfo = new MasterInfo();
            SubTickInfo = new SubTickInfo();
            machine = dll.CreateMachine(this);
            CreateDelegates();
            parameterDelegates = dll.CreateParameterDelegates(machine);
        }

        private void CreateDelegates()
        {
            MethodInfo method = dll.machineType.GetMethod("Work");
            switch (dll.WorkFunctionType)
            {
                case ManagedMachineDLL.WorkFunctionTypes.Control:
                    ControlWork = Delegate.CreateDelegate(typeof(ControlWorkDelegate), machine, method) as ControlWorkDelegate;
                    break;
                case ManagedMachineDLL.WorkFunctionTypes.Generator:
                    GeneratorWork = Delegate.CreateDelegate(typeof(GeneratorWorkDelegate), machine, method) as GeneratorWorkDelegate;
                    break;
                case ManagedMachineDLL.WorkFunctionTypes.Effect:
                    EffectWork = Delegate.CreateDelegate(typeof(EffectWorkDelegate), machine, method) as EffectWorkDelegate;
                    break;
                case ManagedMachineDLL.WorkFunctionTypes.GeneratorBlock:
                    GeneratorBlockWork = Delegate.CreateDelegate(typeof(GeneratorBlockWorkDelegate), machine, method) as GeneratorBlockWorkDelegate;
                    outputBuffer = new Sample[256];
                    break;
                case ManagedMachineDLL.WorkFunctionTypes.GeneratorBlockMulti:
                    GeneratorBlockWorkMulti = Delegate.CreateDelegate(typeof(GeneratorBlockWorkMultiDelegate), machine, method) as GeneratorBlockWorkMultiDelegate;
                    outputBuffer = new Sample[256];
                    break;
                case ManagedMachineDLL.WorkFunctionTypes.EffectBlock:
                    EffectBlockWork = Delegate.CreateDelegate(typeof(EffectBlockWorkDelegate), machine, method) as EffectBlockWorkDelegate;
                    inputBuffer = new Sample[256];
                    outputBuffer = new Sample[256];
                    break;
                case ManagedMachineDLL.WorkFunctionTypes.EffectBlockMulti:
                    EffectBlockWorkMulti = Delegate.CreateDelegate(typeof(EffectBlockWorkMultiDelegate), machine, method) as EffectBlockWorkMultiDelegate;
                    inputBuffer = new Sample[256];
                    outputBuffer = new Sample[256];
                    break;
            }

            StopFunction = (StopDelegate)GetMethod(typeof(StopDelegate), "Stop");
            ImportFinishedFunction = (ImportFinishedDelegate)GetMethod(typeof(ImportFinishedDelegate), "ImportFinished");
            MidiNoteFunction = (MidiNoteDelegate)GetMethod(typeof(MidiNoteDelegate), "MidiNote");
            MidiControlChangeFunction = (MidiControlChangeDelegate)GetMethod(typeof(MidiControlChangeDelegate), "MidiControlChange");
            GetLatencyFunction = (GetLatencyDelegate)GetMethod(typeof(GetLatencyDelegate), "GetLatency");
            //Take into consideration that some more native machines may want to return System.Windows.Forms.UserControl.
            //In this situation, we can use NativeMachineFrameworkUI to wrap around the System.Windows.Forms.UserControl
            try
            {
                PatternEditorControlFunction = (PatternEditorControlDelegate)GetMethod(typeof(PatternEditorControlDelegate), "PatternEditorControl");
            }
            catch(System.ArgumentException) //This is thrown because the return types are not the same
            {
                //Try the System.Windows.Forms.UserControl delegate instead
                OldPatternEditorControlDelegate oldDelegate = (OldPatternEditorControlDelegate)GetMethod(typeof(OldPatternEditorControlDelegate), "PatternEditorControl");
                if(oldDelegate != null)
                {
                    //Create an inline delegate to wrap around the old System.Windows.Forms.UserControl
                    PatternEditorControlFunction = () =>
                    {
                        var control = oldDelegate();
                        return new NativeMachineFrameworkUI.WinFormsControl(control);
                    };
                }
                else
                {
                    //Also not found - throw the original exception
                    throw;
                }
            }
            
            SetEditorPatternFunction = (SetEditorPatternDelegate)GetMethod(typeof(SetEditorPatternDelegate), "SetEditorPattern");
            RecordControlChangeFunction = (RecordControlChangeDelegate)GetMethod(typeof(RecordControlChangeDelegate), "RecordControlChange");
            CreatePatternCopyFunction = (CreatePatternCopyDelegate)GetMethod(typeof(CreatePatternCopyDelegate), "CreatePatternCopy");
            GetChannelNameFunction = (GetChannelNameDelegate)GetMethod(typeof(GetChannelNameDelegate), "GetChannelName");
            SetPatternEditorDataFunction = (SetPatternEditorDataDelegate)GetMethod(typeof(SetPatternEditorDataDelegate), "SetPatternEditorData");
            GetPatternEditorDataFunction = (GetPatternEditorDataDelegate)GetMethod(typeof(GetPatternEditorDataDelegate), "GetPatternEditorData");
            CanExecuteCommandFunction = (CanExecuteCommandDelegate)GetMethod(typeof(CanExecuteCommandDelegate), "CanExecuteCommand");
            ExecuteCommandFunction = (ExecuteCommandDelegate)GetMethod(typeof(ExecuteCommandDelegate), "ExecuteCommand");
            GetPatternEditorMachineMIDIEventsFunction = (GetPatternEditorMachineMIDIEventsDelegate)GetMethod(typeof(GetPatternEditorMachineMIDIEventsDelegate), "GetPatternEditorMachineMIDIEvents");
            SetPatternEditorMachineMIDIEventsFunction = (SetPatternEditorMachineMIDIEventsDelegate)GetMethod(typeof(SetPatternEditorMachineMIDIEventsDelegate), "SetPatternEditorMachineMIDIEvents");
            GetPatternEditorEventsFunction = (GetPatternEditorEventsDelegate)GetMethod(typeof(GetPatternEditorEventsDelegate), "GetPatternEditorEvents");
            DescribeValueFunction = (DescribeValueDelegate)GetMethod(typeof(DescribeValueDelegate), "DescribeValue");
            ActivateFunction = (ActivateDelegate)GetMethod(typeof(ActivateDelegate), "Activate");
            ReleaseFunction = (ReleaseDelegate)GetMethod(typeof(ReleaseDelegate), "Release");
            GetTicksPerBeatFunction = (GetTicksPerBeatDelegate)GetMethod(typeof(GetTicksPerBeatDelegate), "GetTicksPerBeat");
            UpdateWaveReferencesFunction = (UpdateWaveReferencesDelegate)GetMethod(typeof(UpdateWaveReferencesDelegate), "UpdateWaveReferences");
        }

        public IEnumerable<IMenuItem> Commands
        {
            get
            {
                PropertyInfo property = dll.machineType.GetProperty("Commands");
                if (property == null)
                {
                    return null;
                }
                MethodInfo getMethod = property.GetGetMethod();
                if (getMethod == null)
                {
                    return null;
                }
                object obj = getMethod.Invoke(machine, null);
                return obj as IEnumerable<IMenuItem>;
            }
        }

        public int Latency
        {
            get
            {
                if (GetLatencyFunction == null)
                {
                    return 0;
                }
                return GetLatencyFunction();
            }
        }

        public UserControl PatternEditorControl
        {
            get
            {
                if (PatternEditorControlFunction == null)
                {
                    return null;
                }
                return PatternEditorControlFunction();
            }
        }

        public IPattern SetEditorPattern
        {
            set
            {
                if (SetEditorPatternFunction != null)
                {
                    SetEditorPatternFunction(value);
                }
            }
        }

        public byte[] MachineState
        {
            get
            {
                PropertyInfo property = dll.machineType.GetProperty("MachineState");
                if (property == null)
                {
                    return null;
                }
                MethodInfo getMethod = property.GetGetMethod();
                if (getMethod == null)
                {
                    return null;
                }

                object obj = getMethod.Invoke(machine, null);

                if (obj == null)
                    return null;

                using (var ms = new MemoryStream())
                {
                    XmlWriterSettings xmlWriterSettings = new XmlWriterSettings();
                    xmlWriterSettings.NamespaceHandling = NamespaceHandling.OmitDuplicates;
                    xmlWriterSettings.NewLineOnAttributes = false;
                    xmlWriterSettings.Indent = true;
                    XmlWriterSettings settings = xmlWriterSettings;
                    XmlWriter xmlWriter = XmlWriter.Create(ms, settings);
                    XmlSerializer xmlSerializer = new XmlSerializer(obj.GetType());
                    xmlSerializer.Serialize(xmlWriter, obj);
                    xmlWriter.Close();
                    return ms.ToArray();
                }
            }
            set
            {
                PropertyInfo property = dll.machineType.GetProperty("MachineState");
                if (property == null)
                {
                    return;
                }
                MethodInfo setMethod = property.GetSetMethod();
                if (setMethod == null)
                {
                    return;
                }

                if (value == null || value.Length == 0)
                {
                    // Create new instance and set it
                    //object machineStatePropertyInstance = Activator.CreateInstance(property.PropertyType);
                    //property.SetValue(machine, machineStatePropertyInstance, null);
                }
                else
                {
                    using (var ms = new MemoryStream(value))
                    {
                        XmlSerializer xmlSerializer = new XmlSerializer(property.PropertyType);
                        //MemoryStream input = new MemoryStream(value);
                        XmlReader xmlReader = XmlReader.Create(ms);
                        object obj = xmlSerializer.Deserialize(xmlReader);
                        xmlReader.Close();
                        setMethod.Invoke(machine, new object[1] { obj });
                    }
                }
            }
        }

        public bool IsControlMachine { get => ControlWork != null; }

        public void RecordControlChange(IParameter parameter, int track, int value)
        {
            if (RecordControlChangeFunction != null)
            {
                RecordControlChangeFunction(parameter, track, value);
            }
        }

        private Delegate GetMethod(Type t, string name)
        {
            MethodInfo method = dll.machineType.GetMethod(name);
            if (method == null)
            {
                return null;
            }
            return Delegate.CreateDelegate(t, machine, method);
        }


        public void SetParameterValue(int index, int track, int value)
        {
            if (index < dll.globalParameters.Length)
            {
                dll.globalParameters[index].SetValue(this, parameterDelegates[index], -1, value);
            }
            else
            {
                dll.trackParameters[index - dll.globalParameters.Length].SetValue(this, parameterDelegates[index], track, value);
            }
        }

        public unsafe void SetParameters(int* p, int n)
        {
            for (int i = 0; i < n; i++)
            {
                SetParameterValue(p[2 * i] & 0xFFFF, p[2 * i] >> 16, p[2 * i + 1]);
            }
        }

        public bool MultiWork(IList<Sample[]> samplesOut, IList<Sample[]> samplesIn, int n, WorkModes mode)
        {
            bool flag = false;
            if (EffectBlockWorkMulti != null)
            {
                flag = EffectBlockWorkMulti(samplesOut, samplesIn, n, mode);
            }
            else if (GeneratorBlockWorkMulti != null)
            {
                flag = GeneratorBlockWorkMulti(samplesOut, n, mode);
            }
            return flag;
        }

        public bool Work(Sample[] samples, int n, WorkModes mode)
        {
            if (ControlWork != null)
            {
                ControlWork();
                return false;
            }
            else if (GeneratorBlockWork != null)
            {
                Array.Clear(outputBuffer, 0, outputBuffer.Length);
                bool flag = GeneratorBlockWork(outputBuffer, n, mode);
                if (flag && (mode & WorkModes.WM_WRITE) != 0)
                {
                    for (int i = 0; i < n; i++)
                    {
                        samples[i] = outputBuffer[i];
                    }
                }
                return flag;
            }
            else if (EffectBlockWork != null)
            {
                if ((mode & WorkModes.WM_READ) != 0)
                {
                    for (int j = 0; j < n; j++)
                    {
                        inputBuffer[j] = samples[j];
                    }
                }
                bool flag2 = EffectBlockWork(outputBuffer, inputBuffer, n, mode);
                if (flag2 && (mode & WorkModes.WM_WRITE) != 0)
                {
                    for (int k = 0; k < n; k++)
                    {
                        samples[k] = outputBuffer[k];
                    }
                }
                return flag2;
            }
            else if (GeneratorWork != null)
            {
                for (int l = 0; l < n; l++)
                {
                    Sample sample = GeneratorWork();
                    samples[l] = sample;
                    MasterInfo.PosInTick++;
                    SubTickInfo.PosInSubTick++;
                }
                return true;
            }
            else if (EffectWork != null)
            {
                Sample s = default;
                if (samples != null)
                {
                    for (int m = 0; m < n; m++)
                    {
                        s = samples[m];
                        s = EffectWork(s);
                        samples[m] = s;
                        MasterInfo.PosInTick++;
                        SubTickInfo.PosInSubTick++;
                    }
                }
                else
                {
                    for (int num = 0; num < n; num++)
                    {
                        s.L = 0f;
                        s.R = 0f;
                        s = EffectWork(s);
                        samples[num] = s;
                        MasterInfo.PosInTick++;
                        SubTickInfo.PosInSubTick++;
                    }
                }
                return true;
            }
            return false;
        }

        public string DescribeValue(int index, int value)
        {
            if (index < dll.globalParameters.Length)
            {
                return dll.globalParameters[index].DescribeValue(this, parameterDelegates[index], value);
            }
            return dll.trackParameters[index - dll.globalParameters.Length].DescribeValue(this, parameterDelegates[index], value);
        }

        public int MsToSamples(float t)
        {
            return (int)(MasterInfo.SamplesPerSec * (t / 1000f));
        }

        public void Stop()
        {
            if (StopFunction != null)
            {
                StopFunction();
            }
        }

        public void MidiNote(int channel, int value, int velocity)
        {
            if (MidiNoteFunction != null)
            {
                MidiNoteFunction(channel, value, velocity);
            }
        }

        public void MidiControlChange(int ctrl, int channel, int value)
        {
            if (MidiControlChangeFunction != null)
            {
                MidiControlChangeFunction(ctrl, channel, value);
            }
        }

        public void ImportFinished(IDictionary<string, string> d)
        {
            if (ImportFinishedFunction != null)
            {
                ImportFinishedFunction(d);
            }
        }

        public bool IsValidAsciiChar(int index, char ch)
        {
            if (index < 0 || index >= dll.globalParameters.Length + dll.trackParameters.Length)
            {
                return false;
            }
            if (index < dll.globalParameters.Length)
            {
                return dll.globalParameters[index].IsValidAsciiChar(ch);
            }
            return dll.trackParameters[index - dll.globalParameters.Length].IsValidAsciiChar(ch);
        }

        internal void SetParameters()
        {
            int paramIndex = 0;
            var machineGlobaPG = Machine.ParameterGroups[1];
            for (int i = 0; i < machineGlobaPG.Parameters.Count; i++)
            {
                var val = machineGlobaPG.Parameters[i].GetValue(0);
                SetParameterValue(paramIndex, 0, val);

                if (!machineGlobaPG.Parameters[i].Flags.HasFlag(ParameterFlags.State))
                {
                    //machinepg.Parameters[i].SetValue(0, machinepg.Parameters[i].NoValue);
                }
                paramIndex++;
            }

            var machineTrackPG = Machine.ParameterGroups[2];
            for (int i = 0; i < machineTrackPG.Parameters.Count; i++)
            {
                for (int t = 0; t < machineTrackPG.TrackCount; t++)
                {
                    SetParameterValue(paramIndex, t, machineTrackPG.Parameters[i].GetValue(t));

                    if (!machineTrackPG.Parameters[i].Flags.HasFlag(ParameterFlags.State))
                    {
                        //trackpg.Parameters[i].SetValue(t, trackpg.Parameters[i].NoValue);
                    }
                }
                paramIndex++;
            }
        }

        internal void CreatePatternCopy(IPattern newp, IPattern p)
        {
            if (CreatePatternCopyFunction != null)
            {
                CreatePatternCopyFunction(newp, p);
            }
        }

        internal string GetChannelName(bool input, int index)
        {
            if (GetChannelNameFunction != null)
            {
                return GetChannelNameFunction(input, index);
            }

            return null;
        }

        internal void SetPatternEditorData(byte[] data)
        {
            if (SetPatternEditorDataFunction != null)
            {
                SetPatternEditorDataFunction(data);
            }
        }

        internal byte[] GetPatternEditorData()
        {
            if (GetPatternEditorDataFunction != null)
            {
                return GetPatternEditorDataFunction();
            }

            return null;
        }

        public bool CanExecuteCommad(BuzzCommand cmd)
        {
            if (CanExecuteCommandFunction != null)
            {
                return CanExecuteCommandFunction(cmd);
            }

            return false;
        }

        public void ExecuteCommad(BuzzCommand cmd)
        {
            if (ExecuteCommandFunction != null)
            {
                ExecuteCommandFunction(cmd);
            }

            return;
        }

        internal int[] GetPatternEditorMachineMIDIEvents(PatternCore pattern)
        {
            if (GetPatternEditorMachineMIDIEventsFunction != null)
            {
                return GetPatternEditorMachineMIDIEventsFunction(pattern);
            }
            else return new int[0];
        }

        internal void SetPatternEditorMachineMIDIEvents(PatternCore pattern, int[] data)
        {
            if (SetPatternEditorMachineMIDIEventsFunction != null)
            {
                SetPatternEditorMachineMIDIEventsFunction(pattern, data);
            }
        }

        internal IEnumerable<IPatternEditorColumn> GetPatternCloumnEvents(PatternCore pattern, int tbegin, int tend)
        {
            if (GetPatternEditorEventsFunction != null)
            {
                return GetPatternEditorEventsFunction(pattern, tbegin, tend);
            }
            else return new List<IPatternEditorColumn>();
        }

        internal string DescribeParameterValue(int index, int value)
        {
            if (DescribeValueFunction != null)
            {
                var parameter = Machine.AllNonInputParameters().ElementAt(index);
                string str = DescribeValueFunction(parameter, value);
                if (str != null)
                    return str;
                else
                    return DescribeValue(index, value);
            }
            else
            {
                return DescribeValue(index, value);
            }

        }

        internal void Activate()
        {
            if (ActivateFunction != null)
            {
                ActivateFunction();
            }
        }

        internal void Release()
        {
            if (ReleaseFunction != null)
            {
                ReleaseFunction();
            }

            //For CLR machines, or machines that are Disposable, 
            //the Dispose() method must be called to properlly free up resources
            //used by that machine (such as native resources)
            if(machine is IDisposable)
            {
                (machine as IDisposable).Dispose();
            }
            machine = null;
            dll = null;
        }

        internal unsafe void Tick(MachineCore machine)
        {
            foreach (var paramTrack in machine.parametersChanged)
            {
                var par = paramTrack.Key;
                if (par.Group.Type != ParameterGroupType.Input)
                {
                    var track = paramTrack.Value;
                    int index = par.Group.Type == ParameterGroupType.Global ? par.IndexInGroup : par.IndexInGroup + machine.ParameterGroups[1].Parameters.Count;
                    var val = par.GetValue(track);

                    // Properties need to stay within min/max
                    if (val >= par.MinValue && val <= par.MaxValue)
                    {
                        SetParameterValue(index, track, val);
                    }
                    else if (par.Type == ParameterType.Note && val == BuzzNote.Off)
                    {
                        SetParameterValue(index, track, val);
                    }
                }
            }
        }

        internal void SetParameterDefaults(MachineCore mc)
        {
            ParameterGroup pg = mc.ParameterGroupsList[1];

            foreach (ParameterCore p in pg.Parameters)
            {
                for (int i = 0; i < pg.TrackCount; i++)
                {
                    if (p.Flags.HasFlag(ParameterFlags.State) && p.Type != ParameterType.Note)
                        p.SetValue(i, p.DefValue);
                }
            }

            pg = mc.ParameterGroupsList[2];

            foreach (ParameterCore p in pg.Parameters)
            {
                for (int i = 0; i < pg.TrackCount; i++)
                {
                    if (p.Flags.HasFlag(ParameterFlags.State) && p.Type != ParameterType.Note)
                        p.SetValue(i, p.DefValue);
                }
            }
        }

        internal int GetTicksPerBeat(MachineCore machine, IPattern pattern, int pp)
        {
            if (GetTicksPerBeatFunction != null)
            {
                GetTicksPerBeatFunction(pattern, pp);
            }
            return 4; // Buzz ticks per beat
        }

        internal void UpdateWaveReferences(MachineCore machine, MachineCore editorTargetMachine, Dictionary<int, int> remappedWaveReferences)
        {
            if (UpdateWaveReferencesFunction != null)
            {
                foreach (var pattern in editorTargetMachine.Patterns)
                {
                    UpdateWaveReferencesFunction(pattern, remappedWaveReferences);
                }
            }
        }

        public int OutputChannelCount
        {
            get
            {
                return Machine.OutputChannelCount;
            }
            set
            {
                int outCount = value;
                if (Machine.DLL.Info.Flags.HasFlag(MachineInfoFlags.MULTI_IO) &&
                    !Machine.DLL.Info.Flags.HasFlag(MachineInfoFlags.CONTROL_MACHINE))
                {
                    if (outCount < 1)
                    {
                        outCount = 1;
                    }
                    (Machine as MachineCore).OutputChannelCount = outCount;
                }
            }
        }

        public int InputChannelCount
        {
            get
            {
                return Machine.InputChannelCount;
            }
            set
            {
                int inCount = value;
                if (Machine.DLL.Info.Flags.HasFlag(MachineInfoFlags.MULTI_IO) &&
                    Machine.DLL.Info.Type == MachineType.Effect)
                {
                    if (inCount < 1)
                    {
                        inCount = 1;
                    }
                    (Machine as MachineCore).InputChannelCount = inCount;
                }
            }
        }
    }
}
