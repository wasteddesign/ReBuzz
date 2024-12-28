using BuzzGUI.Interfaces;
using System;
using System.Windows.Media;

namespace BuzzGUI.MachineView
{
    public class MachineListItemVM : IComparable<MachineListItemVM>
    {
        readonly IInstrument instrument;

        public MachineListItemVM(IInstrument instrument)
        {
            this.instrument = instrument;
        }

        public IInstrument Instrument { get { return instrument; } }
        public bool IsInstrument { get { return instrument.Name.Length > 0; } }

        public string DisplayName
        {
            get
            {
                if (IsInstrument)
                {
                    //return System.IO.Path.GetFileName(instrument.Name) + " (" + instrument.MachineDLL.Name + ")";
                    string name = System.IO.Path.GetFileName(instrument.Name);
                    if (instrument.Name.StartsWith("VST3/"))
                        name += "(vst3)";

                    return name;
                }
                else
                {
                    var name = instrument.MachineDLL.Name;
                    if (instrument.MachineDLL.IsOutOfProcess) name += " (32-bit)";
                    return name;
                }

            }
        }
        public Brush DisplayNameBrush
        {
            get
            {
                if (IsInstrument)
                {
                    switch (instrument.Type)
                    {
                        case InstrumentType.Generator: return Brushes.DarkCyan;
                        case InstrumentType.Effect: return Brushes.DarkMagenta;
                        default: return Brushes.Black;
                    }
                }
                else
                {
                    switch (instrument.Type)
                    {
                        case InstrumentType.Generator: return Brushes.DarkBlue;
                        case InstrumentType.Effect: return Brushes.DarkRed;
                        default: return Brushes.Black;
                    }
                }
            }
        }

        public string MachineName { get { return Instrument.MachineDLL.Name; } }
        public string MachinePath { get { return Instrument.MachineDLL.Path; } }
        public IMachineDLL MachineDLL { get { return Instrument.MachineDLL; } }
        public string InstrumentName { get { return Instrument.Name; } }
        public string InstrumentPath { get { return Instrument.Path; } }

        public int CompareTo(MachineListItemVM other)
        {
            return StringComparer.OrdinalIgnoreCase.Compare(DisplayName, other.DisplayName);
        }

    }

}
