using BuzzGUI.Common;
using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ReBuzz.Common
{
    /// <summary>
    /// Interaction logic for LoadWindow.xaml
    /// </summary>
    public partial class SplashScreenWindow : Window, INotifyPropertyChanged
    {
        public bool FadeOut { get; set; }
        public SplashScreenWindow(string wndName)
        {
            InitializeComponent();
            DataContext = this;

            //this.Title = BuzzGUI.Common.Win32.CompactPath(wndName, 40);
            var md = Utils.GetUserControlXAML<Window>("ParameterWindowShell.xaml");
            if (md != null)
            {
                //this.Resources.MergedDictionaries.Add(md.Resources);
            }

            int img = new Random(DateTime.Now.Millisecond).Next(4);
            Uri uriImg = new Uri("pack://application:,,,/Common/gfx/splashscreen1.jpeg");
            switch (img)
            {
                case 1:
                    uriImg = new Uri("pack://application:,,,/Common/gfx/fox.jpeg");
                    break;
                case 2:
                    uriImg = new Uri("pack://application:,,,/Common/gfx/bear.jpeg");
                    break;
                case 3:
                    uriImg = new Uri("pack://application:,,,/Common/gfx/squirrel.jpeg");
                    break;
            }

            bgImage.Source = new BitmapImage(uriImg);


            this.Closing += (sender, e) =>
            {
                e.Cancel = true;
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;
        bool IsClosed { get; set; }
        public void UpdateText(string text)
        {
            if (!IsClosed)
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    tbStatus.Text = text + "...";
                }));
            }
        }

        public static SplashScreenWindow CreateAsync(string wndName, Window parentWnd)
        {
            SplashScreenWindow window = null;

            //var pos = parentWnd.PointToScreen(new Point(0, 0));
            //double pWidth = parentWnd.Width;
            //double pHeight = parentWnd.Height;

            // Launch window in its own thread with a specific size and position
            var windowThread = new Thread(() =>
            {
                window = new SplashScreenWindow(wndName);

                //window.Left = pos.X + pWidth / 2 - window.Width / 2;
                //window.Top = pos.Y + pHeight / 2 - window.Height / 2;
#if !DEBUG
                window.Show();
#endif
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
            IsClosed = true;
            Dispatcher.InvokeShutdown();
        }

        internal void CloseWindow()
        {
            FadeOut = true;
            PropertyChanged.Raise(this, "FadeOut");
            DispatcherTimer dt = new DispatcherTimer();
            dt.Interval = TimeSpan.FromSeconds(2);
            dt.Tick += ((sender, e) =>
            {
                Dispatcher.InvokeShutdown();
                dt.Stop();
            });
            dt.Start();

        }
    }
}
