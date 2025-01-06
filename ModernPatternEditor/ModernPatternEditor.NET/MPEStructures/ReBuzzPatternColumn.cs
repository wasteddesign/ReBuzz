using BuzzGUI.Common.Actions;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WDE.ModernPatternEditor.MPEStructures
{
    internal class ReBuzzPatternColumn : IPatternColumn
    {
        internal MPEPattern MPEPattern { get; }

        public PatternColumnType Type { get; }

        public IPattern Pattern { get; }

        public IMachine Machine
        {
            get;
            set;
        }

        public IParameter Parameter { get; }

        public int Track { get; set; }

        List<PatternEvent> patternEvents = new List<PatternEvent>();

        Dictionary<int, int> beatSubdivision;
        public ReadOnlyDictionary<int, int> BeatSubdivision { get => BuzzGUI.Interfaces.IDictionaryExtensions.AsReadOnly(beatSubdivision); }

        Dictionary<string, string> metadata = new Dictionary<string, string>();
        public IDictionary<string, string> Metadata { get => metadata; }

        public event Action<IEnumerable<PatternEvent>, bool> EventsChanged;
        public event Action<int> BeatSubdivisionChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public ReBuzzPatternColumn(MPEPattern p, PatternColumnType type, IPattern pattern, IMachine machine, IParameter parameter, int track, Dictionary<int, int> beatSubdivisions, Dictionary<string, string> metadatas)
        {
            MPEPattern = p;
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

    public struct MPEPatternColumnBeatRef
    {
        private readonly MPEPatternColumnRef column;

        private readonly int beat;

        public int Beat => beat;

        public IPatternColumn GetColumn(MPEPattern p)
        {
            return column.GetColumn(p);
        }

        public MPEPatternColumnBeatRef(MPEPatternColumnRef pcr, int beat)
        {
            column = pcr;
            this.beat = beat;
        }
    }

    public struct MPEPatternColumnRef
    {
        private readonly int index;

        public IPatternColumn GetColumn(MPEPattern p)
        {
            return p.Columns[index];
        }

        public MPEPatternColumnRef(IPatternColumn pc)
        {
            var mpepc = pc as ReBuzzPatternColumn;
            index = mpepc.MPEPattern.Columns.IndexOf(mpepc);
        }
    }
}
