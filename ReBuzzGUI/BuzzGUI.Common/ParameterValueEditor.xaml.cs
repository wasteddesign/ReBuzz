using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BuzzGUI.Common
{
    /// <summary>
    /// Interaction logic for ParameterValueEditor.xaml
    /// </summary>
    public partial class ParameterValueEditor : Window
    {
        bool invalid;
        int value = 0;
        readonly int minimum;
        readonly int maximum;

        public int Value { get { return value; } }

        public ParameterValueEditor(int val, int mini, int maxi, bool select)
        {
            this.DataContext = this;
            InitializeComponent();
            textBox.KeyDown += new KeyEventHandler(textBox_KeyDown);
            textBox.TextChanged += new TextChangedEventHandler(textBox_TextChanged);

            /*
			foreach (var re in EventManager.GetRoutedEvents())
			{
				EventManager.RegisterClassHandler(GetType(), re, new RoutedEventHandler((sender, e) =>
				{
					DebugConsole.WriteLine(e.OriginalSource + "=>" + e.RoutedEvent);
				}));
			}
			 */

            this.MouseLeftButtonDown += (sender, e) =>
            {
                this.DialogResult = !invalid;
                this.Close();
                e.Handled = true;
            };

            this.PreviewMouseLeftButtonDown += (sender, e) =>
            {
                if (e.ClickCount == 2)
                {
                    this.DialogResult = !invalid;
                    this.Close();
                    e.Handled = true;
                }
            };

            textBox.Text = val.ToString();
            minimum = mini;
            maximum = maxi;
            text.Text = string.Format("[{0}..{1}]", mini, maxi);

            Validate();
            if (select)
                textBox.SelectAll();
            else
                textBox.Select(textBox.Text.Length, 0);
            textBox.Focus();
        }

        void Validate()
        {
            invalid = !Int32.TryParse(textBox.Text, out value) || value < minimum || value > maximum;
            textBox.Foreground = invalid ? Brushes.Red : Brushes.Black;
        }

        void textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Validate();
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

        public TextFormattingMode TextFormattingMode { get { return Global.GeneralSettings.WPFIdealFontMetrics ? TextFormattingMode.Ideal : TextFormattingMode.Display; } }

    }
}
