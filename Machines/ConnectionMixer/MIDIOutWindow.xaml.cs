using BuzzGUI.Common;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace WDE.ConnectionMixer
{
    /// <summary>
    /// Interaction logic for MIDIWindow.xaml
    /// </summary>
    public partial class MIDIOutWindow : Window, INotifyPropertyChanged
    {
        public class VMMidiOut
        {
            public int deviceNumber;
            public string deviceName;

            public VMMidiOut()
            {
                deviceNumber = -1;
                deviceName = "";
            }

            public override string ToString()
            {
                return deviceName;
            }
        }

        public List<VMMidiOut> MidiOutDevices { get; set; }

        public MIDIOutWindow(string selectedDeviceName, bool sendBackToAllChannels)
        {
            DataContext = this;
            InitializeComponent();

            cbSendToAll.IsChecked = sendBackToAllChannels;

            if (selectedDeviceName != null)
            {
                tbSelectedMidiOut.Text = selectedDeviceName;
            }
            else
            {
                tbSelectedMidiOut.Text = "<none>";
            }

            MidiOutDevices = [new VMMidiOut() { deviceNumber = -1, deviceName = "<none>" }];

            foreach (var mo in Global.Buzz.GetMidiOuts())
            {
                MidiOutDevices.Add(new VMMidiOut() { deviceNumber = mo.Item1, deviceName = mo.Item2 });
            }

            PropertyChanged.Raise(this, "MidiOutDevices");

            var selectedItem = MidiOutDevices.FirstOrDefault( mo => mo.deviceName == tbSelectedMidiOut.Text );
            if (selectedItem != null)
            {
                bcMidiOuts.SelectedItem = selectedItem;
            }
            else
            {
                bcMidiOuts.SelectedIndex = 0;
            }

            ok.Click += (sender, e) =>
            {
                DialogResult = true;
            };

            cancel.Click += (sender, e) =>
            {
                DialogResult = false;
            };
        }

        public string GetSelectedDeviceName()
        {
            var selectedItem = bcMidiOuts.SelectedItem as VMMidiOut;
            if (selectedItem.deviceNumber == -1)
                return null;
            else
                return selectedItem.deviceName;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
