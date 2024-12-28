using BuzzGUI.Common;
using System.Collections.Generic;

namespace BuzzGUI.MachineView.MDBTab.MDB
{
    public class MachineParameter
    {
        public int Type { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int MinValue { get; set; }
        public int MaxValue { get; set; }
        public int NoValue { get; set; }
        public int Flags { get; set; }
        public int DefValue { get; set; }

    }

    public class MachineAttribute
    {
        public string Name { get; set; }
        public int MinValue { get; set; }
        public int MaxValue { get; set; }
        public int DefValue { get; set; }

    }

    public class MachineInfo
    {
        public int Type { get; set; }
        public int Version { get; set; }
        public int Flags { get; set; }
        public int minTracks { get; set; }
        public int maxTracks { get; set; }
        public int numGlobalParameters { get; set; }
        public int numTrackParameters { get; set; }
        public int numAttributes { get; set; }
        public string Name { get; set; }
        public string ShortName { get; set; }
        public string Author { get; set; }
        public string Commands { get; set; }
        public bool HaspLI { get; set; }

        public MachineParameter[] Parameters { get; set; }
        public MachineAttribute[] Attributes { get; set; }

    }

    public class MachineDLL
    {
        public string Filename { get; set; }
        public string GearDirectory { get; set; }
        public string DateTime { get; set; }
        public string SHA1 { get; set; }
        public bool IsDebugBuild { get; set; }
        public MachineInfo MachineInfo { get; set; }

    }

    public class Database
    {
        public List<MachineDLL> MachineDLLs { get; set; }


        public static Database Load(string path)
        {
            var str = ZipFileEx.UnzipString(path, "mdb.json");
            return Newtonsoft.Json.JsonConvert.DeserializeObject<Database>(str);
        }

    }
}
