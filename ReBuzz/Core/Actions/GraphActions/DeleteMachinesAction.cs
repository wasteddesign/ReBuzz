using BuzzGUI.Common.Actions;
using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.Linq;


namespace ReBuzz.Core.Actions.GraphActions
{
    internal class DeleteMachinesAction : BuzzAction
    {
        readonly List<MachineInfoRef> deleteMachineDatas = new List<MachineInfoRef>();

        private readonly ReBuzzCore buzz;
        private readonly IUiDispatcher dispatcher;

        public DeleteMachinesAction(ReBuzzCore buzz, IEnumerable<IMachine> m, IUiDispatcher dispatcher)
        {
            this.buzz = buzz;
            this.dispatcher = dispatcher;

            foreach (var mac in m)
            {
                var machine = mac as MachineCore;
                MachineInfoRef machineData = new MachineInfoRef(machine);
                deleteMachineDatas.Add(machineData);
                SaveConnections(machine, machineData);

                // Save sequences
                int index = 0;
                foreach (var seq in buzz.SongCore.Sequences.Where(s => s.Machine == machine))
                {
                    var events = seq.Events;
                    index = buzz.SongCore.Sequences.IndexOf(seq);
                    machineData.AddSequence(index, events);
                    index++;
                }

                // Save pattern colors
                foreach (var pattern in BuzzGUI.SequenceEditor.SequenceEditor.ViewSettings.PatternAssociations.Keys.Where(pa => pa.Machine == machine))
                {
                    if (BuzzGUI.SequenceEditor.SequenceEditor.ViewSettings.PatternAssociations.TryGetValue(pattern, out var pex))
                    {
                        // Save pattern color
                        machineData.patternAssociations.Add(pattern.Name, pex.ColorIndex);
                    }
                }
            }

        }

        private void SaveConnections(MachineCore machine, MachineInfoRef machineData)
        {
            foreach (var connection in machine.AllOutputs)
            {
                ConnectionInfoRef c = new ConnectionInfoRef(connection);
                machineData.connections.Add(c);
            }

            foreach (var connection in machine.AllInputs)
            {
                ConnectionInfoRef c = new ConnectionInfoRef(connection);
                machineData.connections.Add(c);
            }
        }

        protected override void DoAction()
        {
            lock (ReBuzzCore.AudioLock)
            {
                // Remove Connections
                foreach (var machineData in deleteMachineDatas)
                {
                    var machine = buzz.SongCore.MachinesList.FirstOrDefault(x => x.Name == machineData.Name);
                    if (machine != null)
                    {
                        foreach (var c in machineData.connections)
                        {
                            RemoveConnection(machine, c);
                        }
                    }
                }

                // Remove machines
                foreach (var machineData in deleteMachineDatas)
                {
                    var machine = buzz.SongCore.MachinesList.FirstOrDefault(x => x.Name == machineData.Name);
                    if (machine != null)
                    {

                        // Remove pattern colors
                        foreach (var pattern in BuzzGUI.SequenceEditor.SequenceEditor.ViewSettings.PatternAssociations.Keys.Where(pa => pa.Machine == machine))
                        {
                            if (BuzzGUI.SequenceEditor.SequenceEditor.ViewSettings.PatternAssociations.TryGetValue(pattern, out var pex))
                            {
                                // remove from dictionary
                                BuzzGUI.SequenceEditor.SequenceEditor.ViewSettings.PatternAssociations.Remove(pattern);
                            }
                        }

                        // Delete all pattern Columns that are linked to deleted machine
                        foreach (var m in buzz.SongCore.MachinesList)
                        {
                            foreach (var p in m.Patterns)
                            {
                                for (int i = 0; i < p.Columns.Count; i++)
                                {
                                    if (p.Columns[i].Machine == m)
                                    {
                                        p.DeleteColumn(i);
                                        i--;
                                    }
                                }
                            }
                        }

                        // Remove machine
                        buzz.RemoveMachine(machine);
                    }
                }
            }
        }

        private void RemoveConnection(MachineCore machine, ConnectionInfoRef c)
        {
            if (machine == null)
                return;

            var mc = machine.AllOutputs.FirstOrDefault(output => output.Source.Name == c.Source &&
                output.Destination.Name == c.Destination);
            if (mc != null)
            {
                // Call action without adding it to action stack
                new DisconnectMachinesAction(buzz, mc, dispatcher).Do();
            }

            mc = machine.AllInputs.FirstOrDefault(input => input.Source.Name == c.Source &&
                input.Destination.Name == c.Destination);
            if (mc != null)
            {
                // Call action without adding it to action stack
                new DisconnectMachinesAction(buzz, mc, dispatcher).Do();
            }
        }

        protected override void UndoAction()
        {
            // Create machines
            foreach (var machineData in deleteMachineDatas)
            {
                var machine = buzz.CreateMachine(
                    machineData.MachineLib, machineData.Instrument, machineData.Name, machineData.Data,
                    machineData.PatternEditorDllName, machineData.PatternEditorData, machineData.TrackCount,
                    machineData.Pos.Item1, machineData.Pos.Item2, machineData.PatternEditorMachineName);

                // Ensure name is correct
                buzz.RenameMachine(machine, machineData.Name);

                machine.AttributesList = machineData.Attributes;
                if (machine.DLL.IsMissing)
                {
                    // machine.ParameterGroupsList.Add(machineData.GetGroup(machine, 0));
                    machine.ParameterGroupsList.Add(machineData.GetGroup(machine, 1));
                    machine.ParameterGroupsList.Add(machineData.GetGroup(machine, 2));
                }
                else
                {
                    // Restore parameter values
                    machineData.CopyParameters(machine, 0, 0);
                    machineData.CopyParameters(machine, 1, 0);
                    for (int i = 0; i < machine.TrackCount; i++)
                    {
                        machineData.CopyParameters(machine, 2, i);
                    }
                }

                // Create Patterns
                foreach (var pattern in machineData.patterns)
                {
                    machine.CreatePattern(pattern.Name, pattern.Lenght);
                }

                // add patterncolors
                foreach (var pa in machineData.patternAssociations)
                {
                    var pattern = machine.PatternsList.FirstOrDefault(p => p.Name == pa.Key);
                    if (pattern != null)
                    {
                        BuzzGUI.SequenceEditor.SequenceEditor.ViewSettings.PatternAssociations.Add(pattern, new BuzzGUI.SequenceEditor.PatternEx() { ColorIndex = pa.Value });
                    }
                }

                var sequences = machineData.sequences;

                if (machine != null)
                {
                    foreach (var seq in sequences)
                    {
                        buzz.SongCore.AddSequence(machine, seq.Key);
                        var seqAdded = buzz.SongCore.SequencesList.ElementAt(seq.Key);
                        foreach (var eventItem in seq.Value)
                        {
                            var seqEvent = eventItem.Value;
                            // Get pattern
                            var pattern = machine.PatternsList.FirstOrDefault(p => p.Name == seqEvent.PatternName);
                            // Create new seqence event
                            SequenceEvent sequenceEvent = new SequenceEvent(seqEvent.Type, pattern, seqEvent.Span);
                            seqAdded.SetEvent(eventItem.Key, sequenceEvent);
                        }
                    }
                }
            }

            // Create connections
            foreach (var machineData in deleteMachineDatas)
            {
                foreach (var c in machineData.connections)
                {
                    var mc = new MachineConnectionCore(dispatcher);
                    mc.Source = buzz.SongCore.MachinesList.FirstOrDefault(m => m.Name == c.Source);
                    mc.Destination = buzz.SongCore.MachinesList.FirstOrDefault(m => m.Name == c.Destination);
                    mc.SourceChannel = c.SourceChannel;
                    mc.DestinationChannel = c.DestinationChannel;
                    mc.Amp = c.Amp;
                    mc.Pan = c.Pan;

                    // Hidden machines are automatically connected to Master so skip them here
                    var sourceMachine = mc.Source as MachineCore;
                    if (!sourceMachine.Hidden)
                    {
                        new ConnectMachinesAction(buzz, mc, dispatcher).Do();
                    }
                }
            }
        }
    }
}
