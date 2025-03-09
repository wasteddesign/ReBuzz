using BuzzGUI.Common.Actions;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ReBuzz.Core.Actions.GraphActions
{
    internal class ReplaceMachineAction : BuzzAction
    {
        private readonly ReBuzzCore buzz;
        readonly DeleteMachinesAction deleteMachinesAction;
        readonly CreateMachineAction createMachinesAction;

        readonly MachineInfoRef machineInfo;
        private string newMachineName;
        private readonly bool swapInstrument;
        private readonly string newInstrument;
        private readonly IUiDispatcher dispatcher;

        public ReplaceMachineAction(ReBuzzCore buzz, IMachine m, int id, float x, float y, IUiDispatcher dispatcher)
        {
            this.buzz = buzz;
            this.dispatcher = dispatcher;

            var MachineDB = buzz.MachineDB;
            var MachineDLLs = buzz.MachineDLLs;
            if (MachineDB.DictLibRef.ContainsKey(id))
            {
                var instInfo = MachineDB.DictLibRef[id];
                var MachineDll = MachineDLLs[instInfo.libName];

                if (MachineDll.Name == m.DLL.Name && instInfo.InstrumentName != null)
                {
                    this.swapInstrument = true;
                    this.newInstrument = instInfo.InstrumentName;
                }
                else
                {
                    string peName = m.PatternEditorDLL != null ? m.PatternEditorDLL.Name : null;
                    deleteMachinesAction = new DeleteMachinesAction(buzz, new List<IMachine>() { m }, this.dispatcher);
                    createMachinesAction = new CreateMachineAction(buzz, MachineDll.Name, instInfo.InstrumentName, null, null, peName, null, m.TrackCount, m.Position.Item1, m.Position.Item2);
                }
            }
            // Backup connections
            var machineCore = m as MachineCore;
            machineInfo = new MachineInfoRef(machineCore);
            SaveConnections(machineCore, machineInfo);
        }

        public ReplaceMachineAction(ReBuzzCore buzz, IMachine m, string machine, string instrument, float x, float y, IUiDispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
            var newMachine = buzz.MachineDLLs[machine];
            if (newMachine.Name == m.DLL.Name && instrument != null)
            {
                this.swapInstrument = true;
                this.newInstrument = instrument;
            }
            else
            {
                string peName = m.PatternEditorDLL != null ? m.PatternEditorDLL.Name : null;
                // Delete machine action saves patterns and editor data
                deleteMachinesAction = new DeleteMachinesAction(buzz, new List<IMachine>() { m }, this.dispatcher);
                createMachinesAction = new CreateMachineAction(buzz, machine, instrument, null, null, peName, null, m.TrackCount, m.Position.Item1, m.Position.Item2);
            }
            // Backup connections
            var machineCore = m as MachineCore;
            machineInfo = new MachineInfoRef(machineCore);
            SaveConnections(machineCore, machineInfo);
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
            if (swapInstrument)
            {
                var machine = buzz.Song.Machines.FirstOrDefault(m => m.Name == machineInfo.Name) as MachineCore;
                if (machine != null)
                {
                    if (machine.DLL.IsCrashed)
                    {
                        // Adapter machine was crashed, so we need to recreate adapter and save & restore all relevant data
                        // Save editor data
                        byte[] editorData = machine.PatternEditorData;
                        string editorDLLName = machine.PatternEditorDLL != null ? machine.PatternEditorDLL.Name : null;
                        string dllName = machine.DLL.Name;
                        string oldMachineName = machine.Name;
                        int tracks = machine.TrackCount;
                        float x = machine.Position.Item1;
                        float y = machine.Position.Item2;

                        // Save pattern information
                        var patterns = machine.Patterns.ToArray();

                        // Save sequences and events
                        List<Tuple<int, List<Tuple<int, SequenceEvent>>>> oldSeqs = new List<Tuple<int, List<Tuple<int, SequenceEvent>>>>();
                        foreach(var seq in buzz.Song.Sequences)
                        {
                            if (seq.Machine == machine)
                            {
                                int index = buzz.Song.Sequences.IndexOf(seq);
                                List<Tuple<int, SequenceEvent>> oldEvents = new List<Tuple<int, SequenceEvent>>();
                                foreach (var e in seq.Events)
                                {
                                    oldEvents.Add(new(e.Key, e.Value));
                                }

                                oldSeqs.Add(new (index, oldEvents));
                            }
                        }

                        lock (ReBuzzCore.AudioLock)
                        {
                            new DeleteMachinesAction(buzz, new List<IMachine>() { machine }, this.dispatcher).Do();
                            new CreateMachineAction(buzz, dllName, newInstrument, oldMachineName, null, editorDLLName, editorData, tracks, x, y).Do();
                        }

                        var newMachine = buzz.Song.Machines.Last();

                        var nameAfterCreation = newMachine.Name; // Save name for undo

                        // Pattern editor expects to have data linked to the old machine, so do some renaming before creating patterns
                        buzz.RenameMachine(newMachine as MachineCore, oldMachineName);

                        // Create patterns
                        foreach (var p in patterns)
                        {
                            newMachine.CreatePattern(p.Name, p.Length);
                        }

                        foreach (var seq in oldSeqs)
                        {
                            int index = seq.Item1;
                            buzz.Song.AddSequence(newMachine, index);
                            var newSeq = buzz.Song.Sequences[index];

                            foreach (var e in seq.Item2)
                            {
                                var seOld = e.Item2;
                                var time = e.Item1;
                                var pattern = newMachine.Patterns.FirstOrDefault(p => p.Name == e.Item2.Pattern.Name);
                                SequenceEvent seNew = new SequenceEvent(seOld.Type, pattern, seOld.Span);
                                newSeq.SetEvent(time, seNew);
                            }
                        }
                        
                        // reconnect inputs
                        foreach (var cma in machineInfo.connections.Where(c => c.Destination == machineInfo.Name))
                        {
                            var src = buzz.Song.Machines.FirstOrDefault(m => m.Name == cma.Source);
                            new ConnectMachinesAction(buzz, src, machine, 0, 0, cma.Amp, cma.Pan, dispatcher).Do();
                        }

                        // reconnect outputs
                        foreach (var cma in machineInfo.connections.Where(c => c.Source == machineInfo.Name))
                        {
                            var dst = buzz.Song.Machines.FirstOrDefault(m => m.Name == cma.Destination);
                            new ConnectMachinesAction(buzz, machine, dst, 0, 0, cma.Amp, cma.Pan, dispatcher).Do();
                        }

                        buzz.RenameMachine(newMachine as MachineCore, nameAfterCreation);
                        this.newMachineName = nameAfterCreation; // Save name for undo
                    }
                    else
                    {
                        buzz.MachineManager.SetInstrument(machine, newInstrument);
                        this.newMachineName = machine.Name; // Save name for undo
                    }
                }
            }
            else
            {
                deleteMachinesAction.Do();
                createMachinesAction.Do();

                var machine = buzz.Song.Machines.Last();
                newMachineName = machine.Name; // Save name for undo

                if (machine.Patterns.Count == 0)
                {
                    machine.CreatePattern("00", 16);
                }

                if (buzz.Song.Sequences.Count == 0)
                {
                    buzz.Song.AddSequence(machine, buzz.Song.Sequences.Count);
                    buzz.Song.Sequences.Last().SetEvent(0, new SequenceEvent(SequenceEventType.PlayPattern, machine.Patterns.First()));
                }

                // reconnect inputs
                foreach (var cma in machineInfo.connections.Where(c => c.Destination == machineInfo.Name))
                {
                    var src = buzz.Song.Machines.FirstOrDefault(m => m.Name == cma.Source);
                    new ConnectMachinesAction(buzz, src, machine, 0, 0, cma.Amp, cma.Pan, dispatcher).Do();
                }

                // reconnect outputs
                foreach (var cma in machineInfo.connections.Where(c => c.Source == machineInfo.Name))
                {
                    var dst = buzz.Song.Machines.FirstOrDefault(m => m.Name == cma.Destination);
                    new ConnectMachinesAction(buzz, machine, dst, 0, 0, cma.Amp, cma.Pan, dispatcher).Do();
                }
            }
        }

        protected override void UndoAction()
        {
            if (swapInstrument)
            {
                var machine = buzz.Song.Machines.FirstOrDefault(m => m.Name == newMachineName) as MachineCore;
                if (machine != null)
                {
                    if (machineInfo.Instrument != null)
                    {
                        buzz.MachineManager.SetInstrument(machine, machineInfo.Instrument);
                        this.newMachineName = machine.Name; // Save name for redo
                        machine.Data = machineInfo.Data;
                    }
                }
            }
            else
            {
                var machine = buzz.Song.Machines.FirstOrDefault(m => m.Name == newMachineName);

                // Disconnect inputs
                foreach (var cma in machineInfo.connections.Where(c => c.Destination == machineInfo.Name))
                {
                    var src = buzz.Song.Machines.FirstOrDefault(m => m.Name == cma.Source);
                    new DisconnectMachinesAction(buzz, src, machine, cma.SourceChannel, cma.DestinationChannel, cma.Amp, cma.Pan, dispatcher).Do();
                }

                // Disconnect outputs
                foreach (var cma in machineInfo.connections.Where(c => c.Source == machineInfo.Name))
                {
                    var dst = buzz.Song.Machines.FirstOrDefault(m => m.Name == cma.Destination);
                    new DisconnectMachinesAction(buzz, machine, dst, cma.SourceChannel, cma.DestinationChannel, cma.Amp, cma.Pan, dispatcher).Do();
                }
                createMachinesAction.Undo();
                deleteMachinesAction.Undo();
            }
        }
    }
}
