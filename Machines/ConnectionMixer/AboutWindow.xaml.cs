using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace WDE.ConnectionMixer
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        SolidColorBrush[] C64Brushes = {
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF000000")),
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF626262")),
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF898989")),
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFadadad")),
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFFFF")),
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9f4e44")),
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFcb7e75")),
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6d5412")),
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFa1683c")),
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFc9d487")),
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9ae29b")),
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5cab5e")),
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6abfc6")),
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF887ecb")),
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF50459b")),
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFa057a3"))
        };

        Random rnd = new Random();

        SolidColorBrush foregroundBrush;
        public AboutWindow(string version)
        {
            InitializeComponent();

            foregroundBrush = (SolidColorBrush)TryFindResource("foregroundBrush");

            long availMemory = 0;
            long totalMemory = 0;

            try
            {
                PerformanceCounter ram = new PerformanceCounter("Memory", "Available MBytes", null);
                availMemory = (long)ram.NextValue();
            }
            catch (Exception) { };

            try
            {
                GetPhysicallyInstalledSystemMemory(out totalMemory);
                totalMemory /= 1024;
            }
            catch (Exception) { };

            tbTop.TextAlignment = TextAlignment.Center;
            tbTop.FontFamily = new FontFamily(new Uri("pack://application:,,,/Connection Mixer Console.NET;Component/Resources/"), "./#Commodore 64");
            tbTop.FontSize = 19;
            tbTop.Text = @"**** CONNECTION MIXER CONSOLE V" + version + @" ****

" + totalMemory + " MB RAM SYSTEM " + availMemory + " BASIC MB FREE";

            tbEdit.FontFamily = new FontFamily(new Uri("pack://application:,,,/Connection Mixer Console.NET;Component/Resources/"), "./#Commodore 64");
            tbEdit.FontSize = 19;
            tbEdit.Text = @"READY.


      ___           ___           ___     
     /\  \         /\__\         /\  \    
    /::\  \       /::|  |       /::\  \   
   /:/\:\  \     /:|:|  |      /:/\:\  \  
  /:/  \:\  \   /:/|:|__|__   /:/  \:\  \ 
 /:/__/ \:\__\ /:/ |::::\__\ /:/__/ \:\__
 \:\  \  \/__/ \/__/~~/:/  / \:\  \  \/__
  \:\  \             /:/  /   \:\  \      
   \:\  \           /:/  /     \:\  \     
    \:\__\         /:/  /       \:\__\    
     \/__/         \/__/         \/__/    
";
            this.tbEdit.SelectionChanged += (sender, e) => MoveCustomCaret();
            this.tbEdit.LostFocus += (sender, e) => Caret.Visibility = Visibility.Collapsed;
            this.tbEdit.GotFocus += (sender, e) => Caret.Visibility = Visibility.Visible;

            DispatcherTimer cursorTime = new DispatcherTimer();
            cursorTime.Interval = TimeSpan.FromMilliseconds(500);
            cursorTime.Start();
            cursorTime.Tick += (sender, e) =>
            {
                Caret.Visibility = Caret.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            };

            int borderLoadingCounter = 0;
            DispatcherTimer borderTimer = new DispatcherTimer();
            DispatcherTimer borderLoadingTimer = new DispatcherTimer();
            borderLoadingTimer.Interval = TimeSpan.FromMilliseconds(60);
            borderLoadingTimer.Tick += (sendr, e) =>
            {
                if (borderLoadingCounter < 50)
                {
                    borderLoadingCounter++;
                    mainBorder.BorderBrush = C64Brushes[rnd.Next(C64Brushes.Length - 1)];
                }
                else
                {
                    mainBorder.BorderBrush = foregroundBrush;
                    borderTimer.Interval = TimeSpan.FromMilliseconds(3000 + rnd.Next(3000));
                    borderLoadingTimer.Stop();
                    borderTimer.Start();
                }
            };

            borderTimer.Interval = TimeSpan.FromMilliseconds(3000 + rnd.Next(3000));
            borderTimer.Start();
            borderTimer.Tick += (sender, e) =>
            {
                borderLoadingCounter = 0;
                borderLoadingTimer.Start();
                borderTimer.Stop();
            };

            this.PreviewKeyDown += (sender, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    this.Close();
                }
            };

            tbEdit.Loaded += (sender, e) =>
            {
                tbEdit.Select(tbEdit.Text.Split('\n').First().Length, 0);
                tbEdit.Focus();
                tbEdit.ScrollToEnd();
                MoveCustomCaret();
            };

            tbEdit.PreviewKeyDown += (sender, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    foreach (var line in tbEdit.Text.Split('\n'))
                    {
                        if (line.Trim().ToLower() == "close")
                            this.Close();
                    }
                }
            };

            mainBorder.MouseLeftButtonDown += (sender, e) =>
            {
                this.Close();
                e.Handled = true;
            };
        }

        private void MoveCustomCaret()
        {
            var caretLocation = tbEdit.GetRectFromCharacterIndex(tbEdit.CaretIndex).Location;

            if (!double.IsInfinity(caretLocation.X))
            {
                Canvas.SetLeft(Caret, caretLocation.X + 1);
            }

            if (!double.IsInfinity(caretLocation.Y))
            {
                Canvas.SetTop(Caret, caretLocation.Y);
            }
        }


        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetPhysicallyInstalledSystemMemory(out long TotalMemoryInKilobytes);

    }
}
