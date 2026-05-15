using BuzzGUI.Common;
using BuzzGUI.Common.Actions;
using BuzzGUI.Common.Settings;
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
        private readonly EngineSettings engineSettings;

        public DeleteMachinesAction(
            ReBuzzCore buzz,
            IEnumerable<IMachine> m,
            IUiDispatcher dispatcher,
            EngineSettings engineSettings)
        {
            this.engineSettings = engineSettings;
            this.buzz = buzz;
            this.dispatcher = dispatcher;

            foreach (var mac in m)
            {
                var machine = mac as MachineCore;
                MachineInfoRef machineData = new MachineInfoRef(machine);
                deleteMachineDatas.Add(machineData);
                SaveConnections(machine, machineData);

                // Save sequences
                foreach (var seq in buzz.SongCore.Sequences.Where(s => s.Machine == machine).OrderBy(s => buzz.SongCore.Sequences.IndexOf(s)))
                {
                    var events = seq.Events;
                    int index = buzz.SongCore.Sequences.IndexOf(seq);
                    machineData.AddSequence(index, events);
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
                machineData.disconnectActions.Add( new DisconnectMachinesAction(buzz, connection, dispatcher, engineSettings) );
            }

            foreach (var connection in machine.AllInputs)
            {
                machineData.disconnectActions.Add(new DisconnectMachinesAction(buzz, connection, dispatcher, engineSettings));
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
                        //foreach (var c in machineData.connections)
                        //{
                        //    RemoveConnection(machine, c);
                        //}

                        foreach (var c in machineData.disconnectActions)
                        {
                            c.Do();
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

        protected override void UndoAction()
        {
            // Create machines
            foreach (var machineData in deleteMachineDatas.OrderBy(md => md.sequences.Count > 0 ? md.sequences.Keys.OrderBy(o => o).First() : 0))
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
                        int index = seq.Key;
                        buzz.SongCore.AddSequence(machine, index);
                        var seqAdded = buzz.SongCore.SequencesList.ElementAt(index);

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
                foreach (var c in machineData.disconnectActions)
                {
                    c.Undo();
                }
            }
        }
    }
}
