using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
//using PropertyChanged;

namespace BuzzGUI.MachineView
{
    /// <summary>
    /// Interaction logic for CreateTemplateWindow.xaml
    /// </summary>
    /// 

    //[DoNotNotify]
    public partial class LatencyWindow : Window, INotifyPropertyChanged
    {
        bool isOverridden;
        public bool IsOverridden
        {
            get { return isOverridden; }
            set
            {
                isOverridden = value;
                PropertyChanged.Raise(this, "IsOverridden");
                Validate();
            }
        }
        public int OverrideLatency { get; set; }

        public BuzzGUI.Common.Settings.EngineSettings EngineSettings { get { return Global.EngineSettings; } }

        bool invalid;
        private IMachine machine;
        readonly Brush textBoxForegroundBrush;

        public LatencyWindow(IMachine machine)
        {
            DataContext = this;

            this.machine = machine;

            InitializeComponent();

            textBoxForegroundBrush = textbox.Foreground;

            IsOverridden = machine.OverrideLatency >= 0;
            OverrideLatency = machine.OverrideLatency;

            textbox.Text = IsOverridden ? OverrideLatency.ToString() : "0";
            Title = "Latency - " + machine.Name;

            if (machine.DLL.Info.Version >= 27)
            {
                status.Text = "Latency as reported by the machine: " + machine.Latency + " samples";
            }
            else
            {
                status.Foreground = Brushes.DarkBlue;
                status.Text = "The machine does not support latency reporting (too old for that).";
            }

            textbox.TextChanged += (sender, e) =>
            {
                Validate();
                if (!invalid) OverrideLatency = int.Parse(textbox.Text);
            };

            checkbox.Checked += (sender, e) =>
            {
                Validate();
                if (!invalid) OverrideLatency = int.Parse(textbox.Text);
            };

            okButton.Click += (sender, e) =>
            {
                this.DialogResult = true;
                Validate();
                if (!invalid) OverrideLatency = int.Parse(textbox.Text);
                Close();
            };

            cancelButton.Click += (sender, e) =>
            {
                this.DialogResult = false;
                Close();
            };

            KeyDown += (sender, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (e.Key == Key.O)
                    {
                        foreach (var m in Global.Buzz.Song.Machines)
                            m.OverrideLatency = -1;
                    }
                }
                
            };

            Loaded += (sender, e) =>
            {
                machine.PropertyChanged += Machine_PropertyChanged;
            };

            Unloaded += (sender, e) =>
            {
                machine.PropertyChanged -= Machine_PropertyChanged;
            };

            Validate();
        }

        private void Machine_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "OverrideLatency")
            {
                OverrideLatency = machine.OverrideLatency;
                IsOverridden = OverrideLatency >= 0;
                textbox.Text = IsOverridden ? OverrideLatency.ToString() : "0";
                Validate();
            }
        }

        void Validate()
        {
            int value = 0;
            invalid = IsOverridden && !Int32.TryParse(textbox.Text, out value) || value < 0;
            textbox.Foreground = invalid ? Brushes.Red : textBoxForegroundBrush;
            okButton.IsEnabled = !invalid;
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
