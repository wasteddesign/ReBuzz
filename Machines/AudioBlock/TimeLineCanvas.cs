using BuzzGUI.Common;
using System;
using System.Windows.Controls;
using System.Windows.Media;

namespace WDE.AudioBlock
{
    class TimeLineCanvas : Canvas
    {
        private double waveCanvasLengthInPixels = 1000;

        private ViewOrientationMode viewMode = ViewOrientationMode.Vertical;
        double timeItemWidth = 70;
        double timeItemHeight = 40;

        double offset = 0;
        private double timeScaleSeconds;

        public TimeLineCanvas()
        {
            this.ClipToBounds = true;
        }

        public double TimeScale { get => timeScaleSeconds; set => timeScaleSeconds = value; }
        internal ViewOrientationMode ViewMode { get => viewMode; set => viewMode = value; }
        public double Offset { get => offset; set => offset = value; }
        public double SlidingWindowOffsetSeconds { get; internal set; }
        public double WaveCanvasLengthInPixels { get => waveCanvasLengthInPixels; set => waveCanvasLengthInPixels = value; }

        public void UpdateTimeLine()
        {
            this.Children.Clear();

            if (ViewMode == ViewOrientationMode.Vertical)
            {
                double step = timeItemHeight + TimeScale % timeItemHeight;
                double time = SlidingWindowOffsetSeconds;
                double timeAddSeconds = (timeScaleSeconds / WaveCanvasLengthInPixels) * step;

                for (double y = 1 - Offset; y < this.Height; y += step)
                {
                    Utils.DrawLine(this, 0, y, this.Width, y, new SolidColorBrush(Utils.ChangeColorBrightness(Global.Buzz.ThemeColors["SE Pattern Box"], -0.2f)), 1);
                    Color color = Color.FromArgb(0xD0, 0, 0, 0);
                    Utils.DrawText(this, 0, y - 4, GetTimeStamp(time), new SolidColorBrush(color));
                    time += timeAddSeconds;
                }
            }
            else
            {
                double step = timeItemWidth + TimeScale % timeItemWidth;
                double time = SlidingWindowOffsetSeconds;
                double timeAddSeconds = (timeScaleSeconds / WaveCanvasLengthInPixels) * step;

                for (double x = 1 - Offset; x < this.Width; x += step)
                {
                    Utils.DrawLine(this, x, 0, x, this.Height, new SolidColorBrush(Utils.ChangeColorBrightness(Global.Buzz.ThemeColors["SE Pattern Box"], -0.2f)), 1);
                    Color color = Color.FromArgb(0xD0, 0, 0, 0);
                    Utils.DrawText(this, x, 0, GetTimeStamp(time), new SolidColorBrush(color));
                    time += timeAddSeconds;
                }
            }
        }

        public string GetTimeStamp(double timeSeconds)
        {
            timeSeconds = Math.Round(timeSeconds, 3);
            var reminder = timeSeconds - Math.Truncate(timeSeconds);
            reminder *= 1000;

            return String.Format("{0:000}", (int)timeSeconds) + ":" + String.Format("{0:000}", (int)reminder);
        }
    }
}
