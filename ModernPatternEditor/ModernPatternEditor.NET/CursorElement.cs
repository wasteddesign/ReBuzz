using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;


namespace WDE.ModernPatternEditor
{
    public class CursorElement : FrameworkElement
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetCaretBlinkTime();

        int GetCaretBlinkTimeEx()
        {
            return PatternEditor.Settings.CursorBlinking ? GetCaretBlinkTime() : 0;
        }

        Rect rect;
        public Rect Rect
        {
            get { return rect; }
            set
            {
                rect = value;
                Update();
                if (IsActive)
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
            Height = rect.Height;
            Width = rect.Width;
            Canvas.SetLeft(this, rect.Left);
            Canvas.SetTop(this, rect.Top);

        }

        Brush br;
        Brush bbr;
        Pen pen;
        Brush ibr;

        protected override void OnRender(DrawingContext dc)
        {
            if (ActualWidth == 0 || ActualHeight == 0) return;

            if (br == null)
            {
                br = TryFindResource("CursorBrush") as Brush;
                bbr = TryFindResource("CursorBorderBrush") as Brush;
                if (bbr != null) pen = new Pen(bbr, 1.0);
                ibr = TryFindResource("CursorInactiveBrush") as Brush;
                if (ibr == null) ibr = br;
            }

            //dc.DrawRectangle(isActive ? br : ibr, isActive ? pen : null, new Rect(0, 0, ActualWidth, ActualHeight));
            dc.DrawRectangle(isActive ? br : ibr, null, new Rect(0, 0, ActualWidth, ActualHeight));
            if (bbr != null)
            {
                dc.DrawRectangle(bbr, null, new Rect(0, 0, ActualWidth, 1));
                dc.DrawRectangle(bbr, null, new Rect(0, ActualHeight - 1, ActualWidth, 1));
                dc.DrawRectangle(bbr, null, new Rect(0, 1, 1, ActualHeight - 2));
                dc.DrawRectangle(bbr, null, new Rect(ActualWidth - 1, 1, 1, ActualHeight - 2));
            }

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
