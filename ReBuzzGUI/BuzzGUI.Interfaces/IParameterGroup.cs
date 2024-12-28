using System.Collections.ObjectModel;

namespace BuzzGUI.Interfaces
{
    public enum ParameterGroupType { Input, Global, Track };

    public interface IParameterGroup
    {
        IMachine Machine { get; }
        ParameterGroupType Type { get; }
        ReadOnlyCollection<IParameter> Parameters { get; }
        int TrackCount { get; set; }
    }
}
