namespace BuzzGUI.Interfaces
{
    public interface IMachineMetadata
    {
        ReadOnlyDictionary<IParameter, IParameterMetadata> ParameterMetadata { get; }

    }
}
