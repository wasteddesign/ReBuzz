using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using BuzzGUI.Common;

namespace ReBuzz.Common
{
    /// <summary>
    /// Interaction logic for LoadWindow.xaml
    /// </summary>
    public partial class StatusWindow : Window
    {
        public StatusWindow(string wndName)
        {
            InitializeComponent();

            this.Title = BuzzGUI.Common.Win32.CompactPath(wndName, 40);
            var md = Utils.GetUserControlXAML<Window>("ParameterWindowShell.xaml", Global.BuzzPath);
            if (md != null)
            {
                this.Resources.MergedDictionaries.Add(md.Resources);
            }

            this.Closing += (sender, e) =>
            {
                e.Cancel = true;
            };
        }

        public void UpdateText(string text)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                tbInfo.Text = text;
            }));
        }

        public static StatusWindow CreateAsync(string wndName, Window parentWnd)
        {
            StatusWindow window = null;

            var pos = parentWnd.PointToScreen(new Point(0, 0));
            double pWidth = parentWnd.Width;
            double pHeight = parentWnd.Height;

            // Launch window in its own thread with a specific size and position
            var windowThread = new Thread(() =>
            {
                window = new StatusWindow(wndName);

                window.Left = pos.X + pWidth / 2 - window.Width / 2;
                window.Top = pos.Y + pHeight / 2 - window.Height / 2;

                window.Show();
                window.Closed += window.OnWindowClosed;
                Dispatcher.Run();
            });
            windowThread.SetApartmentState(ApartmentState.STA);
            windowThread.Start();

            // Wait until the new thread has created the window
            while (window == null) { Thread.Sleep(0); /* Allow the other UI rendering thread to process... */ }

            // The window has been created, so return a reference to it
            return window;
        }

        private void OnWindowClosed(object sender, EventArgs args)
        {
            Dispatcher.InvokeShutdown();
        }

        internal void CloseWindow()
        {
            Dispatcher.InvokeShutdown();
        }
    }
}
