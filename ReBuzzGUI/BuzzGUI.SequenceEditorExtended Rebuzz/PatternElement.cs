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
using BuzzGUI.Common;
using System.Diagnostics;

namespace BuzzGUI.SequenceEditor
{
	public class PatternElement : UIElement
	{
		static readonly int MaxCacheableWidth = WPFExtensions.DPI == 96 ? 1024 : 0;

		SequenceEvent se;
		ViewSettings viewSettings;
		TrackControl tc;
		bool renderPending = false;
		internal int time;

		public PatternElement(TrackControl tc, int time, SequenceEvent se, ViewSettings vs)
		{
			this.tc = tc;
			this.time = time;
			this.se = se;
			viewSettings = vs;

			this.IsVisibleChanged += (sender, e) =>
			{
				if (IsVisible)
				{
					if (renderPending)
					{
						renderPending = false;
						InvalidateVisual();
					}
				}
			};
		}

		static BrushSet[] brushSet = new BrushSet[3];
		static Brush[] borderBrush = new Brush[1];
		static Brush textBrush;

		public static Tuple<string, BrushSet>[] PatternBrushes;

		static Typeface font = new Typeface("Segoe UI");

		public static void InvalidateResources()
		{
			brushSet[0] = null;
			PatternVisualCache.Clear();
		}

		protected override void OnRender(DrawingContext dc)
		{
			//DebugConsole.WriteLine("PatternElement.OnRender " + IsVisible.ToString());

			if (!IsVisible)
			{
				renderPending = true;
				return;
			}

			if (brushSet[0] == null)
			{
				brushSet[0] = new BrushSet(tc.TryFindResource("PatternBoxBrush") as SolidColorBrush);
				brushSet[1] = new BrushSet(tc.TryFindResource("BreakBoxBrush") as SolidColorBrush);
				brushSet[2] = new BrushSet(tc.TryFindResource("MuteBoxBrush") as SolidColorBrush);

				borderBrush[0] = tc.TryFindResource("PatternBorderBrush") as Brush;
				textBrush = tc.TryFindResource("PatternTextBrush") as Brush;
				if (textBrush.CanFreeze) textBrush.Freeze();
				
				for (int i = 0; i < borderBrush.Length; i++)
				{
					if (borderBrush[i] != null && borderBrush[i].CanFreeze) borderBrush[i].Freeze();
				}

				var pc = tc.TryFindResource("PatternColors") as NamedColor[];
				if (pc != null) PatternBrushes = pc.Select(nc => Tuple.Create(nc.Name, new BrushSet(new SolidColorBrush(nc.Color)))).ToArray();

			}


			int span = se.Pattern != null ? se.Span : 4;
			string text = se.Pattern != null ? se.Pattern.Name : "";
			double w = span * viewSettings.TickWidth;
			double h = viewSettings.TrackHeight;

			string cktext = "";

			int bi = 0;
			switch (se.Type)
			{
				case SequenceEventType.PlayPattern: bi = 0; cktext = text; break;
				case SequenceEventType.Break: bi = 1; cktext = "<break>";  break;
				case SequenceEventType.Mute: bi = 2; cktext = "<mute>"; break;
				case SequenceEventType.Thru: bi = 2; cktext = "<thru>"; break;
			}

			BrushSet bs;
			int ci = -1;

			if (se.Type == SequenceEventType.PlayPattern && PatternBrushes != null)
			{
				PatternEx pex = null;

				if (viewSettings.PatternAssociations.TryGetValue(se.Pattern, out pex))
				//if (viewSettings.PatternAssociationsList.PatternAssociations.TryGetValue(se.Pattern, out pex))
					ci = pex.ColorIndex % PatternBrushes.Length;

				if (ci < 0 && SequenceEditor.Settings.PatternBoxColors == PatternBoxColorModes.Pattern)
					ci = tc.Sequence.Machine.Patterns.IndexOf(se.Pattern) % PatternBrushes.Length;

			}

			if (ci >= 0)
				bs = PatternBrushes[ci].Item2;
			else
				bs = brushSet[bi];

			
			if (w <= MaxCacheableWidth)
			{
				var bcbr = PatternVisualCache.Lookup(cktext, w, ci);
				if (bcbr == null)
				{
					var z = new PatternVisual(w, h, text, font, textBrush, borderBrush[0], bs.Brush, bs.HighlightBrush, bs.ShadowBrush);

					bcbr = new BitmapCacheBrush(z)
					{
						BitmapCache = new BitmapCache() { EnableClearType = true, SnapsToDevicePixels = true, RenderAtScale = 1.0 }
					};
					PatternVisualCache.Cache(cktext, w, ci, bcbr);
				}

				dc.DrawRectangle(bcbr, null, new Rect(0, 0, w + 1, h + 1));
			}
			else if (childVisual == null)
			{
				childVisual = new PatternVisual(w, h, text, font, textBrush, borderBrush[0], bs.Brush, bs.HighlightBrush, bs.ShadowBrush);
				AddVisualChild(childVisual);

			}
			
		}

		PatternVisual childVisual;

		protected override int VisualChildrenCount { get { return childVisual != null ? 1 : 0; } }
		protected override Visual GetVisualChild(int index) { if (childVisual == null) throw new ArgumentOutOfRangeException(); else return childVisual; }


	}
}
