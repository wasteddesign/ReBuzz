using BuzzGUI.Common;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BuzzGUI.WaveformControl
{
    public partial class WaveformControl : UserControl
    {
        public ICommand ZoomInCommand { get; set; }

        public WaveformControl()
        {
            InitializeComponent();

            int mouseWheelAcc = 0;

            this.PreviewMouseWheel += (sender, e) =>
            {
                if (waveformElement.Waveform != null)
                {
                    mouseWheelAcc += e.Delta;

                    while (mouseWheelAcc > 120)
                    {
                        mouseWheelAcc -= 120;
                        waveformElement.AdjustZoom(false);
                    }

                    while (mouseWheelAcc < 120)
                    {
                        mouseWheelAcc += 120;
                        waveformElement.AdjustZoom(true);
                    }

                }

            };

            DataContextChanged += new DependencyPropertyChangedEventHandler(OnDataContextChanged);

            NameScope.SetNameScope(contextMenu, NameScope.GetNameScope(this));

            ZoomInCommand = new SimpleCommand()
            {
                CanExecuteDelegate = (x) => true,
                ExecuteDelegate = (x) =>
                    {
                        waveformElement.AdjustZoom(true);
                    }
            };

            waveformElement.SelectionChanged += new WaveformElement.ChangedEventHandler((t, e) => { });
            waveformElement.PropertyChanged += waveformElement_PropertyChanged;
        }

        void waveformElement_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "CursorOffset")
            {
                double ScreenOffset = (waveformElement.PlayCursor.Offset - waveformElement.ScrollOffset.X) % waveformElement.ActualWidth;

                if (ScreenOffset < 0)
                {
                    Canvas.SetLeft(TimelineCursor, 0.0);
                }
                else
                {
                    if (waveformElement.PlayCursor.Offset - waveformElement.ActualWidth > waveformElement.ScrollOffset.X)
                    {
                        Canvas.SetLeft(TimelineCursor, waveformElement.ActualWidth);
                    }
                    else
                    {
                        Canvas.SetLeft(TimelineCursor, Math.Floor(ScreenOffset));
                    }
                }

                //BuzzGUI.Common.Global.Buzz.DCWriteLine("screen:" + ScreenOffset.ToString());
                //BuzzGUI.Common.Global.Buzz.DCWriteLine("cursor:" + waveformElement.PlayCursor.Offset.ToString());
                //BuzzGUI.Common.Global.Buzz.DCWriteLine("scroll:" + waveformElement.ScrollOffset.X.ToString());
            }
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point p = Mouse.GetPosition(Timeline);
            waveformElement.PlayCursor.OffsetSamples = waveformElement.PositionToSample(p.X);
        }

        void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var x = (WaveformControl)sender;
            var oldVM = e.OldValue as WaveformVM;
            var newVM = e.NewValue as WaveformVM;

            BuzzGUI.Common.Global.Buzz.DCWriteLine("OnDataContextChanged");
            // You can also validate the data going into the DataContext using the event args
        }
    }
}
