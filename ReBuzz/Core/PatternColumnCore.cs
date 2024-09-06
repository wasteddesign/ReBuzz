using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace ReBuzz.Core
{
    internal class PatternColumnCore : IPatternColumn
    {
        public PatternColumnType Type { get; }

        public IPattern Pattern { get; }

        public IMachine Machine
        {
            get;
            set;
        }

        public IParameter Parameter { get; }

        public int Track { get; set; }

        readonly List<PatternEvent> patternEvents = new List<PatternEvent>();

        readonly Dictionary<int, int> beatSubdivision;
        public ReadOnlyDictionary<int, int> BeatSubdivision { get => IDictionaryExtensions.AsReadOnly(beatSubdivision); }

        readonly Dictionary<string, string> metadata = new Dictionary<string, string>();
        public IDictionary<string, string> Metadata { get => metadata; }

        public event Action<IEnumerable<PatternEvent>, bool> EventsChanged;
        public event Action<int> BeatSubdivisionChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public PatternColumnCore(PatternColumnType type, IPattern pattern, IMachine machine, IParameter parameter, int track, Dictionary<int, int> beatSubdivisions, Dictionary<string, string> metadatas)
        {
            Type = type;
            Pattern = pattern;
            Machine = machine;
            Parameter = parameter;
            Track = track;
            beatSubdivision = beatSubdivisions;
            if (beatSubdivision == null)
                beatSubdivision = new Dictionary<int, int>();
            metadata = metadatas;
            if (metadata == null)
                metadata = new Dictionary<string, string>();
        }

        // Returns the events from PAtternColumn object. Not from pattern editor machine.
        public IEnumerable<PatternEvent> GetEvents(int tbegin, int tend)
        {
            return patternEvents.Where(pe => pe.Time >= tbegin && pe.Time < tend).ToArray();
        }

        public void SetBeatSubdivision(int beatindex, int subdiv)
        {
            beatSubdivision[beatindex] = subdiv;
            BeatSubdivisionChanged?.Invoke(beatindex);
        }

        public void SetEvents(IEnumerable<PatternEvent> patternEvents, bool set)
        {
            foreach (var pe in patternEvents)
            {
                if (set)
                    this.patternEvents.Add(pe);
                else
                    this.patternEvents.Remove(pe);
            }
            EventsChanged?.Invoke(patternEvents, set);
        }
    }
}
