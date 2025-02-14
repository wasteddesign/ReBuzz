using System.Windows;
using System.Windows.Input;

namespace Pianoroll.GUI
{
    /// <summary>
    /// Interaction logic for HelpWindow.xaml
    /// </summary>
    public partial class HelpWindow : Window
    {
        public HelpWindow()
        {
            InitializeComponent();
            this.KeyDown += new KeyEventHandler(HelpWindow_KeyDown);
            this.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(HelpWindow_MouseLeftButtonDown);
        }

        void HelpWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.Close();
        }

        void HelpWindow_KeyDown(object sender, KeyEventArgs e)
        {
            this.Close();
        }
    }
}
