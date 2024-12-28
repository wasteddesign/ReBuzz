using BuzzGUI.Common;
using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
//using PropertyChanged;

namespace BuzzGUI.MachineView.ParametersTab
{

    //[DoNotNotify]
    public class MachineVM : INotifyPropertyChanged
    {
        public IMachine Machine { get; private set; }
        public ParametersTabVM TabVM { get; private set; }

        public string Name { get { return Machine.Name; } }

        public Color TitleBackgroundColor { get { return Machine.GetThemeColor(); } }
        public Color TitleForegroundColor { get { return Machine.Graph.Buzz.ThemeColors["MV Machine Text"].EnsureContrast(TitleBackgroundColor); } }

        public ReadOnlyCollection<ParameterVM> Parameters { get; private set; }

        public Visibility Visibility
        {
            get
            {
                return (TabVM.MachineFilter.Length == 0 || Machine.Name.IndexOf(TabVM.MachineFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                    && Parameters.Any(p => p.Visibility == Visibility.Visible)
                    ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public MachineVM(ParametersTabVM ptvm, IMachine machine)
        {
            TabVM = ptvm;
            Machine = machine;
            machine.PropertyChanged += machine_PropertyChanged;
            CreateParameters();
        }

        void machine_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "TrackCount":
                    CreateParameters();
                    break;

                case "Name":
                    PropertyChanged.Raise(this, "Name");
                    break;

            }
        }

        public void Release()
        {
            Machine.PropertyChanged -= machine_PropertyChanged;

            foreach (var p in Parameters)
                p.Release();
        }

        void CreateParameters()
        {
            if (Parameters != null)
            {
                foreach (var p in Parameters)
                    p.Release();
            }

            Parameters = Machine.AllParametersAndTracks()
                .Where(pt => pt.Item1.Flags.HasFlag(ParameterFlags.State))
                .Select(pt => new ParameterVM(this, pt.Item1, pt.Item2)).ToReadOnlyCollection();

            PropertyChanged.Raise(this, "Parameters");
        }


        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
