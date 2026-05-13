using BuzzGUI.Common;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Interop;


namespace BuzzGUI.Common
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
                    int.Parse(tbController.Text);
                    int.Parse(tbChannel.Text);
                    if (!hideValue)
                    {
                        int.Parse(tbValue.Text);
                    }
                    if (!quickAssign)
                    {
                        if (tbName.Text.Trim().Length > 0)
                        {
                            return true;
                        }
                    }
                    else
                    {
                        return true;
                    }
                }
                catch
                { }

                return false;
            }
        }

        public MidiControllerAssignWindow(bool quickAssign = false, bool hideValue = false)
        {

            var md = GetThemeResources();
            if (md != null)
            {
                this.Resources.MergedDictionaries.Add(md);
            }

            DataContext = this;
            InitializeComponent();

            this.quickAssign = quickAssign;
            this.hideValue = hideValue;

            if (quickAssign)
            {
                mainGrid.RowDefinitions[4].Height = new GridLength(0);
            }

            if (hideValue)
            {
                mainGrid.RowDefinitions[3].Height = new GridLength(0);
            }

            btOk.Click += (sender, e) =>
            {
                DialogResult = true;
                this.Close();
                Global.Buzz.MIDIInput -= Buzz_MIDIInput;
            };

            Global.Buzz.MIDIInput += Buzz_MIDIInput;

            tbController.TextChanged += (sender, e) =>
            {
                IsInputValid = false;
            };

            tbChannel.TextChanged += (sender, e) =>
            {
                IsInputValid = false;
            };

            tbValue.TextChanged += (sender, e) =>
            {
                IsInputValid = false;
            };

            tbName.TextChanged += (sender, e) =>
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
                MidiController = "" + data1;
                MidiChannel = "" + (channel + 1);
                MidiValue = "" + data2;
            }
        }
        private bool quickAssign;
        private bool hideValue;

        string controllerName;
        public string ControllerName { get => controllerName; set { controllerName = value; PropertyChanged.Raise(this, "ControllerName"); } }
        string midiChannel;
        public string MidiChannel { get => midiChannel; set { midiChannel = value; PropertyChanged.Raise(this, "MidiChannel"); } }
        string midiController;
        public string MidiController { get => midiController; set { midiController = value; PropertyChanged.Raise(this, "MidiController"); } }
        string midiValue;
        public string MidiValue { get => midiValue; set { midiValue = value; PropertyChanged.Raise(this, "MidiValue"); } }

        internal ResourceDictionary GetThemeResources()
        {
            ResourceDictionary skin = new ResourceDictionary();

            try
            {
                string selectedTheme = Global.Buzz.SelectedTheme == "<default>" ? "Default" : Global.Buzz.SelectedTheme;
                string skinPath = Global.BuzzPath + "\\Themes\\" + selectedTheme + "\\MainWindow.xaml";
                skin = (ResourceDictionary)XamlReaderEx.LoadHack(skinPath);
            }
            catch (Exception)
            {
                string skinPath = Global.BuzzPath + "\\Themes\\Default\\MainWindow.xaml";
                skin.Source = new Uri(skinPath, UriKind.Absolute);
            }

            return skin;
        }
    }
}
