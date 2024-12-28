using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using BuzzGUI.SequenceEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WDE.ModernSequenceEditor
{
    public enum PatternResizeMode
    {
        None,
        Top,
        Bottom
    }
    public class PatternResizeHelper
    {
        private Rectangle ResizeRect;
        private DispatcherTimer dtDelayTimer;

        public TrackControl TrackControl { get; private set; }
        public SequenceEvent SequenceEvent { get; private set; }
        public ISong Song { get; private set; }
        public bool Resizing { get; private set; }
        //public ISequence Sequence { get; private set; }
        public int Column { get; private set; }
        public PatternResizeMode PatternResizeMode { get; private set; }
        public int OriginalSnapTime { get; private set; }

        public int SnapToTick { get; set; }
        public bool DelayCompleted { get; private set; }

        public PatternResizeHelper()
        {
            DelayCompleted = true;
            dtDelayTimer = new DispatcherTimer();
            dtDelayTimer.Interval = TimeSpan.FromMilliseconds(100);
            dtDelayTimer.Tick += (sender, e) =>
            {
                DelayCompleted = true;
                dtDelayTimer.Stop();
            };
        }

        public void StartResize(TrackControl tc, SequenceEvent se, ISequence selectedSequence, int column, PatternResizeMode mode, Rectangle rect)
        {
            ResizeRect = rect;
            TrackControl = tc;
            SequenceEvent = se;
            //Song = song;
            Column = column;
            Resizing = true;
            //Sequence = selectedSequence;
            PatternResizeMode = mode;

            SnapToTick = SequenceEditor.Settings.ResizeSnap;


            OriginalSnapTime = -1;
            foreach (var seqE in tc.Sequence.Events)
            {
                if (seqE.Value == SequenceEvent)
                {
                    OriginalSnapTime = seqE.Key;
                    break;
                }
            }

            if (OriginalSnapTime >= 0)
            {
                double top = (OriginalSnapTime) * SequenceEditor.ViewSettings.TickHeight;
                Canvas.SetTop(ResizeRect, top);
                Canvas.SetLeft(ResizeRect, Column * SequenceEditor.ViewSettings.TrackWidth);
                ResizeRect.Height = SequenceEvent.Span * SequenceEditor.ViewSettings.TickHeight;
                ResizeRect.Width = tc.ActualWidth;
                ResizeRect.Visibility = Visibility.Visible;
            }
        }

        public void Stop()
        {
            Resizing = false;
            ResizeRect.Visibility = Visibility.Collapsed;
        }

        public void Update(Rectangle rect, Point p)
        {
            if (PatternResizeMode == PatternResizeMode.Bottom)
            {
                double top = (OriginalSnapTime) * SequenceEditor.ViewSettings.TickHeight;
                Canvas.SetTop(ResizeRect, top);
                Canvas.SetLeft(ResizeRect, Column * SequenceEditor.ViewSettings.TrackWidth);

                int tickInSeq = (int)(p.Y / SequenceEditor.ViewSettings.TickHeight);
                tickInSeq = (int)(SnapToTick * Math.Round(tickInSeq / (double)SnapToTick + 0.5));

                double h = (tickInSeq - OriginalSnapTime) * SequenceEditor.ViewSettings.TickHeight;
                if (h <= 0)
                    h = SnapToTick * SequenceEditor.ViewSettings.TickHeight;
                ResizeRect.Height = h;
                ResizeRect.Width = TrackControl.ActualWidth;
            }
            else if (PatternResizeMode == PatternResizeMode.Top)
            {
                int tickInSeq = (int)(p.Y / SequenceEditor.ViewSettings.TickHeight);
                tickInSeq = (int)(SnapToTick * Math.Floor(tickInSeq / (double)SnapToTick + 0.5));

                if (tickInSeq >= SequenceEvent.Span + OriginalSnapTime - SnapToTick)
                    tickInSeq = SequenceEvent.Span + OriginalSnapTime - SnapToTick;

                double top = (tickInSeq) * SequenceEditor.ViewSettings.TickHeight;
                Canvas.SetTop(ResizeRect, top);
                Canvas.SetLeft(ResizeRect, Column * SequenceEditor.ViewSettings.TrackWidth);

                double h = (SequenceEvent.Span + OriginalSnapTime - tickInSeq) * SequenceEditor.ViewSettings.TickHeight;
                ResizeRect.Height = h;
                ResizeRect.Width = TrackControl.ActualWidth;
            }
        }

        internal void StartDelay()
        {
            DelayCompleted = false;
            dtDelayTimer.Start();
        }
    }
}
