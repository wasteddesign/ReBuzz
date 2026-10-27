using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using Microsoft.Win32;
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
        public bool IsFavourite
        {
            get
            {
                if (favourites == null)
                    ReadFauvouritesList();

                if (favourites?.IndexOf(GetFavouriteInstrumentName(instrument)) != -1)
                    return true;

                return false;
            }
            set
            {
                if (favourites == null)
                    ReadFauvouritesList();

                var name = GetFavouriteInstrumentName(instrument);

                if (value == false)
                    favourites?.Remove(name);
                else
                    favourites?.Add(name);

                UpdateFauvouritesList();
            }
        }

        internal static string GetFavouriteInstrumentName(IInstrument i)
        {
            return i.MachineDLL.Name + i.Name;
        }

        static List<string>? favourites;
        void ReadFauvouritesList()
        {
            favourites = RegistryEx.ReadNumberedList<string>("Favourite", "Favourite Machines").ToList();
        }

        public void UpdateFauvouritesList()
        {
            favourites?.RemoveAll(f => string.IsNullOrEmpty(f));
            RegistryEx.WriteNumberedList<string>(favourites, "Favourite", "Favourite Machines");
        }

        public int CompareTo(MachineListItemVM other)
        {
            return StringComparer.OrdinalIgnoreCase.Compare(DisplayName, other.DisplayName);
        }
    }
}
