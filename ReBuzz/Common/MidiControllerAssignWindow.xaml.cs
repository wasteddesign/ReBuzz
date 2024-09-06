using BuzzGUI.Common;
using System.ComponentModel;
using System.Windows;

namespace ReBuzz.Common
{
    /// <summary>
    /// Interaction logic for MidiControllerAssignWindow.xaml
    /// </summary>
    public partial class MidiControllerAssignWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public bool IsInputValid
        {
            set
            {
                PropertyChanged.Raise(this, "IsInputValid");
            }
            get
            {
                try
                {
                    int.Parse(Controller.Text);
                    int.Parse(Channel.Text);
                    int.Parse(Value.Text);
                    if (Name.Text.Trim().Length > 0)
                    {
                        return true;
                    }
                }
                catch
                { }

                return false;
            }
        }

        public MidiControllerAssignWindow()
        {
            DataContext = this;
            InitializeComponent();

            btOk.Click += (sender, e) =>
            {
                DialogResult = true;
                this.Close();
                Global.Buzz.MIDIInput -= Buzz_MIDIInput;
            };

            Global.Buzz.MIDIInput += Buzz_MIDIInput;

            Controller.TextChanged += (sender, e) =>
            {
                IsInputValid = false;
            };

            Channel.TextChanged += (sender, e) =>
            {
                IsInputValid = false;
            };

            Value.TextChanged += (sender, e) =>
            {
                IsInputValid = false;
            };

            Name.TextChanged += (sender, e) =>
            {
                IsInputValid = false;
            };
        }

        private void Buzz_MIDIInput(int data)
        {
            int b = MIDI.DecodeStatus(data);
            int data1 = MIDI.DecodeData1(data);
            int data2 = MIDI.DecodeData2(data);
            int channel = 0;
            int commandCode = MIDI.ControlChange;

            if ((b & 0xF0) == 0xF0)
            {
                // both bytes are used for command code in this case
                commandCode = b;
            }
            else
            {
                commandCode = (b & 0xF0);
                channel = (b & 0x0F);
            }

            if (commandCode == MIDI.ControlChange)
            {
                Controller.Text = "" + data1;
                Channel.Text = "" + (channel + 1);
                Value.Text = "" + data2;
            }
        }

        internal void SelectedController(PreferencesWindow.ControllerVM cVM)
        {
            Name.Text = cVM.Name;
            Controller.Text = "" + cVM.Controller;
            Channel.Text = "" + cVM.Channel;
            Value.Text = "" + cVM.Value;
        }
    }
}
