using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace BuzzGUI.Interfaces
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PatternEvent
    {
        public const int TimeBase = 240;            // ticks per one buzz tick

        public PatternEvent(int time, int value, int duration = 0) { Time = time; Value = value; Duration = duration; }

        public int Time;                            // 0 <= Time < Pattern.Length * TimeBase
        public int Value;
        public int Duration;                        // duration for midi note on events (if negative, there will be no associated note off event)
    }

    public enum PatternColumnType { Parameter, MIDI };

    public interface IPatternColumn : INotifyPropertyChanged
    {
        PatternColumnType Type { get; }
        IPattern Pattern { get; }
        IMachine Machine { get; set; }              // this can be changed after the column is created if Type == MIDI
        IParameter Parameter { get; }
        int Track { get; }

        //IDictionary<int, int> Guides { get; }		// time, step pairs (similar to sequence editor time signatures)
        ReadOnlyDictionary<int, int> BeatSubdivision { get; }
        IDictionary<string, string> Metadata { get; }

        IEnumerable<PatternEvent> GetEvents(int tbegin, int tend);      // tbegin <= t < tend
        void SetEvents(IEnumerable<PatternEvent> e, bool set);

        void SetBeatSubdivision(int beatindex, int subdiv);

        event Action<IEnumerable<PatternEvent>, bool> EventsChanged;
        event Action<int> BeatSubdivisionChanged;
    }
}
