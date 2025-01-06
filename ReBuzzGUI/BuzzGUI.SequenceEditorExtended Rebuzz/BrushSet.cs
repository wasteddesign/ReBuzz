using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using BuzzGUI.Common;

namespace BuzzGUI.SequenceEditor
{
	public class BrushSet
	{
		SolidColorBrush brush;
		public SolidColorBrush Brush { get { return SequenceEditor.Settings.PatternBoxLook != PatternBoxLooks.Invisible ? brush : null; } }

		SolidColorBrush highlightBrush;
		public SolidColorBrush HighlightBrush { get { return SequenceEditor.Settings.PatternBoxLook == PatternBoxLooks.ThreeDee ? highlightBrush : null; } }

		SolidColorBrush shadowBrush;
		public SolidColorBrush ShadowBrush { get { return SequenceEditor.Settings.PatternBoxLook == PatternBoxLooks.ThreeDee ? shadowBrush : null; } }

		public BrushSet(SolidColorBrush br)
		{
			if (br == null) return;
			brush = new SolidColorBrush(br.Color);
			highlightBrush = new SolidColorBrush(br.Color.Blend(Colors.White, 0.5));
			shadowBrush = new SolidColorBrush(br.Color.Blend(Colors.Black, 0.66));
			brush.Freeze();
			highlightBrush.Freeze();
			shadowBrush.Freeze();
		}

	}
}
