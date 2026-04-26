using BuzzGUI.Common;
using System;
using System.Windows;
using System.Windows.Media;

namespace WDE.ConnectionMixer
{
    /// <summary>
    /// Interaction logic for MIDIWindow.xaml
    /// </summary>
    public partial class MIDIWindow : Window
    {
        int valueChannel = 0;
        public int ValueChannel { get { return valueChannel; } }

        int valueCC = 0;
        public int ValueCC { get { return valueCC; } }

        public MIDIWindow(int valChannel, int valCC)
        {
            InitializeComponent();

            valueChannel = valChannel;
            valueCC = valCC;

            tbChannel.Text = "" + valChannel;
            tbCC.Text = "" + valCC;

            Global.Buzz.MIDIInput += Buzz_MIDIInput;

            tbChannel.TextChanged += (s, e) =>
            {
                bool invalid;
                invalid = !Int32.TryParse(tbChannel.Text, out valueChannel) || valueChannel <= 0 || valueChannel > 16;

                tbChannel.Foreground = invalid ? Brushes.Red : Brushes.Black;
            };

            tbCC.TextChanged += (s, e) =>
            {
                bool invalid;
                invalid = !Int32.TryParse(tbCC.Text, out valueCC) || valueCC < 0 || valueCC > 127;

                tbCC.Foreground = invalid ? Brushes.Red : Brushes.Black;
            };

            ok.Click += (sender, e) =>
            {
                DialogResult = true;
            };

            /*
            clear.Click += (sender, e) =>
            {
                valueChannel = 0;
                DialogResult = true;
            };
            */

            cancel.Click += (sender, e) =>
            {
                DialogResult = false;
            };

            this.Unloaded += MIDIWindow_Unloaded;
        }

        private void MIDIWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            Global.Buzz.MIDIInput -= Buzz_MIDIInput;
        }

        private void Buzz_MIDIInput(int mididata)
        {
            int status = (int)(mididata & 0xff);
            int channel = (int)(status & 0xf) + 1;
            int data1 = (int)((mididata >> 8) & 0xff);
            int data2 = (int)((mididata >> 16) & 0xff);

            tbChannel.Text = channel.ToString();
            tbCC.Text = data1.ToString();
        }
    }
}
