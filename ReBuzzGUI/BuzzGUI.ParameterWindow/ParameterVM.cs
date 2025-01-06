using BuzzGUI.Common;
using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace BuzzGUI.ParameterWindow
{
    public class ParameterVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        readonly ParameterWindowVM window;
        IParameter parameter;
        readonly int track;
        readonly IParameterMetadata metadata;

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

        public double Value01
        {
            get { return (Value - MinValue) / (double)(MaxValue - MinValue); }
            set { Value = (int)(MinValue + value * (MaxValue - MinValue)); }
        }

        public double Indicator
        {
            get { return metadata != null ? metadata.Indicator : -1.0; }
        }

        readonly SimpleCommand bindMIDICommand;
        readonly SimpleCommand unbindMIDICommand;
        public ICommand UnbindMIDICommand { get { return unbindMIDICommand; } }
        public ICommand CopyPresetCommand { get { return window.CopyPresetCommand; } }
        public ICommand PastePresetCommand { get { return window.PastePresetCommand; } }

        public ParameterVM(ParameterWindowVM wnd, IParameter p, int t, IParameterMetadata metadata)
        {
            window = wnd;
            parameter = p;
            track = t;
            this.metadata = metadata;
            parameter.SubscribeEvents(track, ParameterValueChanged, ParameterValueDescriptionChanged);
            p.PropertyChanged += ParameterPropertyChanged;
            if (metadata != null) metadata.PropertyChanged += metadata_PropertyChanged;

            isLocked = p.Group.Type == ParameterGroupType.Input;

            bindMIDICommand = new SimpleCommand
            {
                CanExecuteDelegate = x => (x as int?) != null,
                ExecuteDelegate = x => { parameter.BindToMIDIController(track, (int)x); }
            };

            unbindMIDICommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x => { parameter.Group.Machine.UnbindAllMIDIControllers(); }
            };
        }

        public void Release()
        {
            parameter.UnsubscribeEvents(track, ParameterValueChanged, ParameterValueDescriptionChanged);
            parameter.PropertyChanged -= ParameterPropertyChanged;
            if (metadata != null) metadata.PropertyChanged -= metadata_PropertyChanged;
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

        void ParameterPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "Name":
                    PropertyChanged.Raise(this, e.PropertyName);
                    break;
            }
        }

        void metadata_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "Indicator":
                    PropertyChanged.Raise(this, e.PropertyName);
                    break;
            }
        }

        static readonly Random rnd = new Random();

        public void ApplyEditFunction()
        {
            if (IsLocked) return;

            int vala = window.PresetA.GetValueOrDefault(parameter, track);
            int valb = window.PresetB.GetValueOrDefault(parameter, track);
            double alpha = window.EditFunctionParameter;

            // TODO: use pi for all EditFunctions
            var values = metadata != null ? metadata.GetValidParameterValues(track).ToArray() : new int[] { };

            switch (window.EditFunction)
            {
                case ParameterWindowVM.EditFunctions.RandomizeAbsolute:
                    if (values.Length > 0)
                        Value = Math.Min(Math.Max(values[rnd.Next(values.Length)], parameter.MinValue), parameter.MaxValue);
                    else
                        Value = rnd.Next(parameter.MinValue, parameter.MaxValue + 1);
                    break;

                case ParameterWindowVM.EditFunctions.RandomizeRelative:
                    int range = parameter.MaxValue - parameter.MinValue;
                    Value = Math.Min(Math.Max((int)Math.Round(parameter.GetValue(track) + alpha * alpha * rnd.Next(-range, range + 1)), parameter.MinValue), parameter.MaxValue);
                    break;

                case ParameterWindowVM.EditFunctions.RandomizeAB:
                    Value = Math.Min(Math.Max((int)Math.Round(vala + rnd.NextDouble() * (valb - vala)), parameter.MinValue), parameter.MaxValue);
                    break;

                case ParameterWindowVM.EditFunctions.RandomizeAorB:
                    Value = alpha < rnd.NextDouble() ? vala : valb;
                    break;

                case ParameterWindowVM.EditFunctions.RandomizeMaybe:
                    if (rnd.NextDouble() < alpha * alpha)
                    {
                        if (values.Length > 0)
                            Value = Math.Min(Math.Max(values[rnd.Next(values.Length)], parameter.MinValue), parameter.MaxValue);
                        else
                            Value = rnd.Next(parameter.MinValue, parameter.MaxValue + 1);
                    }
                    else
                    {
                        Value = vala;
                    }
                    break;

                case ParameterWindowVM.EditFunctions.MorphAB:
                    Value = Math.Min(Math.Max((int)Math.Round(vala + alpha * (valb - vala)), parameter.MinValue), parameter.MaxValue);
                    break;
            }

            ParameterValueChanged(parameter, track);		// makes the gui update quickly
        }

        public IList<MenuItemVM> MIDIControllers
        {
            get
            {
                var mc = parameter.Group.Machine.DLL.Buzz.MIDIControllers;
                List<MenuItemVM> l = new List<MenuItemVM>();
                if (mc.Count > 0)
                {
                    int index = 0;
                    foreach (string s in mc)
                    {
                        l.Add(new MenuItemVM() { Text = s, Command = bindMIDICommand, CommandParameter = index, IsEnabled = true });
                        index++;
                    }
                }
                else
                {
                    l.Add(new MenuItemVM() { Text = "(no controllers defined)", Command = bindMIDICommand });
                }

                return l;
            }

        }

        public IEnumerable<MenuItemVM> Attributes { get { return parameter.Group.Machine.GetAttributeMenuItems(); } }
        public bool HasAttributes { get { return window.HasAttributes; } }

        public Visibility LockButtonVisibility { get { return window.EditGridVisibility; } }

        bool isLocked;
        public bool IsLocked
        {
            get { return isLocked; }
            set
            {
                isLocked = value;
                PropertyChanged.Raise(this, "IsLocked");
            }
        }

        public SimpleCommand UnlockAllCommand { get { return window.UnlockAllCommand; } }
        public SimpleCommand InvertLocksCommand { get { return window.InvertLocksCommand; } }
        public SimpleCommand SettingsCommand { get { return window.SettingsCommand; } }

        public SimpleCommand AddTrackCommand { get { return window.AddTrackCommand; } }
        public SimpleCommand DeleteLastTrackCommand { get { return window.DeleteLastTrackCommand; } }


    }
}
