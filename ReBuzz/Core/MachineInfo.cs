using BuzzGUI.Interfaces;

namespace ReBuzz.Core
{
    internal class MachineInfo : IMachineInfo
    {
        public MachineType Type { get; set; }

        public int Version { get; set; }

        public int InternalVersion { get; set; }

        public MachineInfoFlags Flags { get; set; }

        public int MinTracks { get; set; }

        public int MaxTracks { get; set; }

        public string Name { get; set; }

        public string ShortName { get; set; }

        public string Author { get; set; }
    }
}
