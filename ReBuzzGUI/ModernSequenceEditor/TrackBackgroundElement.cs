using System.Windows;
using System.Windows.Media;

namespace WDE.ModernSequenceEditor
{
    public class TrackBackgroundElement : FrameworkElement
    {
        ViewSettings viewSettings;
        public ViewSettings ViewSettings
        {
            set
            {
                viewSettings = value;
                InvalidateMeasure();
            }

        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(viewSettings.Height, availableSize.Height == double.PositiveInfinity ? 0 : availableSize.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            return finalSize;
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (viewSettings == null) return;
            var tbr = TryFindResource("SeqEdTimelineTextBrush") as Brush;
            var lbr = TryFindResource("SeqEdTimelineLineBrush") as Brush;

            foreach (var bar in BuzzGUI.SequenceEditor.SequenceEditor.ViewSettings.TimeSignatureList.GetBars(viewSettings.SongEnd))
            {
                double y = bar.Item1 * viewSettings.TickHeight;
                double w = bar.Item2 * viewSettings.TickHeight;

                if (y > 0) dc.DrawRectangle(lbr, null, new Rect(0, y, ActualWidth, 1));

            }

            dc.DrawRectangle(lbr, null, new Rect(0, viewSettings.SongEnd * viewSettings.TickHeight, ActualWidth, 1));


        }
    }
}
