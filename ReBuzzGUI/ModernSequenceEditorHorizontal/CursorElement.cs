using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;


namespace WDE.ModernSequenceEditorHorizontal
{
    public class CursorElement : FrameworkElement
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetCaretBlinkTime();

        int GetCaretBlinkTimeEx()
        {
            return SequenceEditor.Settings.CursorBlinking ? GetCaretBlinkTime() : 0;
        }

        int row = 0;
        public int Row
        {
            get { return row; }
            set
            {
                row = value;
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
            Height = SequenceEditor.ViewSettings.TrackHeight + 1;
            Width = BuzzGUI.SequenceEditor.SequenceEditor.ViewSettings.TimeSignatureList.GetBarLengthAt(Time) * SequenceEditor.ViewSettings.TickWidth + 1;
            Canvas.SetLeft(this, Time * SequenceEditor.ViewSettings.TickWidth);
            Canvas.SetTop(this, Row * SequenceEditor.ViewSettings.TrackHeight);

        }

        public void Move(int dx, int dy, int maxx)
        {
            if (dx != 0)
            {
                long t = Time + (long)dx * BuzzGUI.SequenceEditor.SequenceEditor.ViewSettings.TimeSignatureList.GetBarLengthAt(Time);
                Time = BuzzGUI.SequenceEditor.SequenceEditor.ViewSettings.TimeSignatureList.Snap(t, maxx);
            }

            if (dy != 0)
            {
                Row = (int)Math.Max(0, Math.Min(SequenceEditor.ViewSettings.TrackCount - 1, Row + (long)dy));
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
