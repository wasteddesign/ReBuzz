using BuzzGUI.Common;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WDE.ModernSequenceEditor
{
    public class TimelineElement : FrameworkElement
    {
        new public double Width
        {
            get { return base.Width; }
            set
            {
                if (value != Width)
                {
                    base.Width = value;
                    InvalidateVisual();
                }
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (SequenceEditor.ViewSettings == null) return new Size(0, 0);
            return new Size(SequenceEditor.ViewSettings.Height, availableSize.Height == double.PositiveInfinity ? 0 : availableSize.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            return finalSize;
        }

        static CultureInfo cultureInfo = CultureInfo.GetCultureInfo("en-us");
        static Typeface font = new Typeface("Segoe UI");
        static Typeface boldfont = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

        protected override void OnRender(DrawingContext dc)
        {
            if (SequenceEditor.ViewSettings == null) return;
            var tbr = TryFindResource("SeqEdTimelineTextBrush") as Brush;
            var lbr = TryFindResource("SeqEdTimelineLineBrush") as Brush;
            //var bgbr = TryFindResource("SeqEdTimelineBackgroundBrush") as Brush;

            //dc.DrawRectangle(bgbr, null, new Rect(0, 0, int.MaxValue, ActualHeight));

            double beatsPerMin = Global.Buzz.BPM;
            double tickPerBeat = Global.Buzz.TPB;
            double sPerTick = 1.0 / (beatsPerMin / 60.0 * tickPerBeat);

            int barIndex = 0;

            foreach (var bar in BuzzGUI.SequenceEditor.SequenceEditor.ViewSettings.TimeSignatureList.GetBars(SequenceEditor.ViewSettings.SongEnd))
            {
                double y = bar.Item1 * SequenceEditor.ViewSettings.TickHeight;
                double w = bar.Item2 * SequenceEditor.ViewSettings.TickHeight;

                if (y > 0) dc.DrawRectangle(lbr, null, new Rect(0, y, ActualHeight, 1));

                Typeface f = BuzzGUI.SequenceEditor.SequenceEditor.ViewSettings.TimeSignatureList.TimeSignatureChangesAt(bar.Item1) ? boldfont : font;

                string text = "";
                if (SequenceEditor.Settings.TimelineNumbers == TimelineNumberModes.Tick)
                    text = bar.Item1.ToString();
                else if (SequenceEditor.Settings.TimelineNumbers == TimelineNumberModes.Bar)
                    text = barIndex.ToString();
                else if (SequenceEditor.Settings.TimelineNumbers == TimelineNumberModes.Time)
                {
                    DateTime dt = new DateTime(0);
                    dt = dt.AddSeconds(((double)bar.Item1) * sPerTick);
                    text = dt.ToString("m:ss:ff", DateTimeFormatInfo.InvariantInfo);
                }
                else if (SequenceEditor.Settings.TimelineNumbers == TimelineNumberModes.SMPTE24)
                {
                    text = GetSMPTETimeStamp(24, bar.Item1, sPerTick);
                }
                else if (SequenceEditor.Settings.TimelineNumbers == TimelineNumberModes.SMPTE25)
                {
                    text = GetSMPTETimeStamp(25, bar.Item1, sPerTick);
                }
                else if (SequenceEditor.Settings.TimelineNumbers == TimelineNumberModes.SMPTE29_97)
                {
                    text = GetSMPTETimeStamp(29.97, bar.Item1, sPerTick);
                }
                else if (SequenceEditor.Settings.TimelineNumbers == TimelineNumberModes.SMPTE30)
                {
                    text = GetSMPTETimeStamp(30, bar.Item1, sPerTick);
                }
                else if (SequenceEditor.Settings.TimelineNumbers == TimelineNumberModes.SMPTE60)
                {
                    text = GetSMPTETimeStamp(60, bar.Item1, sPerTick);
                }

                FormattedText ft = new FormattedText(text,
                    cultureInfo,
                    FlowDirection.LeftToRight,
                    f, 12, tbr);

                ft.TextAlignment = TextAlignment.Left;
                //ft.MaxTextWidth = Math.Max(0, w - 3);
                ft.Trimming = TextTrimming.None;

                dc.DrawText(ft, new Point(0 + 3, y + 3));

                barIndex++;
            }

            dc.DrawRectangle(lbr, null, new Rect(0, SequenceEditor.ViewSettings.SongEnd * SequenceEditor.ViewSettings.TickHeight, ActualHeight, 1));


        }

        string GetSMPTETimeStamp(double framerate, double tick, double sPerTick)
        {
            DateTime dt = new DateTime(0);
            double seconds = (tick) * sPerTick;

            dt = dt.AddSeconds(seconds);
            string text = dt.ToString("HH:mm:ss", DateTimeFormatInfo.InvariantInfo);
            text += String.Format(":{0:00}", framerate * seconds % framerate);
            return text;
        }
    }
}
