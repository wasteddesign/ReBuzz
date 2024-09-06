using System.IO;
using System.Windows;

namespace ReBuzz.Common
{
    /// <summary>
    /// Interaction logic for DebugWindow.xaml
    /// </summary>
    public partial class DebugWindow : Window
    {
        public ConcurrentStreamWriter DebugStreamWriter { get; }
        readonly MemoryStream debugStream;

        public DebugWindow()
        {
            DataContext = this;
            InitializeComponent();

            debugStream = new MemoryStream();
            this.DebugStreamWriter = new ConcurrentStreamWriter(debugStream, tbDebug);

            tbDebugInput.KeyDown += (sender, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    tbDebugInput.Clear();
                }
            };

            Loaded += (sender, e) =>
            {
                var rd = Utils.GetUserControlXAML<Window>("ParameterWindowShell.xaml");
                Resources.MergedDictionaries.Add(rd.Resources);
            };

            Closing += (sender, e) =>
            {
                Hide();
                e.Cancel = true;
            };
        }
    }
}
