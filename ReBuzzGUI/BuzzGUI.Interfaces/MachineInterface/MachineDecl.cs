using System;

namespace Buzz.MachineInterface
{
    [Flags]
    public enum WorkModes { WM_NOIO = 0, WM_READ = 1, WM_WRITE = 2, WM_READWRITE = 3 };

    [AttributeUsage(AttributeTargets.Class)]
    public class MachineDecl : Attribute
    {
        public string Name { get; set; }
        public string ShortName { get; set; }
        public string Author { get; set; }
        public int InputCount { get; set; }
        public int OutputCount { get; set; }
        public int MaxTracks { get; set; }
    }
}
