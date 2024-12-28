using BuzzGUI.Interfaces;
using System;
using System.Windows.Media;

namespace BuzzGUI.MachineView.MDBTab
{
    public class MachineListItemVM : IComparable<MachineListItemVM>
    {
        readonly MDB.MachineDLL dll;

        // fake instrument for theme compatibility
        public class FakeInstrument : IInstrument
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public IMachineDLL MachineDLL { get; set; }
            public InstrumentType Type { get; set; }
        }

        public MachineListItemVM(IMachineDLL mdll, MDB.MachineDLL dbdll, string gearPath)
        {
            this.dll = dbdll;
            Instrument = new FakeInstrument() { Name = "", Path = "", MachineDLL = mdll };

            var mt = (MachineType)dbdll.MachineInfo.Type;
            var flags = (MachineInfoFlags)dbdll.MachineInfo.Flags;

            if (mt == MachineType.Generator)
            {
                if (flags.HasFlag(MachineInfoFlags.CONTROL_MACHINE))
                    Instrument.Type = InstrumentType.Control;
                else
                    Instrument.Type = InstrumentType.Generator;
            }
            else
                Instrument.Type = InstrumentType.Effect;

            MachinePath = System.IO.Path.Combine(gearPath, System.IO.Path.Combine(dbdll.GearDirectory, dbdll.Filename + ".dll"));
        }



        public FakeInstrument Instrument { get; set; }
        public bool IsInstrument { get { return false; } }

        public string DisplayName
        {
            get
            {
                return dll.Filename;
            }
        }

        public Brush DisplayNameBrush
        {
            get
            {
                switch (Instrument.Type)
                {
                    case InstrumentType.Generator: return Brushes.DarkBlue;
                    case InstrumentType.Effect: return Brushes.DarkRed;
                    default: return Brushes.Black;
                }
            }
        }

        public string MachineName { get { return System.IO.Path.GetFileName(dll.Filename); ; } }
        public string MachinePath { get; set; }

        public IMachineDLL MachineDLL { get { return Instrument.MachineDLL; } }
        public string InstrumentName { get { return Instrument.Name; } }
        public string InstrumentPath { get { return Instrument.Path; } }

        public int CompareTo(MachineListItemVM other)
        {
            return StringComparer.OrdinalIgnoreCase.Compare(DisplayName, other.DisplayName);
        }

    }

}
