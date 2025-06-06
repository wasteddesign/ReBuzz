using BuzzGUI.Interfaces;
using System.ComponentModel.DataAnnotations;

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

        internal MachineInfo Clone()
        {
            var f = this;
            var m = new MachineInfo()
            {
                Type = f.Type,
                Version = f.Version,
                InternalVersion = f.InternalVersion,
                Flags = f.Flags,
                MinTracks = f.MinTracks,
                MaxTracks = f.MaxTracks,
                Name = f.Name,
                ShortName = f.ShortName,
                Author = f.Author
            };
            return m;
        }
    }
}
