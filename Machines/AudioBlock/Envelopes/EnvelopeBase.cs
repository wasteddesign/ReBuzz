using BuzzGUI.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WDE.AudioBlock
{

    /// <summary>
    /// EnvelopeBase is an abstract class that includes all the common stuff for envelopes.
    /// </summary>
    abstract class EnvelopeBase : Canvas, INotifyPropertyChanged
    {
        public static int MAX_NUMBER_OF_ENVELOPE_BOXES = 512;
        public static double ENVELOPE_VIEW_SCALE_ADJUST = 0.8;

        internal AudioBlock audioBlock;
        internal Envelopes envelopesCanvas;

        internal double drawLengthInSeconds;
        internal ViewOrientationMode viewMode = ViewOrientationMode.Vertical;
        internal int audioBlockIndex;

        internal List<EnvelopeBox> envelopeBoxes;
        internal Polyline envPolyLine;
        internal bool boxDragged = false;
        internal int boxDraggedIndex;

        internal Brush strokeBrush = Brushes.Black;
        internal Brush fillBrush = Brushes.LightSkyBlue;
        internal Brush lineBrush = new SolidColorBrush(Color.FromArgb(0x80, 0, 0, 0));
        internal Brush lineBrushSelected = new SolidColorBrush(Color.FromArgb(0xA0, 20, 20, 20));

        internal Point newBoxPositionFromMenu;

        public event PropertyChangedEventHandler PropertyChanged;

        public EnvelopeBase()
        {
            envelopeBoxes = new List<EnvelopeBox>();
            this.SnapsToDevicePixels = true;
        }

        public void Init(AudioBlock ab, Envelopes c)
        {
            audioBlock = ab;
            envelopesCanvas = c;

            envPolyLine = new Polyline();
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

            if (viewMode == ViewOrientationMode.Vertical)
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



                // Update also box X pos
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

        internal double SnapTo(double val)
        {
            double ret = val;

            if (audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].SnapToTick)
            {
                double SecondsInTick = 1.0 / (audioBlock.host.MasterInfo.TicksPerBeat * audioBlock.host.MasterInfo.BeatsPerMin / 60.0);

                double pixelsPerSecond = GetPixelsPerSecond();
                double PixelsInTick = pixelsPerSecond * SecondsInTick;
                ret = PixelsInTick * Math.Floor((ret / PixelsInTick) + 0.5);
                
                int time = envelopesCanvas.HostCanvas.Time;

                int tick = (Global.Buzz.TPB - (time % Global.Buzz.TPB));
                if (tick == Global.Buzz.TPB)
                    tick = 0;

                ret += PixelsInTick * tick;
            }
            else if (audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].SnapToBeat)
            {
                double SecondsInTick = 1.0 / (audioBlock.host.MasterInfo.TicksPerBeat * audioBlock.host.MasterInfo.BeatsPerMin / 60.0);

                double pixelsPerSecond = GetPixelsPerSecond();
                double PixelsInTick = pixelsPerSecond * SecondsInTick;

                double SecondsInBeat = 1.0 / (audioBlock.host.MasterInfo.BeatsPerMin / 60.0);

                double PixelsInBeat = pixelsPerSecond * SecondsInBeat;
                ret = PixelsInBeat * Math.Floor((ret / PixelsInBeat) + 0.5);

                int time = envelopesCanvas.HostCanvas.Time;
                int tick = (Global.Buzz.TPB - (time % Global.Buzz.TPB));
                if (tick == Global.Buzz.TPB)
                    tick = 0;

                ret += PixelsInTick * tick;
            }

            ret += GetSlidingWindowOffset();

            return ret;
        }

        internal double GetSlidingWindowOffset()
        {
            double ret = 0;
            if (!envelopesCanvas.HostCanvas.PatternEditorWave)
            {
                double SlidingWindowOffsetSeconds = envelopesCanvas.HostCanvas.SlidingWindowOffsetSeconds;

                if (viewMode == ViewOrientationMode.Vertical)
                {
                    double numberOfTicks = envelopesCanvas.HostCanvas.patternLengthInSeconds * audioBlock.host.MasterInfo.TicksPerSec;
                    double pixelsPerTick = envelopesCanvas.HostCanvas.Height / numberOfTicks;

                    double nextTick = (double)audioBlock.host.MasterInfo.TicksPerBeat - (SlidingWindowOffsetSeconds * audioBlock.host.MasterInfo.TicksPerSec) % (double)audioBlock.host.MasterInfo.TicksPerBeat;
                    ret = (nextTick - Math.Floor(nextTick)) * pixelsPerTick;
                }
                else
                {
                    double numberOfTicks = envelopesCanvas.HostCanvas.patternLengthInSeconds * audioBlock.host.MasterInfo.TicksPerSec;
                    double pixelsPerTick = envelopesCanvas.HostCanvas.Width / numberOfTicks;

                    double nextTick = (double)audioBlock.host.MasterInfo.TicksPerBeat - (SlidingWindowOffsetSeconds * audioBlock.host.MasterInfo.TicksPerSec) % (double)audioBlock.host.MasterInfo.TicksPerBeat;
                    ret = (nextTick - Math.Floor(nextTick)) * pixelsPerTick;
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
            double drawLenghtInSeconds = envelopesCanvas.HostCanvas.PatternLengthInSeconds; // Includes offset
            double slidingOffsetWindowInSeconds = envelopesCanvas.HostCanvas.SlidingWindowOffsetSeconds;

            double ret;

            if (viewMode == ViewOrientationMode.Vertical)
            {
                double secondsPerPixel = 1.0 / GetPixelsPerSecond();
                ret = secondsPerPixel * pos.Y + slidingOffsetWindowInSeconds;
            }
            else
            {
                double secondsPerPixel = 1.0 / GetPixelsPerSecond();
                ret = secondsPerPixel * pos.X + slidingOffsetWindowInSeconds;
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
            double slidingOffsetWindowInSeconds = envelopesCanvas.HostCanvas.SlidingWindowOffsetSeconds;

            double ret;

            if (viewMode == ViewOrientationMode.Vertical)
            {
                double pixelsPerSecond = GetPixelsPerSecond();
                ret = pixelsPerSecond * (timeStamp - slidingOffsetWindowInSeconds);
            }
            else
            {
                double pixelsPerSecond = GetPixelsPerSecond();
                ret = pixelsPerSecond * (timeStamp - slidingOffsetWindowInSeconds);
            }

            return ret;
        }

        internal double GetPixelsPerSecond()
        {
            double pixelsPerSecond;
            double drawLenghtInSeconds = envelopesCanvas.DrawLengthInSeconds; // Includes offset
            if (envelopesCanvas.HostCanvas.TickHeight > 0)
            {
                pixelsPerSecond = envelopesCanvas.HostCanvas.TickHeight * audioBlock.host.MasterInfo.TicksPerSec;
            }
            else
            {
                if (viewMode == ViewOrientationMode.Vertical)
                {
                    pixelsPerSecond = envelopesCanvas.HostCanvas.Height / drawLenghtInSeconds;
                }
                else
                {
                    pixelsPerSecond = envelopesCanvas.HostCanvas.Width / drawLenghtInSeconds;
                }
            }

            return pixelsPerSecond;
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

        /// <summary>
        /// Envelope value to canvas point.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public double ValueToScreen(double value)
        {
            double ret;

            if (viewMode == ViewOrientationMode.Vertical)
            {
                ret = (value / MaxBoxValue()) * envelopesCanvas.HostCanvas.Width * ENVELOPE_VIEW_SCALE_ADJUST + envelopesCanvas.HostCanvas.Width * (1.0 - ENVELOPE_VIEW_SCALE_ADJUST) / 2.0;
            }
            else
            {
                ret = ((MaxBoxValue() - value) / MaxBoxValue()) * envelopesCanvas.HostCanvas.Height * ENVELOPE_VIEW_SCALE_ADJUST + envelopesCanvas.HostCanvas.Height * (1.0 - ENVELOPE_VIEW_SCALE_ADJUST) / 2.0;
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

            if (viewMode == ViewOrientationMode.Vertical)
            {
                posInScreen -= envelopesCanvas.HostCanvas.Width * (1.0 - ENVELOPE_VIEW_SCALE_ADJUST) / 2.0;
                posInScreen /= ENVELOPE_VIEW_SCALE_ADJUST;
                posInScreen /= envelopesCanvas.HostCanvas.Width;
                posInScreen *= MaxBoxValue();

                ret = posInScreen;
            }
            else
            {
                posInScreen -= envelopesCanvas.HostCanvas.Height * (1.0 - ENVELOPE_VIEW_SCALE_ADJUST) / 2.0;
                posInScreen /= ENVELOPE_VIEW_SCALE_ADJUST;
                posInScreen /= envelopesCanvas.HostCanvas.Height;
                posInScreen *= MaxBoxValue();
                posInScreen -= MaxBoxValue();
                posInScreen *= -1;

                ret = posInScreen;
            }
            return ret;
        }

        /// <summary>
        /// Updates envelope value based on screen point.
        /// </summary>
        /// <param name="evb"></param>
        /// <param name="point"></param>
        internal void UpdateEnvPoint(EnvelopeBox evb, Point point)
        {
            if (viewMode == ViewOrientationMode.Vertical)
            {
                SetEnvelopeValue(evb, ScreenToValue(point.X));
                SetEnvelopeTimeStamp(evb, ConvertPixelToTimeStampInSeconds(point));
            }
            else
            {
                SetEnvelopeValue(evb, ScreenToValue(point.Y));
                SetEnvelopeTimeStamp(evb, ConvertPixelToTimeStampInSeconds(point));
            }
        }

        /// <summary>
        /// View orientation mode.
        /// </summary>
        /// <param name="vmode"></param>
        public void SetOrientation(ViewOrientationMode vmode)
        {
            viewMode = vmode;
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

        internal abstract double DefaulBoxValue();

        /// <summary>
        /// Reset Envelope box to default value.
        /// </summary>
        /// <param name="envelopeBox"></param>
        public void ResetEnvelopeBox(EnvelopeBox envelopeBox)
        {
            SetEnvelopeValue(envelopeBox, DefaulBoxValue());
            int envelopeBoxIndex = envelopeBoxes.IndexOf(envelopeBox);

            if (viewMode == ViewOrientationMode.Vertical)
            {
                Canvas.SetLeft(envelopeBox, ValueToScreen(GetEnvelopeValue(envelopeBox)) - envelopeBox.Width / 2);
                Point point = envPolyLine.Points[envelopeBoxIndex];
                point.X = ValueToScreen(GetEnvelopeValue(envelopeBox));
                envPolyLine.Points[envelopeBoxIndex] = point;
            }
            else
            {
                Canvas.SetTop(envelopeBox, ValueToScreen(GetEnvelopeValue(envelopeBox)) - envelopeBox.Height / 2);
                Point point = envPolyLine.Points[envelopeBoxIndex];
                point.Y = ValueToScreen(GetEnvelopeValue(envelopeBox));
                envPolyLine.Points[envelopeBoxIndex] = point;
            }
        }

        internal void Release()
        {
            // envelopesCanvas = null;
        }
    }
}
