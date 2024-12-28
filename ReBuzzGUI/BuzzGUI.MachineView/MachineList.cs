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


        public MachineList(MachineView view)
        {
            this.view = view;
            Update();
        }


        public void Update()
        {
            items = new List<MachineListItemVM>();
            foreach (var i in view.Buzz.Instruments)
            {
                if (Match(i.Type, VisibleMachines)
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
