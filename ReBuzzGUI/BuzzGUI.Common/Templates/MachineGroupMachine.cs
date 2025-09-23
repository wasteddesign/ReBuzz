using BuzzGUI.Common.Presets;
using BuzzGUI.Interfaces;
using SevenZip.Compression.LZMA;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace BuzzGUI.Common.Templates
{
    public class MachineGroupMachine
    {
        [XmlAttribute]
        public string Name;

        [XmlAttribute]
        public float PositionX;

        [XmlAttribute]
        public float PositionY;


        public MachineGroupMachine() { }
        public MachineGroupMachine(IMachineGroup g, IMachine m)
        {
            Name = m.Name;

            var pos = g.GetMachinePosition(m);
            PositionX = pos.Item1;
            PositionY = pos.Item2;
        }
    }
}
