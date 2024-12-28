using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace BuzzGUI.SequenceEditor
{
    public partial class StepEditWindow : Window
    {
        bool invalid;
        int step = 0;

        public int Step { get { return step; } }

		public StepEditWindow(int val)
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
            invalid = !Int32.TryParse(textBox.Text, out step) || step < 4 || step > 512;
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
