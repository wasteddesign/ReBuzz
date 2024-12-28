using System.Windows;
using System.Windows.Media;

namespace WDE.ModernSequenceEditor
{
    public class PatternVisual : DrawingVisual
    {
        public PatternVisual(double w, double h, string text, Typeface font, Brush textbr, Brush borderbr, Brush br, Brush hlbr, Brush shbr)
        {
            var dc = RenderOpen();

            if (w > 2)
            {
                if (br == null)
                {
                    br = Brushes.Transparent;
                    borderbr = Brushes.Transparent;
                }

                dc.DrawRectangle(borderbr, null, new Rect(0, 0, w + 1, h + 1));
                if (h > 1) dc.DrawRectangle(br, null, new Rect(1, 1, w - 1, h - 1));

                if (hlbr != null)
                {
                    dc.DrawRectangle(hlbr, null, new Rect(1, 1, w - 2, 1));
                    if (h > 3) dc.DrawRectangle(hlbr, null, new Rect(1, 2, 1, h - 3));
                }

                if (shbr != null)
                {
                    dc.DrawRectangle(shbr, null, new Rect(2, h - 1, w - 2, 1));
                    if (h > 3) dc.DrawRectangle(shbr, null, new Rect(w - 1, 2, 1, h - 3));
                }
            }

            //FormattedText ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, font, 12, textbr);

            //ft.MaxTextWidth = Math.Max(0, w - 2);
            //ft.TextAlignment = TextAlignment.Left;
            //ft.Trimming = TextTrimming.None;
            //ft.MaxLineCount = 1;

            //dc.DrawText(ft, new Point(4, 2));

            dc.Close();

        }
    }
    public class VisualHost : UIElement
    {
        public Visual Visual { get; set; }

        public VisualHost()
        {
            IsHitTestVisible = false;
        }

        protected override int VisualChildrenCount
        {
            get { return Visual != null ? 1 : 0; }
        }

        protected override Visual GetVisualChild(int index)
        {
            return Visual;
        }
    }
}
