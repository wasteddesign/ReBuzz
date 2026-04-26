using BuzzGUI.Common;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Resources;
using System.Windows.Threading;

namespace WDE.AudioBlock
{
    class AboutWindow : Window
    {
        private string audioBlockVersion;
        private string aboutText;
        private int numLinesToShow = 24;

        private static string defaultAboutText = @"AudioBlock Version {0}
(C) 2024 WDE

r8brain-free-src 2013-2019 Aleksey Vaneev
Managed Wrapper for LibSampleRate (C) 2015 Mario Guggenberger
Spline code (C) Ryan Seghers
SoundTouch library Copyright (C) Olli Parviainen 2001-2019
Free code examples around the Web (C) Respected owners
(C) Robotman for Amiga cursor
(C) Alan Tinsley for 'Topaz' font
Scott W Harden, Spectrogram
Mark Heath, NAudio
Jean-Marc Valin, rnnoise
Jagger, rnnoise-windows

Big thanks to:
Oskari Tammelin for Jeskola Buzz
IXix, polac, UNZ and many other Buzz plugin developers.

...and Buzz community helping to improve AudioBlock!

...
            
And keep buzzing :)
                   
";

        private Border guruBorder;
        TextBlock tbGuru;
        private TextBlock tbContent;

        private DispatcherTimer dispatcherTimer;
        private DispatcherTimer dispatcherContentText;
        private DispatcherTimer waitTimer;
        private DispatcherTimer blurTimer;
        private Image imageMouse;
        private int aboutTextTypingPos = 0;

        private Canvas mainCanvas;

        private Cursor cursor;
        Grid aboutGrid;

        Random rnd = new Random();

        public AboutWindow(string ver, string txt = "")
        {
            audioBlockVersion = ver;
            aboutText = txt;
            if (txt == "")
            {
                aboutText = String.Format(defaultAboutText, audioBlockVersion);
            }

            this.Width = 640;
            this.Height = 500;

            this.Background = Brushes.Black;

            this.WindowStyle = WindowStyle.None;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;

            WindowInteropHelper helper = new WindowInteropHelper(this);
            helper.Owner = Global.Buzz.MachineViewHWND;

            aboutGrid = new Grid() { HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(6) };
            aboutGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(80) });
            aboutGrid.RowDefinitions.Add(new RowDefinition());

            guruBorder = new Border();
            guruBorder.BorderThickness = new Thickness(6);
            guruBorder.BorderBrush = Brushes.Red;

            tbGuru = new TextBlock() { VerticalAlignment = VerticalAlignment.Center };
            tbGuru.Text = "Software Failure. Press left mouse button to continue.\n" +
                "Guru Meditation #00000004.000" + audioBlockVersion;
            tbGuru.Foreground = Brushes.Red;
            tbGuru.TextAlignment = TextAlignment.Center;
            tbGuru.FontFamily = new FontFamily(new Uri("pack://application:,,,/AudioBlock.NET;Component/Resources/"), "./#Topaz New");
            tbGuru.FontSize = 18;
            tbGuru.Effect = new DropShadowEffect() { BlurRadius = 10, Color = Colors.Aquamarine, Opacity = 0 };
            //tbGuru.RenderTransform = new SkewTransform() { AngleX = 10  };
            guruBorder.Child = tbGuru;

            Grid.SetRow(guruBorder, 0);
            aboutGrid.Children.Add(guruBorder);

            tbContent = new TextBlock() { HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(6, 20, 6, 20) };
            tbContent.Foreground = Brushes.Red;
            tbContent.TextAlignment = TextAlignment.Center;
            tbContent.FontFamily = new FontFamily(new Uri("pack://application:,,,/AudioBlock.NET;Component/Resources/"), "./#Topaz New");
            //tbContent.RenderTransform = new SkewTransform() { AngleX = 10 };
            tbContent.FontSize = 18;
            tbContent.Effect = new DropShadowEffect() { BlurRadius = 10, Color = Colors.Aquamarine, Opacity = 0 };

            Grid.SetRow(tbContent, 1);
            aboutGrid.Children.Add(tbContent);

            dispatcherContentText = new DispatcherTimer();
            dispatcherTimer = new DispatcherTimer();
            waitTimer = new DispatcherTimer();
            blurTimer = new DispatcherTimer();

            StreamResourceInfo sriCursor = Application.GetResourceStream(new Uri("pack://application:,,,/AudioBlock.NET;Component/Resources/pointer.cur"));
            cursor = new Cursor(sriCursor.Stream);

            mainCanvas = new Canvas();
            mainCanvas.Width = this.Width;
            mainCanvas.Height = this.Height;

            this.Content = aboutGrid;

            this.MouseDown += AboutWindow_MouseDown;
            this.MouseEnter += AboutWindow_MouseEnter;
            this.MouseMove += AboutWindow_MouseMove;
            this.MouseLeave += AboutWindow_MouseLeave;

            this.Loaded += AboutWindow_Loaded;
            this.Closed += AboutWindow_Closed;

            imageMouse = new Image();
            imageMouse.Source = new BitmapImage(new Uri("pack://application:,,,/AudioBlock.NET;Component/Resources/pointer.cur"));
            imageMouse.Stretch = Stretch.None;
            imageMouse.Opacity = 0.0;
            mainCanvas.Children.Add(imageMouse);

            aboutGrid.Children.Add(mainCanvas);
        }

        private void AboutWindow_MouseLeave(object sender, MouseEventArgs e)
        {
            imageMouse.Opacity = 0;

        }

        Point mousePosPrev = new Point(-100, -100);

        private void AboutWindow_MouseMove(object sender, MouseEventArgs e)
        {
            Point mousePos = e.GetPosition(this);

            double direction = Math.Atan2(mousePos.X - this.Width / 2, mousePos.Y + 400.0) * (180 / Math.PI) + 270;

            double opacity = (400.0 - mousePos.Y) / 400.0;
            opacity = opacity < 0 ? 0 : opacity;

            ((DropShadowEffect)(tbGuru.Effect)).Opacity = opacity;
            ((DropShadowEffect)(tbGuru.Effect)).Direction = direction;

            imageMouse.Opacity = 0.5;
            Canvas.SetLeft(imageMouse, mousePosPrev.X);
            Canvas.SetTop(imageMouse, mousePosPrev.Y);

            mousePosPrev = new Point(mousePos.X - imageMouse.ActualWidth / 4.0, mousePos.Y - imageMouse.ActualHeight / 4.0);
        }

        private void AboutWindow_Closed(object sender, EventArgs e)
        {
            dispatcherTimer.Stop();
            dispatcherContentText.Stop();
            waitTimer.Stop();
            blurTimer.Stop();
        }

        private void AboutWindow_Loaded(object sender, RoutedEventArgs e)
        {
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 800);
            dispatcherTimer.Tick += DispatcherTimer_Tick;
            dispatcherTimer.Start();

            // Wait 3 seconds before starting to type text

            waitTimer.Interval = new TimeSpan(0, 0, 0, 3, 0);
            waitTimer.Tick += WaitTimer_Tick;
            waitTimer.Start();

            blurTimer.Interval = new TimeSpan(0, 0, 0, 0, (int)(rnd.NextDouble() * 2000.0));
            blurTimer.Tick += BlurTimer_Tick;
            blurTimer.Start();
        }

        Point movePos;

        double skewVal = 0;
        private void BlurTimer_Tick(object sender, EventArgs e)
        {
            DoubleAnimation da = new DoubleAnimation();
            da.From = 0;
            da.To = 6;
            da.Duration = new Duration(TimeSpan.FromSeconds(0.05));
            da.Completed += Da_Completed_Blur1;

            BlurEffect b = new BlurEffect();
            this.Effect = b;
            b.BeginAnimation(BlurEffect.RadiusProperty, da);

            movePos = new Point(rnd.NextDouble() * 12.0 - 6.0, rnd.NextDouble() * 12.0 - 6.0);

            ThicknessAnimation ta = new ThicknessAnimation()
            {
                From = new Thickness(6),
                To = new Thickness(6 - movePos.X, 6 - movePos.Y, 6 + movePos.X, 6 + movePos.Y),
                Duration = new Duration(TimeSpan.FromSeconds(0.06))
            };
            ta.Completed += Ta_Completed;
            aboutGrid.BeginAnimation(Grid.MarginProperty, ta);


            skewVal = rnd.NextDouble() * 4.0 - 2.0;
            DoubleAnimation daSkew = new DoubleAnimation();
            daSkew.From = 0;
            daSkew.To = skewVal;
            daSkew.Duration = new Duration(TimeSpan.FromSeconds(0.06));
            daSkew.Completed += DaSkew_Completed;

            this.RenderTransform = new SkewTransform() { CenterY = this.Height / 20.0, CenterX = this.Width / 2.0 };
            this.RenderTransform.BeginAnimation(SkewTransform.AngleXProperty, daSkew);
        }

        private void Ta_Completed(object sender, EventArgs e)
        {

            ThicknessAnimation ta = new ThicknessAnimation()
            {
                From = new Thickness(6 - movePos.X, 6 - movePos.Y, 6 + movePos.X, 6 + movePos.Y),
                To = new Thickness(6),
                Duration = new Duration(TimeSpan.FromSeconds(0.03))
            };
            aboutGrid.BeginAnimation(Grid.MarginProperty, ta);
        }

        private void DaSkew_Completed(object sender, EventArgs e)
        {
            DoubleAnimation daSkew = new DoubleAnimation();
            daSkew.From = skewVal;
            daSkew.To = 0;
            daSkew.Duration = new Duration(TimeSpan.FromSeconds(0.03));

            this.RenderTransform = new SkewTransform() { CenterY = this.Height / 20.0, CenterX = this.Width / 2.0 };
            this.RenderTransform.BeginAnimation(SkewTransform.AngleXProperty, daSkew);

        }

        private void Da_Completed_Blur1(object sender, EventArgs e)
        {
            DoubleAnimation da = new DoubleAnimation();
            da.From = 6;
            da.To = 0;

            da.Duration = new Duration(TimeSpan.FromSeconds(0.15));
            da.Completed += Da_Completed_Blur2;

            tbContent.Foreground = Brushes.OrangeRed;

            ((DropShadowEffect)(tbContent.Effect)).Opacity = 0.5;
            ((DropShadowEffect)(tbContent.Effect)).Direction = rnd.NextDouble() * 360;
            ((DropShadowEffect)(tbContent.Effect)).ShadowDepth = rnd.NextDouble() * 10.0;

            BlurEffect b = new BlurEffect();
            this.Effect = b;
            b.BeginAnimation(BlurEffect.RadiusProperty, da);
        }

        private void Da_Completed_Blur2(object sender, EventArgs e)
        {
            tbContent.Foreground = Brushes.Red;
            blurTimer.Interval = new TimeSpan(0, 0, 0, 0, (int)(rnd.NextDouble() * 2000.0));
            blurTimer.Start();

            ((DropShadowEffect)(tbContent.Effect)).Opacity = 0.0;
        }

        private void WaitTimer_Tick(object sender, EventArgs e)
        {
            waitTimer.Stop();
            dispatcherContentText = new DispatcherTimer();
            dispatcherContentText.Interval = new TimeSpan(0, 0, 0, 0, (int)(GetTypingDelay() * 999.0));
            dispatcherContentText.Tick += DispatcherContentText_Tick;
            dispatcherContentText.Start();
        }

        private void DispatcherContentText_Tick(object sender, EventArgs e)
        {
            aboutTextTypingPos++;

            if (aboutTextTypingPos >= defaultAboutText.Length)
            {
                aboutTextTypingPos = 0;
                StreamResourceInfo sriCursor = Application.GetResourceStream(new Uri("pack://application:,,,/AudioBlock.NET;Component/Resources/busy.cur"));
                cursor = new Cursor(sriCursor.Stream);
                this.Cursor = cursor;

                mainCanvas.Children.Clear();

                imageMouse = new Image();
                imageMouse.Source = new BitmapImage(new Uri("pack://application:,,,/AudioBlock.NET;Component/Resources/busy.cur"));
                imageMouse.Stretch = Stretch.None;
                imageMouse.Opacity = 0.0;
                mainCanvas.Children.Add(imageMouse);
            }

            string txt = aboutText.Substring(0, aboutTextTypingPos);
            int numLines = txt.Length - txt.Replace(Environment.NewLine, string.Empty).Length;

            if (numLines > numLinesToShow)
                txt = DeleteLines(txt, numLines - numLinesToShow);

            tbContent.Text = txt;

            dispatcherContentText.Interval = new TimeSpan(0, 0, 0, 0, (int)(GetTypingDelay() * 699));
            dispatcherContentText.Start();
        }

        public static string DeleteLines(string s, int linesToRemove)
        {
            return s.Split(Environment.NewLine.ToCharArray(),
                           linesToRemove + 1
                ).Skip(linesToRemove)
                .FirstOrDefault();
        }

        private double GetTypingDelay()
        {
            return rnd.NextDouble() * 0.1 + 0.1;
        }

        private void DispatcherTimer_Tick(object sender, EventArgs e)
        {
            guruBorder.BorderBrush = guruBorder.BorderBrush == Brushes.Red ? Brushes.Black : Brushes.Red;
        }

        private void AboutWindow_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            this.Cursor = cursor;
        }

        private void AboutWindow_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.Close();
        }
    }
}
