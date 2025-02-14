using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Pianoroll.GUI
{
    /// <summary>
    /// Interaction logic for NotePropertiesWindow.xaml
    /// </summary>
    public partial class NotePropertiesWindow : Window
    {
        bool invalid;
        int velocity = 0;

        public int Velocity { get { return velocity; } }

        public NotePropertiesWindow(int val)
        {
            InitializeComponent();
            textBox.KeyDown += new KeyEventHandler(textBox_KeyDown);
            textBox.TextChanged += new TextChangedEventHandler(textBox_TextChanged);

            if (val > 0)
            {
                textBox.Text = val.ToString();
                textBox.SelectAll();
                invalid = false;
            }
            else
            {
                invalid = true;
            }


            textBox.Focus();
        }

        void textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            invalid = !Int32.TryParse(textBox.Text, out velocity) || velocity < 1 || velocity > 127;
            textBox.Foreground = invalid ? Brushes.Red : Brushes.Black;
        }

        void textBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.DialogResult = false;
                this.Close();
                e.Handled = true;
            }

            if (e.Key == Key.Return && !invalid)
            {
                this.DialogResult = true;
                this.Close();
                e.Handled = true;
            }

        }
    }
}
