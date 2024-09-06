using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ReBuzz.Core.Actions
{
    public class MachineInfoRef
    {
        public IMachineDLL PatternEditorDLL { get; internal set; }
        public byte[] PatternEditorData { get; internal set; }
        public string Name { get; set; }
        public Tuple<float, float> Pos { get; set; }
        public byte[] Data { get; set; }
        public string PatternEditorMachineName { get; }
        public string PatternEditorDllName { get; internal set; }
        public string MachineLib { get; internal set; }
        public string Instrument { get; internal set; }
        public int TrackCount { get; internal set; }
        internal List<AttributeCore> Attributes { get; set; }

        public Dictionary<int, IEnumerable<KeyValuePair<int, SequenceEventRef>>> sequences = new Dictionary<int, IEnumerable<KeyValuePair<int, SequenceEventRef>>>();
        public Dictionary<string, int> patternAssociations = new Dictionary<string, int>();
        public List<ConnectionInfoRef> connections = new List<ConnectionInfoRef>();
        internal List<ParameterGroup> parameterGroups = new List<ParameterGroup>();
        internal List<PatternInfoRef> patterns = new List<PatternInfoRef>();

        public MachineInfoRef(MachineCore machine)
        {
            Name = machine.Name;
            if (machine.PatternEditorDLL != null)
            {
                PatternEditorDllName = machine.PatternEditorDLL.Name;
            }
            if (machine.EditorMachine != null)
            {
                PatternEditorMachineName = machine.EditorMachine.Name;
            }
            Pos = new Tuple<float, float>(machine.Position.Item1, machine.Position.Item2);
            MachineLib = machine.DLL.Name;
            Instrument = machine.InstrumentName;
            TrackCount = machine.TrackCount;
            PatternEditorData = machine.PatternEditorData;
            Data = machine.Data;

            // Save Attributes
            Attributes = machine.AttributesList.ToList();

            // Save parameter values
            ParameterGroup pg = machine.ParameterGroupsList[0].Clone();
            parameterGroups.Add(pg);
            pg = machine.ParameterGroupsList[1].Clone();
            parameterGroups.Add(pg);
            pg = machine.ParameterGroupsList[2].Clone();
            parameterGroups.Add(pg);

            // Save Patterns
            foreach (var pattern in machine.PatternsList)
            {
                patterns.Add(new PatternInfoRef(pattern.Name, pattern.Length));
            }
        }

        internal ParameterGroup GetGroup(MachineCore machine, int index)
        {
            var pg = parameterGroups[index];
            pg.Machine = machine;
            for (int i = 0; i < pg.ParametersList.Count; i++)
            {
                pg.ParametersList[i].Group = pg;
            }

            return pg;
        }

        internal void CopyParameters(MachineCore machine, int group, int track)
        {
            var parametersTo = machine.ParameterGroupsList[group].ParametersList;
            var parametersFrom = parameterGroups[group].ParametersList;

            for (int i = 0; i < parametersTo.Count; i++)
            {
                // Set the defaul/saved state of parameters. Skip non-state, notes and input group
                if (parametersTo[i].Flags.HasFlag(ParameterFlags.State) && parametersTo[i].Type != ParameterType.Note && group != 0)
                {
                    if (machine.DLL.IsManaged)
                    {
                        parametersTo[i].SetValue(track, parametersFrom[i].GetValue(track));
                    }
                    else
                    {
                        parametersTo[i].DirectSetValue(track, parametersFrom[i].GetValue(track));
                    }
                }
            }
        }

        internal void AddSequence(int index, ReadOnlyDictionary<int, SequenceEvent> sourceEvents)
        {
            Dictionary<int, SequenceEventRef> targetEvents = new Dictionary<int, SequenceEventRef>();
            foreach (var kvSource in sourceEvents)
            {
                var e = kvSource.Value;
                targetEvents.Add(kvSource.Key, new SequenceEventRef(e.Type, e.Pattern?.Name, e.Span));
            }
            sequences.Add(index, targetEvents);
        }
    }

    public class PatternInfoRef
    {
        public string Name { get; }
        public int Lenght { get; }
        public PatternInfoRef(string name, int lenght)
        {
            Name = name;
            Lenght = lenght;
        }
    }

    public class SequenceEventRef
    {
        public SequenceEventType Type { get; private set; }

        public string PatternName { get; private set; }

        public int Span { get; set; }

        public SequenceEventRef(SequenceEventType type, string pattern, int span)
        {
            Type = type;
            PatternName = pattern;
            Span = span;
        }
    }

    public class ConnectionInfoRef
    {

        public ConnectionInfoRef(IMachineConnection m)
        {
            Source = m.Source.Name;
            SourceChannel = m.SourceChannel;
            Destination = m.Destination.Name;
            DestinationChannel = m.DestinationChannel;
            Amp = m.Amp;
            Pan = m.Pan;
        }

        public string Source { get; internal set; }
        public int SourceChannel { get; internal set; }
        public string Destination { get; internal set; }
        public int Amp { get; internal set; }
        public int Pan { get; internal set; }
        public int DestinationChannel { get; internal set; }
    }

}
