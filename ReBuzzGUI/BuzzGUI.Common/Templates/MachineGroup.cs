using BuzzGUI.Common.Presets;
using BuzzGUI.Interfaces;
using SevenZip.Compression.LZMA;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace BuzzGUI.Common.Templates
{
    public class MachineGroup
    {
        [XmlAttribute]
        public string Name;

        [XmlAttribute]
        public float PositionX;

        [XmlAttribute]
        public float PositionY;

        [XmlAttribute]
        public bool IsGrouped;

        [XmlAttribute]
        public string MainInputMachineName { get; set; }

        [XmlAttribute]
        public string MainOutputMachineName { get; set; }

        public List<MachineGroupMachine> Machines;

        public MachineGroup() { }
        public MachineGroup(IMachineGroup g)
        {
            Name = g.Name;
            PositionX = g.Position.Item1;
            PositionY = g.Position.Item2;
            IsGrouped = g.IsGrouped;

            MainInputMachineName = g.MainInputMachine?.Name;
            MainOutputMachineName = g.MainOutputMachine?.Name;

            List<MachineGroupMachine> machineGroupMachines = new List<MachineGroupMachine>();
            foreach (var machine in g.Machines)
            {
                machineGroupMachines.Add( new MachineGroupMachine(g, machine));
            }
            Machines = machineGroupMachines;
        }
    }
}
