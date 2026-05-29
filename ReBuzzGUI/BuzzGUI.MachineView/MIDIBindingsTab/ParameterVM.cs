using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.ComponentModel;
using System.Windows;
//using PropertyChanged;

namespace BuzzGUI.MachineView.MIDIBindingsTab
{

    //[DoNotNotify]
    public class ParameterVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        readonly MachineVM machine;
        IParameter parameter;
        readonly int track;

        public IParameter Parameter { get { return parameter; } }

        public SimpleCommand ClearBindingCommand { get; set; }

        public string Name
        {
            get
            {
                string n = parameter.GetDisplayName(track);

                if (parameter.Group.Type == ParameterGroupType.Input)
                {
                    if (parameter.IndexInGroup == 0)
                        return "a-" + n;
                    else if (parameter.IndexInGroup == 1)
                        return "p-" + n;
                    else
                        return "?-" + n;
                }
                else if (parameter.Group.Type == ParameterGroupType.Track)
                {
                    return string.Format("{0}-{1}", track, n);
                }
                else
                {
                    return n;
                }
            }
        }

        public string Description { get { return parameter.Description; } }
        public string ValueDescription { get { return parameter.DescribeValue(parameter.GetValue(track)); } }
        public string MIDIBindingDescription { get
            {
                var m = parameter.GetMIDIBinding(track);
                return "Chan " + (m.Item1 + 1) + " | Ctrl " + m.Item2;
            }
        }

        public Visibility Visibility
        {
            get
            {
                return ((machine.TabVM.ParameterFilter.Length == 0
                    || parameter.Name.IndexOf(machine.TabVM.ParameterFilter, StringComparison.OrdinalIgnoreCase) >= 0
                    || parameter.Description.IndexOf(machine.TabVM.ParameterFilter, StringComparison.OrdinalIgnoreCase) >= 0)) &&
                    parameter.GetMIDIBinding(track).Item1 != -1
                    ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public ParameterVM(MachineVM wnd, IParameter p, int t)
        {
            machine = wnd;
            parameter = p;
            track = t;

            Parameter.MIDIBindingChanged += Parameter_MIDIBindingChanged;
            ClearBindingCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = id =>
                {
                    parameter.BindToMIDIController(track, -1, -1);
                   
                }
            };
        }

        public void Release()
        {
            Parameter.MIDIBindingChanged -= Parameter_MIDIBindingChanged;
            parameter = null;
        }

        private void Parameter_MIDIBindingChanged(int track)
        {
            PropertyChanged.Raise(this, "MIDIBindingDescription");
            machine.TabVM.UpdateVisibility();
        }
    }
}
