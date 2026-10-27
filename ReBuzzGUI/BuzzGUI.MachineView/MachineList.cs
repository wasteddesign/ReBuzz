using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
//using PropertyChanged;

namespace BuzzGUI.MachineView
{
    public class MachineList : INotifyPropertyChanged
    {
        readonly MachineView view;
        List<MachineListItemVM> items;
        private IEnumerable<string> favourties;

        public IEnumerable<MachineListItemVM> Items { get { return items; } }

        string filter = "";
        public string Filter
        {
            set
            {
                if (value == filter) return;
                filter = value;
                PropertyChanged.Raise(this, "Filter");
                Update();
            }
        }

        bool filterVst2;
        public bool FilterVst2
        {
            get => filterVst2;
            set
            {
                filterVst2 = value;
                PropertyChanged.Raise(this, "FilterVst2");
                Update();
            }
        }

        bool filterVst3;
        public bool FilterVst3
        {
            get => filterVst3;
            set
            {
                filterVst3 = value;
                PropertyChanged.Raise(this, "FilterVst3");
                Update();
            }
        }

        bool filterBM;
        public bool FilterBM
        {
            get => filterBM;
            set
            {
                filterBM = value;
                PropertyChanged.Raise(this, "FilterBM");
                Update();
            }
        }

        bool filterFavourites;
        public bool FilterFavourites
        {
            get => filterFavourites;
            set
            {
                filterFavourites = value;
                PropertyChanged.Raise(this, "FilterFavourites");
                Update();
            }
        }

        public enum MachineFilterGroups { All, Generator, Effect, Control };

        MachineFilterGroups visibleMachines = MachineFilterGroups.All;
        public MachineFilterGroups VisibleMachines
        {
            get { return visibleMachines; }
            set
            {
                if (value == visibleMachines) return;
                visibleMachines = value;
                PropertyChanged.Raise(this, "VisibleMachines");
                Update();
            }
        }

        bool Match(InstrumentType t, MachineFilterGroups g)
        {
            if (g == MachineFilterGroups.All) return true;

            switch (t)
            {
                case InstrumentType.Generator: return g == MachineFilterGroups.Generator;
                case InstrumentType.Effect: return g == MachineFilterGroups.Effect;
                case InstrumentType.Control: return g == MachineFilterGroups.Control;
                default: return false;
            }
        }

        bool ApplyFilter(IInstrument i)
        {
            bool show = !(FilterVst2 || FilterVst3 || FilterBM) ? true : false;
            bool isVst3 = i.Name.StartsWith("VST3/");

            if (FilterVst2)
            {
                if (!string.IsNullOrEmpty(i.Name))
                {
                    show = !isVst3;
                }
            }

            if (FilterVst3)
            {
                if (!string.IsNullOrEmpty(i.Name))
                {
                    show |= isVst3;
                }
            }

            if (FilterBM)
            {
                if (string.IsNullOrEmpty(i.Name))
                    show |= true;
            }

            // Only apply favourites filter if item is shown
            if (FilterFavourites && show)
                 return favourties.Contains(MachineListItemVM.GetFavouriteInstrumentName(i));

            return show;
        }

        public MachineList(MachineView view)
        {
            this.view = view;
            Update();
        }

        public void Update()
        {
            items = new List<MachineListItemVM>();
            favourties = RegistryEx.ReadNumberedList<string>("Favourite", "Favourite Machines");
            foreach (var i in view.Buzz.Instruments)
            {
                if (Match(i.Type, VisibleMachines) && ApplyFilter(i)
                    && (filter.Length == 0
                    || (i.Name.Length == 0 && i.MachineDLL.Name.IndexOf(filter, 0, StringComparison.OrdinalIgnoreCase) != -1)
                    || i.Name.IndexOf(filter, 0, StringComparison.OrdinalIgnoreCase) != -1))
                    items.Add(new MachineListItemVM(i));
            }

            try
            {
                items.Sort();
            }
            catch (Exception e)
            {
                DebugConsole.WriteLine(e.Message);
            }

            PropertyChanged.Raise(this, "Items");
        }



        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
