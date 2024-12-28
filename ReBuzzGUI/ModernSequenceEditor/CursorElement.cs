using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;


namespace WDE.ModernSequenceEditor
{
    public class CursorElement : FrameworkElement
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetCaretBlinkTime();

        int GetCaretBlinkTimeEx()
        {
            return SequenceEditor.Settings.CursorBlinking ? GetCaretBlinkTime() : 0;
        }

        int column = 0;
        public int Column
        {
            get { return column; }
            set
            {
                column = value;
                Update();
                SetBlinkAnimation(true, true);
            }
        }

        int time;
        public int Time
        {
            get { return time; }
            set
            {
                time = value;
                Update();
                SetBlinkAnimation(true, true);
            }
        }

        bool isActive;
        public bool IsActive
        {
            get { return isActive; }
            set
            {
                isActive = value;
                InvalidateVisual();
                SetBlinkAnimation(isActive, false);
            }

        }

        public CursorElement()
        {
            this.IsVisibleChanged += (sender, e) =>
            {
                SetBlinkAnimation(IsVisible & IsActive, false);
            };
        }

        public void Update()
        {
            Width = SequenceEditor.ViewSettings.TrackWidth + 1;
            Height = BuzzGUI.SequenceEditor.SequenceEditor.ViewSettings.TimeSignatureList.GetBarLengthAt(Time) * SequenceEditor.ViewSettings.TickHeight + 1;
            Canvas.SetTop(this, Time * SequenceEditor.ViewSettings.TickHeight);
            Canvas.SetLeft(this, Column * SequenceEditor.ViewSettings.TrackWidth);

        }

        public void Move(int dx, int dy, int maxx)
        {
            if (dy != 0)
            {
                long t = Time + (long)dy * BuzzGUI.SequenceEditor.SequenceEditor.ViewSettings.TimeSignatureList.GetBarLengthAt(Time);
                Time = BuzzGUI.SequenceEditor.SequenceEditor.ViewSettings.TimeSignatureList.Snap(t, maxx);
            }

            if (dx != 0)
            {
                Column = (int)Math.Max(0, Math.Min(SequenceEditor.ViewSettings.TrackCount - 1, Column + (long)dx));
            }

            BringIntoView();
        }

        Brush br;
        Brush ibr;

        protected override void OnRender(DrawingContext dc)
        {
            if (br == null)
            {
                br = TryFindResource("CursorBrush") as Brush;
                ibr = TryFindResource("CursorInactiveBrush") as Brush;
                if (ibr == null) ibr = br;
            }

            dc.DrawRectangle(isActive ? br : ibr, null, new Rect(0, 0, ActualWidth, ActualHeight));

        }


        //AnimationClock _blinkAnimationClock;
        DispatcherTimer blinkTimer;
        TimeSpan blinkDuration;
        bool blinkState;

        public void SetBlinkAnimation(bool visible, bool positionChanged)
        {
            int time = GetCaretBlinkTimeEx();

            if (time > 0)
            {
                var duration = TimeSpan.FromMilliseconds((double)(time));
                if (blinkTimer == null || blinkDuration != duration)
                {
                    blinkTimer = new DispatcherTimer();
                    blinkTimer.Interval = blinkDuration = duration;
                    blinkState = true;
                    blinkTimer.Tick += (sender, e) =>
                    {
                        blinkState ^= true;
                        this.Opacity = blinkState ? 1.0 : 0.0;
                    };
                }

            }
            else if (blinkTimer != null)
            {
                blinkTimer.Stop();
                blinkTimer = null;
                this.Opacity = 1.0;
            }

            if (blinkTimer != null)
            {
                if (visible && (!blinkState || positionChanged))
                {
                    blinkTimer.Stop();
                    blinkState = true;
                    this.Opacity = 1.0;
                    blinkTimer.Start();
                }
                else if (!visible)
                {
                    blinkTimer.Stop();
                    blinkState = false;
                    this.Opacity = 1.0;

                }
            }

        }

    }

}
