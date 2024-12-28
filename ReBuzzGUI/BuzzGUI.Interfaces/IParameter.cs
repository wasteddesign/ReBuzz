using System;
using System.ComponentModel;

namespace BuzzGUI.Interfaces
{
    public enum ParameterType { Note, Switch, Byte, Word, Internal = 127 }
    [Flags]
    public enum ParameterFlags { None = 0, Wave = 1, State = 2, TickOnEdit = 4, TiedToNext = 8, Ascii = 16 }

    public interface IParameter : INotifyPropertyChanged
    {
        IParameterGroup Group { get; }
        int IndexInGroup { get; }

        ParameterType Type { get; }
        string Name { get; }
        string Description { get; }
        int MinValue { get; }
        int MaxValue { get; }
        int NoValue { get; }
        ParameterFlags Flags { get; }
        int DefValue { get; }

        string GetDisplayName(int track);
        int GetValue(int track);
        void SetValue(int track, int value);
        string DescribeValue(int value);
        void BindToMIDIController(int track, int mcindex);
        bool IsValidAsciiChar(int ch);      // ParameterFlags.Ascii

        void SubscribeEvents(int track, Action<IParameter, int> valueChanged, Action<IParameter, int> valueDescriptionChanged);
        void UnsubscribeEvents(int track, Action<IParameter, int> valueChanged, Action<IParameter, int> valueDescriptionChanged);

        //event Action<IParameter> ValueChanged;
        //event Action<IParameter> ValueDescriptionChanged;
    }
}
