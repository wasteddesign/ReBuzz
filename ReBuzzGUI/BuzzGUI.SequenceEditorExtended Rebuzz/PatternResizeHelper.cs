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

namespace BuzzGUI.SequenceEditor
{
    public enum PatternResizeMode
    {
        None,
        Left,
        Right
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
        public int Row { get; private set; }
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

        public void StartResize(TrackControl tc, SequenceEvent se, ISequence selectedSequence, int row, PatternResizeMode mode, Rectangle rect)
        {
            ResizeRect = rect;
            TrackControl = tc;
            SequenceEvent = se;
            //Song = song;
            Row = row;
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
                double left = (OriginalSnapTime) * SequenceEditor.ViewSettings.TickWidth;
                Canvas.SetLeft(ResizeRect, left);
                Canvas.SetTop(ResizeRect, Row * SequenceEditor.ViewSettings.TrackHeight);
                ResizeRect.Width = SequenceEvent.Span * SequenceEditor.ViewSettings.TickWidth;
                ResizeRect.Height = tc.ActualHeight;
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
            if (PatternResizeMode == PatternResizeMode.Right)
            {
                double left = (OriginalSnapTime) * SequenceEditor.ViewSettings.TickWidth;
                Canvas.SetLeft(ResizeRect, left);
                Canvas.SetTop(ResizeRect, Row * SequenceEditor.ViewSettings.TrackHeight);

                int tickInSeq = (int)(p.X / SequenceEditor.ViewSettings.TickWidth);
                tickInSeq = (int)(SnapToTick * Math.Round(tickInSeq / (double)SnapToTick + 0.5));

                double w = (tickInSeq - OriginalSnapTime) * SequenceEditor.ViewSettings.TickWidth;
                if (w <= 0)
                    w = SnapToTick * SequenceEditor.ViewSettings.TickWidth;
                ResizeRect.Width = w;
                ResizeRect.Height = TrackControl.ActualHeight;
            }
            else if (PatternResizeMode == PatternResizeMode.Left)
            {
                int tickInSeq = (int)(p.X / SequenceEditor.ViewSettings.TickWidth);
                tickInSeq = (int)(SnapToTick * Math.Floor(tickInSeq / (double)SnapToTick + 0.5));

                if (tickInSeq >= SequenceEvent.Span + OriginalSnapTime - SnapToTick)
                    tickInSeq = SequenceEvent.Span + OriginalSnapTime - SnapToTick;

                double left = (tickInSeq) * SequenceEditor.ViewSettings.TickWidth;
                Canvas.SetLeft(ResizeRect, left);
                Canvas.SetTop(ResizeRect, Row * SequenceEditor.ViewSettings.TrackHeight);

                double w = (SequenceEvent.Span + OriginalSnapTime - tickInSeq) * SequenceEditor.ViewSettings.TickWidth;
                ResizeRect.Width = w;
                ResizeRect.Height = TrackControl.ActualHeight;
            }
        }

        internal void StartDelay()
        {
            DelayCompleted = false;
            dtDelayTimer.Start();
        }
    }
}
