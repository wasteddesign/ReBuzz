using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.ComponentModel;
using System.Windows;
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
        readonly Brush textBoxForegroundBrush;

        public LatencyWindow(IMachine machine)
        {
            DataContext = this;

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

            okButton.Click += (sender, e) =>
            {
                this.DialogResult = true;
                Close();
            };

            cancelButton.Click += (sender, e) =>
            {
                this.DialogResult = false;
                Close();
            };


            Validate();
        }

        void Validate()
        {
            int value = 0;
            invalid = IsOverridden && !Int32.TryParse(textbox.Text, out value) || value < 0;
            textbox.Foreground = invalid ? Brushes.Red : textBoxForegroundBrush; ;
            okButton.IsEnabled = !invalid;
        }


        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
