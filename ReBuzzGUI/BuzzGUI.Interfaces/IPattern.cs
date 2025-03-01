using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace BuzzGUI.Interfaces
{
    public interface IPattern : INotifyPropertyChanged
    {
        IMachine Machine { get; }
        string Name { get; set;  }
        int Length { get; set; }                                        // length in buzz ticks

        ReadOnlyCollection<IPatternColumn> Columns { get; }

        int PlayPosition { get; }                                       // position in PatternEvent.TimeBase units or int.MinValue if not playing
        bool IsPlayingSolo { get; set; }

        int[] PatternEditorMachineMIDIEvents { get; set; }                   // data from pxp or pianoroll as midi events (CMachineInterfaceEx::ExportMidiEvents)
        IEnumerable<IPatternEditorColumn> PatternEditorMachineEvents { get; }   // data from pxp or pianoroll as events

        void InsertColumn(int index, IParameter p, int track);          // PatternColumnType.Parameter
        void InsertColumn(int index, IMachine m);                       // PatternColumnType.MIDI

        void DeleteColumn(int index);

        void UpdatePEMachineWaveReferences(IDictionary<int, int> map);

        event Action<IPatternColumn> ColumnAdded;
        event Action<IPatternColumn> ColumnRemoved;
        event Action<IPatternColumn> PatternChanged;
        void NotifyPatternChanged();

        IntPtr CPattern { get;  }

        IReadOnlyCollection<ISequence> Sequences { get; }

       event Action<IPattern> OnPatternPlayStart;
       event Action<IPattern> OnPatternPlayPositionChange;
       event Action<IPattern> OnPatternPlayEnd;
    }

    public interface IPatternEditorColumn
    {
        IParameter Parameter { get; }
        IEnumerable<PatternEvent> Events { get; }
    }
}
