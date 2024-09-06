using BuzzGUI.Common.Actions;
using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace ReBuzz.Core.Actions.GraphActions
{
    internal class CreateMachineAction : BuzzAction
    {
        private readonly ReBuzzCore buzz;
        private readonly int id = -1;
        private readonly float x;
        private readonly float y;
        private readonly bool createSeqAndPattern;
        private readonly string machineLib;
        private readonly string instrument;
        private readonly string name;
        private readonly byte[] data;
        private readonly string patterneditor;
        private readonly byte[] patterneditordata;
        private readonly int trackcount;

        readonly Dictionary<int, IEnumerable<KeyValuePair<int, SequenceEventRef>>> sequences = new Dictionary<int, IEnumerable<KeyValuePair<int, SequenceEventRef>>>();
        private string actualName;

        public CreateMachineAction(ReBuzzCore buzz, int id, float x, float y)
        {
            this.buzz = buzz;
            this.id = id;
            this.x = x;
            this.y = y;

            createSeqAndPattern = true;
        }

        public CreateMachineAction(ReBuzzCore buzz, string machineLib, string instrument, string name, byte[] data, string patterneditor, byte[] patterneditordata, int trackcount, float x, float y)
        {
            this.buzz = buzz;
            this.machineLib = machineLib;
            this.instrument = instrument;
            this.name = name;
            this.data = data;
            this.patterneditor = patterneditor;
            this.patterneditordata = patterneditordata;
            this.trackcount = trackcount;
            this.x = x;
            this.y = y;
        }

        protected override void DoAction()
        {
            MachineCore machine;
            if (id != -1)
            {
                machine = buzz.CreateMachine(id, x, y);
            }
            else
            {
                machine = buzz.CreateMachine(machineLib, instrument, name, data, patterneditor, patterneditordata, trackcount, x, y);
            }

            this.actualName = machine.Name;

            if (machine != null)
            {
                if (sequences.Count > 0)
                {
                    foreach (var seq in sequences)
                    {
                        buzz.SongCore.AddSequence(machine, seq.Key);
                        var seqAdded = buzz.SongCore.SequencesList.ElementAt(seq.Key);
                        foreach (var eventItem in seq.Value)
                        {
                            var seqEvent = eventItem.Value;
                            machine.CreatePattern(seqEvent.PatternName, seqEvent.Span);
                            var pattern = machine.PatternsList.FirstOrDefault(p => p.Name == seqEvent.PatternName);
                            SequenceEvent sequenceEvent = new SequenceEvent(seqEvent.Type, pattern, seqEvent.Span);
                            seqAdded.SetEvent(eventItem.Key, sequenceEvent);
                        }
                    }
                }
                else
                {
                    if (createSeqAndPattern)
                    {
                        buzz.SongCore.AddSequence(machine, buzz.SongCore.Sequences.Count);
                        if (machine.Patterns.Count == 0)
                        {
                            machine.CreatePattern("00", 16);
                            var pattern = machine.Patterns.Last();
                            SequenceEvent sequenceEvent = new SequenceEvent(SequenceEventType.PlayPattern, pattern);
                            buzz.SongCore.Sequences.Last().SetEvent(0, sequenceEvent);
                            buzz.MIDIFocusMachine = machine;
                        }
                    }
                }
            }
        }

        protected override void UndoAction()
        {
            var machine = buzz.SongCore.MachinesList.FirstOrDefault(x => x.Name == actualName);
            if (machine != null)
            {
                sequences.Clear();
                int index = 0;
                // Save sequences
                foreach (var seq in buzz.SongCore.Sequences)
                {
                    if (seq.Machine == machine)
                    {
                        Dictionary<int, SequenceEventRef> targetEvents = new Dictionary<int, SequenceEventRef>();
                        var events = seq.Events;
                        foreach (var eventItem in events)
                        {
                            var v = eventItem.Value;
                            targetEvents[eventItem.Key] = new SequenceEventRef(v.Type, v.Pattern?.Name, v.Span);
                        }
                        sequences.Add(index, targetEvents);
                    }
                    index++;
                }

                buzz.RemoveMachine(machine);
            }
        }
    }
}
