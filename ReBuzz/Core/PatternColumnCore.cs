using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;

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

        List<PatternEvent> patternEventsList = new List<PatternEvent>(0);
        PatternEvent[] emptyPatternEventList = Array.Empty<PatternEvent>();
        Lock patternEventsLock = new Lock();
        // Returns the events from PatternColumn object. Not from pattern editor machine.
        public IEnumerable<PatternEvent> GetEvents(int tbegin, int tend)
        {
            if (patternEvents.Count == 0)
                return emptyPatternEventList;

            patternEventsList.Clear();
            lock (patternEventsLock)
            {
                for (int i = 0; i < patternEvents.Count; i++)
                {
                    var pe = patternEvents[i];

                    if (pe.Time >= tbegin && pe.Time < tend)
                        patternEventsList.Add(pe);
                }
            }
            return patternEventsList.ToArray();
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
                lock (patternEventsLock)
                {
                    if (set)
                        this.patternEvents.Add(pe);
                    else
                        this.patternEvents.Remove(pe);
                }
            }
            EventsChanged?.Invoke(patternEvents, set);
        }
    }
}
