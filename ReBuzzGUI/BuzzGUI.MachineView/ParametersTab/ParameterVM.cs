using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.ComponentModel;
using System.Windows;
//using PropertyChanged;

namespace BuzzGUI.MachineView.ParametersTab
{

    //[DoNotNotify]
    public class ParameterVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        readonly MachineVM machine;
        IParameter parameter;
        readonly int track;

        public IParameter Parameter { get { return parameter; } }

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
        public int MinValue { get { return parameter.MinValue; } }
        public int MaxValue { get { return parameter.MaxValue; } }
        public int Value
        {
            get { return parameter.GetValue(track); }
            set
            {
                parameter.SetValue(track, value);

                if (parameter.Group.Machine.DLL.Info.Version >= 42)
                    parameter.Group.Machine.SendControlChanges();

            }
        }

        public Visibility Visibility
        {
            get
            {
                return (machine.TabVM.ParameterFilter.Length == 0
                    || parameter.Name.IndexOf(machine.TabVM.ParameterFilter, StringComparison.OrdinalIgnoreCase) >= 0
                    || parameter.Description.IndexOf(machine.TabVM.ParameterFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                    ? Visibility.Visible : Visibility.Collapsed;
            }
        }


        public ParameterVM(MachineVM wnd, IParameter p, int t)
        {
            machine = wnd;
            parameter = p;
            track = t;
            parameter.SubscribeEvents(track, ParameterValueChanged, ParameterValueDescriptionChanged);

        }

        public void Release()
        {
            parameter.UnsubscribeEvents(track, ParameterValueChanged, ParameterValueDescriptionChanged);
            parameter = null;
        }

        int lastValue = -1;
        void ParameterValueChanged(IParameter sender, int t)
        {
            // avoid calling DescribeValue when value doesn't change
            int newvalue = Value;
            if (newvalue != lastValue)
            {
                lastValue = newvalue;
                PropertyChanged.Raise(this, "Value");
                PropertyChanged.Raise(this, "ValueDescription");
            }
        }

        void ParameterValueDescriptionChanged(IParameter sender, int t)
        {
            PropertyChanged.Raise(this, "ValueDescription");
        }


    }
}
