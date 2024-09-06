using BuzzGUI.Common.Actions;
using BuzzGUI.Interfaces;
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

        public ReplaceMachineAction(ReBuzzCore buzz, IMachine m, int id, float x, float y)
        {
            this.buzz = buzz;

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
                    deleteMachinesAction = new DeleteMachinesAction(buzz, new List<IMachine>() { m });
                    createMachinesAction = new CreateMachineAction(buzz, MachineDll.Name, instInfo.InstrumentName, null, null, m.PatternEditorDLL.Name, null, m.TrackCount, m.Position.Item1, m.Position.Item2);
                }
            }
            // Backup connections
            var machineCore = m as MachineCore;
            machineInfo = new MachineInfoRef(machineCore);
            SaveConnections(machineCore, machineInfo);
        }

        public ReplaceMachineAction(ReBuzzCore buzz, IMachine m, string machine, string instrument, float x, float y)
        {
            var newMachine = buzz.MachineDLLs[machine];
            if (newMachine.Name == m.DLL.Name && instrument != null)
            {
                this.swapInstrument = true;
                this.newInstrument = instrument;
            }
            else
            {
                // Delete machine action saves patterns and editor data
                deleteMachinesAction = new DeleteMachinesAction(buzz, new List<IMachine>() { m });
                createMachinesAction = new CreateMachineAction(buzz, machine, instrument, null, null, m.PatternEditorDLL.Name, null, m.TrackCount, m.Position.Item1, m.Position.Item2);
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
                    buzz.MachineManager.SetInstrument(machine, newInstrument);
                    this.newMachineName = machine.Name; // Save name for undo
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
                    new ConnectMachinesAction(buzz, src, machine, 0, 0, cma.Amp, cma.Pan).Do();
                }

                // reconnect outputs
                foreach (var cma in machineInfo.connections.Where(c => c.Source == machineInfo.Name))
                {
                    var dst = buzz.Song.Machines.FirstOrDefault(m => m.Name == cma.Destination);
                    new ConnectMachinesAction(buzz, machine, dst, 0, 0, cma.Amp, cma.Pan).Do();
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
                    buzz.MachineManager.SetInstrument(machine, machineInfo.Instrument);
                    this.newMachineName = machine.Name; // Save name for redo
                    machine.Data = machineInfo.Data;
                }
            }
            else
            {
                var machine = buzz.Song.Machines.FirstOrDefault(m => m.Name == newMachineName);

                // Disconnect inputs
                foreach (var cma in machineInfo.connections.Where(c => c.Destination == machineInfo.Name))
                {
                    var src = buzz.Song.Machines.FirstOrDefault(m => m.Name == cma.Source);
                    new DisconnectMachinesAction(buzz, src, machine, cma.SourceChannel, cma.DestinationChannel, cma.Amp, cma.Pan).Do();
                }

                // Disconnect outputs
                foreach (var cma in machineInfo.connections.Where(c => c.Source == machineInfo.Name))
                {
                    var dst = buzz.Song.Machines.FirstOrDefault(m => m.Name == cma.Destination);
                    new DisconnectMachinesAction(buzz, machine, dst, cma.SourceChannel, cma.DestinationChannel, cma.Amp, cma.Pan).Do();
                }
                createMachinesAction.Undo();
                deleteMachinesAction.Undo();
            }
        }
    }
}
