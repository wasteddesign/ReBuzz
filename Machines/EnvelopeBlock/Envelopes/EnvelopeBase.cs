using BuzzGUI.Common;
using ModernSequenceEditor.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace EnvelopeBlock
{

    /// <summary>
    /// EnvelopeBase is an abstract class that includes all the common stuff for envelopes.
    /// </summary>
    abstract class EnvelopeBase : Canvas, INotifyPropertyChanged
    {
        public static int MAX_NUMBER_OF_ENVELOPE_BOXES = 512;
        public static double ENVELOPE_VIEW_SCALE_ADJUST = 0.8;

        readonly double SNAP_TO_RANGE = 6;

        internal EnvelopeBlockMachine envelopeBlockMachine;
        internal Envelopes envelopesCanvas;

        internal double drawLengthInSeconds;
        internal int envelopePatternIndex;
        internal int envelopeParamIndex;

        internal List<EnvelopeBox> envelopeBoxes;
        internal Polyline envPolyLine;
        internal SequencerLayout layoutMode;
        internal bool boxDragged = false;
        internal int boxDraggedIndex;

        internal Brush strokeBrush = Brushes.Black;
        internal Brush fillBrush = Brushes.LightSkyBlue;
        internal Brush[] brushes = new Brush[]
        {
            Brushes.Orange, Brushes.LightBlue, Brushes.Red, Brushes.LightGreen, Brushes.LightPink, Brushes.Fuchsia
        };
        internal Brush lineBrush = new SolidColorBrush(Color.FromArgb(0x70, 20, 20, 20));
        internal Brush lineBrushSelected = new SolidColorBrush(Color.FromArgb(0xA0, 20, 20, 20));

        internal Point newBoxPositionFromMenu;

#pragma warning disable CS0067 // The event 'EnvelopeBase.PropertyChanged' is never used
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore CS0067 // The event 'EnvelopeBase.PropertyChanged' is never used

        public EnvelopeBase(double width, double height)
        {
            Width = width;
            Height = height;

            envelopeBoxes = new List<EnvelopeBox>();
            this.SnapsToDevicePixels = true;

            foreach (var b in brushes)
                if (b.CanFreeze) b.Freeze();
            if (strokeBrush.CanFreeze) strokeBrush.Freeze();
            if (fillBrush.CanFreeze) fillBrush.Freeze();    
            if (lineBrush.CanFreeze) lineBrush.Freeze();
            if (lineBrushSelected.CanFreeze) lineBrushSelected.Freeze();
        }

        public void Init(EnvelopeBlockMachine ab, Envelopes c, int paramIndex, SequencerLayout layoutMode)
        {
            envelopeBlockMachine = ab;
            envelopesCanvas = c;
            envelopePatternIndex = envelopesCanvas.HostCanvas.EnvelopePatternIndex;
            envelopeParamIndex = paramIndex;

            envPolyLine = new Polyline();

            this.layoutMode = layoutMode;
        }



        /// <summary>
        /// Updates envelope point position. Must be between previous and next point. Adjusts also scale.
        /// </summary>
        /// <param name="evb"></param>
        /// <param name="newPos"></param>
        /// <param name="newPointAdded"></param>
        /// <returns></returns>
        internal Point UpdateEnvelopeBoxCenterPosition(EnvelopeBox evb, Point newPos, bool newPointAdded)
        {
            Point ret = new Point();

            bool isFirstOrLast = false;

            int evbIndex = envelopeBoxes.IndexOf(evb);
            if (evbIndex == 0 || evbIndex == envelopeBoxes.Count - 1)
                isFirstOrLast = true;

            if (layoutMode == SequencerLayout.Vertical)
            {
                double offsetX = envelopesCanvas.Width * (1.0 - ENVELOPE_VIEW_SCALE_ADJUST) / 2.0;
                ret.X = newPos.X;
                if (ret.X < offsetX)
                    ret.X = offsetX;
                else if (ret.X > envelopesCanvas.Width - offsetX)
                    ret.X = envelopesCanvas.Width - offsetX;

                // Update also box Y pos
                if (newPointAdded)
                {
                    ret.Y = newPos.Y;
                    ret = MakeSureNotOverlapping(ret);
                }
                else if (!isFirstOrLast)
                {
                    double prevY = ConvertTimeStampInSecondsToPixels(GetEnvelopeTimeStamp(evbIndex - 1));
                    double nextY = ConvertTimeStampInSecondsToPixels(GetEnvelopeTimeStamp(evbIndex + 1));

                    ret.Y = newPos.Y;

                    if (newPos.Y <= prevY)
                        ret.Y = prevY + 0.5;
                    else if (newPos.Y >= nextY)
                        ret.Y = nextY - 0.5;
                }
                else if (evbIndex == envelopeBoxes.Count - 1)
                {
                    ret.Y = ConvertTimeStampInSecondsToPixels(GetEnvelopeTimeStamp(evb));
                }
            }
            else
            {
                double offsetY = envelopesCanvas.Height * (1.0 - ENVELOPE_VIEW_SCALE_ADJUST) / 2.0;
                ret.Y = newPos.Y;
                if (ret.Y < offsetY)
                    ret.Y = offsetY;
                else if (ret.Y > envelopesCanvas.Height - offsetY)
                    ret.Y = envelopesCanvas.Height - offsetY;

                // Update also box Y pos
                if (newPointAdded)
                {
                    ret.X = newPos.X;
                    ret = MakeSureNotOverlapping(ret);
                }
                else if (!isFirstOrLast)
                {
                    double prevX = ConvertTimeStampInSecondsToPixels(GetEnvelopeTimeStamp(evbIndex - 1));
                    double nextX = ConvertTimeStampInSecondsToPixels(GetEnvelopeTimeStamp(evbIndex + 1));

                    ret.X = newPos.X;

                    if (newPos.X <= prevX)
                        ret.X = prevX + 0.5;
                    else if (newPos.X >= nextX)
                        ret.X = nextX - 0.5;
                }
                else if (evbIndex == envelopeBoxes.Count - 1)
                {
                    ret.X = ConvertTimeStampInSecondsToPixels(GetEnvelopeTimeStamp(evb));
                }
            }

            return ret;
        }

        internal double SnapToTime(double yPos, double draggedBoxPreviousYPos)
        {
            double ret = yPos;


            if (!(Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                if (Math.Abs(yPos - draggedBoxPreviousYPos) < SNAP_TO_RANGE)
                {
                    ret = draggedBoxPreviousYPos;
                }
            }

            if (envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].SnapToTick)
            {
                double SecondsInTick = 1.0 / (envelopeBlockMachine.host.MasterInfo.TicksPerBeat * envelopeBlockMachine.host.MasterInfo.BeatsPerMin / 60.0);

                double pixelsPerSecond = GetPixelsPerSecond();
                double PixelsInTick = pixelsPerSecond * SecondsInTick;
                ret = PixelsInTick * Math.Floor((ret / PixelsInTick) + 0.5);

                int time = envelopesCanvas.HostCanvas.Time;

                int tick = (Global.Buzz.TPB - (time % Global.Buzz.TPB));
                if (tick == Global.Buzz.TPB)
                    tick = 0;

                ret += PixelsInTick * tick;

            }
            else if (envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].SnapToBeat)
            {
                double SecondsInTick = 1.0 / (envelopeBlockMachine.host.MasterInfo.TicksPerBeat * envelopeBlockMachine.host.MasterInfo.BeatsPerMin / 60.0);

                double pixelsPerSecond = GetPixelsPerSecond();
                double PixelsInTick = pixelsPerSecond * SecondsInTick;

                double SecondsInBeat = 1.0 / (envelopeBlockMachine.host.MasterInfo.BeatsPerMin / 60.0);

                double PixelsInBeat = pixelsPerSecond * SecondsInBeat;
                ret = PixelsInBeat * Math.Floor((ret / PixelsInBeat) + 0.5);

                int time = envelopesCanvas.HostCanvas.Time;
                int tick = (Global.Buzz.TPB - (time % Global.Buzz.TPB));
                if (tick == Global.Buzz.TPB)
                    tick = 0;

                ret += PixelsInTick * tick;
            }

            return ret;
        }

        internal double SnapToBox(int envelopeBoxIndex, double xPos, double draggedBoxPreviousXPos)
        {
            double ret = xPos;

            double prevBoxXPos = 0;
            double nextBoxXPos = 0;

            if (envelopeBoxIndex > 0)
            {
                EnvelopeBox eb = envelopeBoxes[envelopeBoxIndex - 1];
                prevBoxXPos = Canvas.GetLeft(eb) + eb.Width / 2.0;
            }
            if (envelopeBoxIndex < envelopeBoxes.Count - 1)
            {
                EnvelopeBox eb = envelopeBoxes[envelopeBoxIndex + 1];
                nextBoxXPos = Canvas.GetLeft(eb) + eb.Width / 2.0;
            }

            if (!(Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                if (Math.Abs(xPos - draggedBoxPreviousXPos) < SNAP_TO_RANGE)
                {
                    ret = draggedBoxPreviousXPos;
                }
                else if (Math.Abs(xPos - prevBoxXPos) < SNAP_TO_RANGE)
                {
                    ret = prevBoxXPos;
                }
                else if (Math.Abs(xPos - nextBoxXPos) < SNAP_TO_RANGE)
                {
                    ret = nextBoxXPos;
                }
            }

            return ret;
        }

        internal abstract Point MakeSureNotOverlapping(Point pos);
        internal abstract double GetEnvelopeValue(EnvelopeBox envelopeBox);
        internal abstract double GetEnvelopeValue(int envelopeBoxIndex);
        internal abstract double GetEnvelopeTimeStamp(EnvelopeBox envelopeBox);
        internal abstract double GetEnvelopeTimeStamp(int envelopeBoxIndex);
        internal abstract void SetEnvelopeValue(EnvelopeBox envelopeBox, double value);
        internal abstract void SetEnvelopeValue(int envelopeBoxIndex, double value);
        internal abstract void SetEnvelopeTimeStamp(EnvelopeBox envelopeBox, double value);
        internal abstract void SetEnvelopeTimeStamp(int envelopeBoxIndex, double value);
        public abstract void UpdatePolyLinePath();
        public abstract void Draw();


        internal abstract double MaxBoxValue();

        public double DrawLengthInSeconds { get => drawLengthInSeconds; set => drawLengthInSeconds = value; }

        public void SetLenghtInSeconds(double len)
        {
            DrawLengthInSeconds = len;
        }

        /// <summary>
        /// Converts canvas point to time stamp.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        internal double ConvertPixelToTimeStampInSeconds(Point pos)
        {
            //double drawLenghtInSeconds = envelopesCanvas.HostCanvas.PatternLengthInSeconds; // Includes offset

            double ret = 0;

            double secondsPerPixel = 1.0 / GetPixelsPerSecond();

            if (layoutMode == SequencerLayout.Vertical)
            {
                ret = secondsPerPixel * pos.Y;
            }
            else
            {
                ret = secondsPerPixel * pos.X;
            }

            return ret;
        }

        /// <summary>
        /// Converts time stamp in seconds to screen point.
        /// </summary>
        /// <param name="timeStamp"></param>
        /// <returns></returns>
        internal double ConvertTimeStampInSecondsToPixels(double timeStamp)
        {
            double ret;

            double pixelsPerSecond = GetPixelsPerSecond();

            ret = pixelsPerSecond * (timeStamp);

            return ret;
        }

        internal double GetPixelsPerSecond()
        {
            double pixelsPerSecond;
            double drawLenghtInSeconds = envelopesCanvas.DrawLengthInSeconds; // Includes offset

            if (layoutMode == SequencerLayout.Vertical)
            {
                if (envelopesCanvas.HostCanvas.TickHeight > 0)
                    pixelsPerSecond = envelopesCanvas.HostCanvas.TickHeight * envelopeBlockMachine.host.MasterInfo.TicksPerSec;
                else
                    pixelsPerSecond = envelopesCanvas.HostCanvas.Height / drawLenghtInSeconds;
            }
            else
            {
                if (envelopesCanvas.HostCanvas.TickHeight > 0)
                    pixelsPerSecond = envelopesCanvas.HostCanvas.TickHeight * envelopeBlockMachine.host.MasterInfo.TicksPerSec;
                else
                    pixelsPerSecond = envelopesCanvas.HostCanvas.Width / drawLenghtInSeconds;
            }

            return pixelsPerSecond;
        }

        /// <summary>
        /// Envelope value to canvas point.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public double ValueToScreen(double value)
        {
            double ret;

            if (layoutMode == SequencerLayout.Vertical)
            {
                ret = value / MaxBoxValue() * envelopesCanvas.HostCanvas.Width * ENVELOPE_VIEW_SCALE_ADJUST + envelopesCanvas.HostCanvas.Width * (1.0 - ENVELOPE_VIEW_SCALE_ADJUST) / 2.0;
            }
            else
            {
                ret = (envelopesCanvas.HostCanvas.Height - value / MaxBoxValue() * envelopesCanvas.HostCanvas.Height) * ENVELOPE_VIEW_SCALE_ADJUST + envelopesCanvas.HostCanvas.Height * (1.0 - ENVELOPE_VIEW_SCALE_ADJUST) / 2.0;
            }
            return ret;
        }

        /// <summary>
        /// Canvas point to envelope value.
        /// </summary>
        /// <param name="posInScreen"></param>
        /// <returns></returns>
        public double ScreenToValue(double posInScreen)
        {
            double ret;

            if (layoutMode == SequencerLayout.Vertical)
            {
                posInScreen -= envelopesCanvas.HostCanvas.Width * (1.0 - ENVELOPE_VIEW_SCALE_ADJUST) / 2.0;
                posInScreen /= ENVELOPE_VIEW_SCALE_ADJUST;
                posInScreen /= envelopesCanvas.HostCanvas.Width;
                posInScreen *= MaxBoxValue();
            }
            else
            {
                posInScreen -= envelopesCanvas.HostCanvas.Height * (1.0 - ENVELOPE_VIEW_SCALE_ADJUST) / 2.0;
                posInScreen /= ENVELOPE_VIEW_SCALE_ADJUST;
                posInScreen -= envelopesCanvas.HostCanvas.Height;
                posInScreen /= -envelopesCanvas.HostCanvas.Height;
                posInScreen *= MaxBoxValue();
            }

            ret = posInScreen;

            return ret;
        }

        /// <summary>
        /// Updates envelope value based on screen point.
        /// </summary>
        /// <param name="evb"></param>
        /// <param name="point"></param>
        internal void UpdateEnvPoint(EnvelopeBox evb, Point point)
        {
            if (layoutMode == SequencerLayout.Vertical)
            {
                SetEnvelopeValue(evb, ScreenToValue(point.X));
            }
            else
            {
                SetEnvelopeValue(evb, ScreenToValue(point.Y));
            }

            SetEnvelopeTimeStamp(evb, ConvertPixelToTimeStampInSeconds(point));
        }

        /// <summary>
        /// Add new envelope box.
        /// </summary>
        /// <param name="evb"></param>
        /// <param name="timeStamp"></param>
        /// <returns></returns>
        internal int AddEnvelopeBoxToList(EnvelopeBox evb, double timeStamp)
        {
            EnvelopeBox prevBox = envelopeBoxes[0];

            int index;
            for (index = 1; index < envelopeBoxes.Count; index++)
            {
                EnvelopeBox box = envelopeBoxes[index];
                if (GetEnvelopeTimeStamp(prevBox) <= timeStamp && timeStamp <= GetEnvelopeTimeStamp(box))
                {
                    envelopeBoxes.Insert(index, evb);
                    break;
                }
                prevBox = box;
            }

            return index;
        }

        internal int GetIndexForNewEnvelope(EnvelopeBox evb, double timeStamp)
        {
            EnvelopeBox prevBox = envelopeBoxes[0];

            int index;
            for (index = 1; index < envelopeBoxes.Count; index++)
            {
                EnvelopeBox box = envelopeBoxes[index];
                if (GetEnvelopeTimeStamp(prevBox) <= timeStamp && timeStamp <= GetEnvelopeTimeStamp(box))
                {
                    break;
                }
                prevBox = box;
            }

            return index;
        }

        internal abstract double DefaulBoxValue();

        /// <summary>
        /// Reset Envelope box to default value.
        /// </summary>
        /// <param name="envelopeBox"></param>
        public void ResetEnvelopeBox(EnvelopeBox envelopeBox)
        {
            SetEnvelopeValue(envelopeBox, DefaulBoxValue());
            int envelopeBoxIndex = envelopeBoxes.IndexOf(envelopeBox);

            Canvas.SetLeft(envelopeBox, ValueToScreen(GetEnvelopeValue(envelopeBox)) - envelopeBox.Width / 2.0);
            Point point = envPolyLine.Points[envelopeBoxIndex];
            point.X = ValueToScreen(GetEnvelopeValue(envelopeBox));
            envPolyLine.Points[envelopeBoxIndex] = point;
        }
    }
}
