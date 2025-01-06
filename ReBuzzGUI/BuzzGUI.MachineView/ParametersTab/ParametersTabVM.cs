using BuzzGUI.Common;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
//using PropertyChanged;

namespace BuzzGUI.MachineView.ParametersTab
{

    //[DoNotNotify]
    public class ParametersTabVM : INotifyPropertyChanged
    {
        readonly MachineView view;

        public enum MachineFilterGroups { All, Selected };
        MachineFilterGroups visibleMachines = MachineFilterGroups.All;

        public MachineFilterGroups VisibleMachines
        {
            get { return visibleMachines; }
            set
            {
                visibleMachines = value;
                PropertyChanged.Raise(this, "VisibleMachines");
                UpdateMachines();
            }
        }

        List<MachineVM> machines;
        public IEnumerable<MachineVM> Machines
        {
            get
            {
                if (machines == null)
                {
                    machines = (VisibleMachines == MachineFilterGroups.All ? view.Machines : view.SelectedMachines)
                       .Select(mc => new MachineVM(this, mc.Machine)).OrderBy(m => m.Name).ToList();
                }

                return machines;
            }
        }

        string machineFilter = "";
        public string MachineFilter
        {
            get { return machineFilter; }
            set
            {
                machineFilter = value;
                if (machines != null)
                {
                    foreach (var m in machines)
                        PropertyChanged.Raise(m, "Visibility");
                }
            }
        }

        string parameterFilter = "";
        public string ParameterFilter
        {
            get { return parameterFilter; }
            set
            {
                parameterFilter = value;
                if (machines != null)
                {
                    foreach (var m in machines)
                    {
                        PropertyChanged.Raise(m, "Visibility");

                        foreach (var p in m.Parameters)
                            PropertyChanged.Raise(p, "Visibility");
                    }
                }
            }
        }

        public ParametersTabVM(MachineView view)
        {
            this.view = view;
            view.PropertyChanged += view_PropertyChanged;
        }

        public void Release()
        {
            view.PropertyChanged -= view_PropertyChanged;
        }

        void view_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "Machines":
                    if (VisibleMachines == MachineFilterGroups.All)
                        UpdateMachines();
                    break;

                case "SelectedMachines":
                    if (VisibleMachines == MachineFilterGroups.Selected)
                        UpdateMachines();
                    break;
            }

        }


        void UpdateMachines()
        {
            if (machines != null)
            {
                if ((VisibleMachines == MachineFilterGroups.All ? view.Machines : view.SelectedMachines)
                    .Select(mc => mc.Machine).OrderBy(m => m.Name).SequenceEqual(machines.Select(vm => vm.Machine)))
                    return;

                foreach (var m in machines) m.Release();
                machines = null;
            }

            PropertyChanged.Raise(this, "Machines");
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
