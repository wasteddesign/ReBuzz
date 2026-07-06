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

        PatternEvent[] emptyPatternEventList = Array.Empty<PatternEvent>();
        Lock patternEventsLock = new Lock();
        // Returns the events from PatternColumn object. Not from pattern editor machine.
        // Returns a fresh snapshot array (callers may enumerate it multiple times and
        // across threads - e.g. the BMX save path Count()s then foreach()es it). All
        // reads of the shared event list happen under the lock.
        public IEnumerable<PatternEvent> GetEvents(int tbegin, int tend)
        {
            lock (patternEventsLock)
            {
                if (patternEvents.Count == 0)
                    return emptyPatternEventList;

                var list = new List<PatternEvent>();
                for (int i = 0; i < patternEvents.Count; i++)
                {
                    var pe = patternEvents[i];
                    if (pe.Time >= tbegin && pe.Time < tend)
                        list.Add(pe);
                }
                return list.ToArray();
            }
        }

        // Zero-alloc variant for the audio play path: fills a caller-owned buffer
        // instead of allocating a snapshot array. `dest` MUST be owned exclusively by
        // the caller (the audio thread passes its own reusable list); the old shared
        // scratch field this replaces was itself a cross-thread race (its Clear() ran
        // outside the lock). The lock guards the shared source list only.
        public void GetEventsInto(int tbegin, int tend, List<PatternEvent> dest)
        {
            dest.Clear();
            lock (patternEventsLock)
            {
                int count = patternEvents.Count;
                for (int i = 0; i < count; i++)
                {
                    var pe = patternEvents[i];
                    if (pe.Time >= tbegin && pe.Time < tend)
                        dest.Add(pe);
                }
            }
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
