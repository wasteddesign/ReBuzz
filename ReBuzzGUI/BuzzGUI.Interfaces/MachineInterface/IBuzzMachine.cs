namespace Buzz.MachineInterface
{
    public interface IBuzzMachine
    {
        // Buzz uses reflection and delegates to access the machine's functions so there's only documentation here
        // {U} = ui thread, {A} = an audio thread, {M} = midi thread

        // Declaration:
        // [MachineDecl] public <any class name> : IBuzzMachine

        // Constructor:
        // {U} public <class name>(IBuzzMachineHost host);

        // Parameter properties:
        // {A} [ParameterDecl] public <bool | int | float | Interpolator> <parameter name> { get; set; }
        // Interpolator parameters must be new'd in the constructor and never changed

        // Generator Work functions:
        // {A} public void Work();		// control machine
        // {A} public Sample Work();
        // {A} public bool Work(Sample[] output, int n, WorkModes mode);

        // Effect Work functions:
        // {A} public Sample Work(Sample s);
        // {A} public bool Work(Sample[] output, Sample[] input, int n, WorkModes mode);

        // Machine state that is saved in files (CMachineInteface::Init, CMachineInteface::Load, CMachineInteface::Save):
        // {U} public <any class name> MachineState { get; set; }

        // Right-click menu commands:
        // {U} public IEnumerable<IMenuItem> Commands { get; }

        // Other functions:
        // {U} public void Stop();
        // {U} public void ImportFinished(IDictionary<string, string> machineNameMap);
        // {M} public void MidiNote(int const channel, int const value, int const velocity);
        // {M} public void MidiControlChange(int const ctrl, int const channel, int const value);
        // {A} public int GetLatency();

        // Update 1
        // {U} public string DescribeValue(IParameter parameter, int value); // Return null get ParameterDecl values
        // {U} public string GetChannelName(bool input, int index);
        // {A} public bool Work(IList<Sample[]> output, int n, WorkModes mode);
        // {A} public bool Work(IList<Sample[]> output, IList<Sample[]> input, int n, WorkModes mode);

        // Pattern Editor
        // {U} public UserControl PatternEditorControl();
        // {U} public void SetEditorPattern(IPattern pattern);
        // {M} void RecordControlChange(IParameter parameter, int track, int value);
        // {U} public void CreatePatternCopy(IPattern pnew, IPattern p);
        // {U} void SetPatternEditorData(byte[] data);
        // {U} public void byte[] GetPatternEditorData();
        // {U} public bool CanExecuteCommand(BuzzCommand cmd);
        // {U} public void ExecuteCommand(BuzzCommand cmd);
        // {U} public void UpdateWaveReferences(IPattern pattern, Dictionary<int, int> remap)
    }
}
