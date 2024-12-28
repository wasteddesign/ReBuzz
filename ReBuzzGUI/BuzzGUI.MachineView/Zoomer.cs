using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
//using PropertyChanged;

namespace BuzzGUI.MachineView
{

    //[DoNotNotify]
    public class Zoomer : INotifyPropertyChanged
    {
        public int LevelCount { get; set; }
        public Action Reset { get; set; }
        public bool Ctrl { get; set; }

        int level;
        public double Level
        {
            get { return level; }
            set
            {
                level = (int)Math.Round(value);

                DependencyObject o = element;
                do { o = VisualTreeHelper.GetParent(o); } while (!(o is ScrollViewer));
                var sv = o as ScrollViewer;

                double s = Math.Pow(2.0, Level / LevelCount);

                //				Point cp = e.GetPosition(value);
                //				Point p = e.GetPosition(sv);

                ScaleTransform st = new ScaleTransform(s, s);
                element.LayoutTransform = st;

                sv.ScrollToHorizontalOffset(element.ActualWidth / 2 * s - sv.ViewportWidth / 2);
                sv.ScrollToVerticalOffset(element.ActualHeight / 2 * s - sv.ViewportHeight / 2);

            }
        }

        FrameworkElement element;
        public FrameworkElement Element
        {
            set
            {
                element = value;
                DependencyObject o = value;
                do { o = VisualTreeHelper.GetParent(o); } while (!(o is ScrollViewer));
                var sv = o as ScrollViewer;

                int acc = 0;

                value.PreviewMouseWheel += (sender, e) =>
                {
                    if (!Ctrl || Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        acc += e.Delta;
                        int l = (int)Math.Round(Level);

                        if (acc >= 120)
                        {
                            acc -= 120;
                            l++;
                        }
                        else if (e.Delta <= -120)
                        {
                            acc += 120;
                            l--;
                        }

                        level = Math.Min(Math.Max(l, -LevelCount), LevelCount);

                        double s = Math.Pow(2.0, Level / LevelCount);

                        Point cp = e.GetPosition(value);
                        Point p = e.GetPosition(sv);

                        ScaleTransform st = new ScaleTransform(s, s);
                        value.LayoutTransform = st;

                        e.Handled = true;

                        sv.ScrollToHorizontalOffset(cp.X * s - p.X);
                        sv.ScrollToVerticalOffset(cp.Y * s - p.Y);

                        if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Level"));
                    }

                };

                value.MouseDown += (sender, e) =>
                {
                    if (e.ChangedButton == MouseButton.Middle && e.ClickCount == 2)
                    {
                        level = 0;
                        Point cp = e.GetPosition(value);
                        Point p = e.GetPosition(sv);
                        value.LayoutTransform = null;
                        //						sv.ScrollToHorizontalOffset(cp.X - p.X);
                        //						sv.ScrollToVerticalOffset(cp.Y - p.Y);
                        if (Reset != null) Reset();
                        if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Level"));
                    }
                };

            }
        }


        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
