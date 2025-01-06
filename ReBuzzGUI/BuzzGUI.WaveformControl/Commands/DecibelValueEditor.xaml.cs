using BuzzGUI.Common;
using System.Windows;
using System.Windows.Input;

namespace BuzzGUI.WaveformControl.Commands
{
    /// <summary>
    /// Interaction logic for DecibelValueEditor.xaml
    /// </summary>
    public partial class DecibelValueEditor : Window
    {
        double newAmp;
        public DecibelValueEditor()
        {
            InitializeComponent();
            this.KeyDown += new KeyEventHandler(textBox_KeyDown);

            gainKnob.Minimum = 0.0;
            gainKnob.Maximum = 4.0;

            gainKnob.Value = 1.0;
        }

        public double Value { get { return newAmp; } }

        private void gainKnob_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            newAmp = gainKnob.Value;
            UpdateGainText();

        }
        void UpdateGainText()
        {
            double v = newAmp;
            dbTextBlock.Text = v > 0 ? string.Format("{0:F1}dB", Decibel.FromAmplitude(v)) : "-inf.dB";
        }

        void textBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.DialogResult = false;
                this.Close();
                e.Handled = true;
            }

            if (e.Key == Key.Return)
            {
                this.DialogResult = true;
                this.Close();
                e.Handled = true;
            }

        }

        private void accept_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
            e.Handled = true;

        }

    }
}
