using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Globalization;
using BuzzGUI.Interfaces;

namespace BuzzGUI.SequenceEditor
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
			return new Size(SequenceEditor.ViewSettings.Width, availableSize.Height == double.PositiveInfinity ? 0 : availableSize.Height);
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

			int barIndex = 0;

			foreach (var bar in SequenceEditor.ViewSettings.TimeSignatureList.GetBars(SequenceEditor.ViewSettings.SongEnd))
			{
				double x = bar.Item1 * SequenceEditor.ViewSettings.TickWidth;
				double w = bar.Item2 * SequenceEditor.ViewSettings.TickWidth;

				if (x > 0) dc.DrawRectangle(lbr, null, new Rect(x, 0, 1, ActualHeight));

				Typeface f = SequenceEditor.ViewSettings.TimeSignatureList.TimeSignatureChangesAt(bar.Item1) ? boldfont : font;

				FormattedText ft = new FormattedText(
					SequenceEditor.Settings.TimelineNumbers == TimelineNumberModes.Tick ? bar.Item1.ToString() : barIndex.ToString(),
					cultureInfo,
					FlowDirection.LeftToRight,
					f, 12, tbr);

				ft.TextAlignment = TextAlignment.Left;
				ft.MaxTextWidth = Math.Max(0, w - 3);
				ft.Trimming = TextTrimming.None;

				dc.DrawText(ft, new Point(x + 3, 0));

				barIndex++;
			}

			dc.DrawRectangle(lbr, null, new Rect(SequenceEditor.ViewSettings.SongEnd * SequenceEditor.ViewSettings.TickWidth, 0, 1, ActualHeight));


		}
	}
}
