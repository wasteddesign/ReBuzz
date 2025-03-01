using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BuzzGUI.Interfaces
{
    public enum SongMarkers { LoopStart, LoopEnd, SongEnd };

    public interface ISong : IMachineGraph
    {
        string SongName { get; }
        int PlayPosition { get; set; }
        int LoopStart { get; set; }
        int LoopEnd { get; set; }
        int SongEnd { get; set; }

        ReadOnlyCollection<ISequence> Sequences { get; }
        IWavetable Wavetable { get; }

        IDictionary<string, object> Associations { get; }

        event Action<int, ISequence> SequenceAdded;
        event Action<int, ISequence> SequenceRemoved;
        event Action<int, ISequence> SequenceChanged;

        void AddSequence(IMachine m, int index);
        void RemoveSequence(ISequence s);
        void SwapSequences(ISequence s, ISequence t);
    }
}
