using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Formats.Tar;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BuzzGUI.SequenceEditor
{
	public class PatternElement : Canvas, INotifyPropertyChanged
    {
		static readonly int MaxCacheableWidth = WPFExtensions.DPI == 96 ? 1024 : 0;

		internal SequenceEvent se;
		ViewSettings viewSettings;
		TrackControl tc;
		bool renderPending = false;
		internal int time;

        Brush patternEventHintBrush;
        Brush patternEventHintBrushOff;
        Brush patternEventHintParam;
        Brush patternEventBorder;
        Brush trFill;
        Brush trStroke;

        public PatternElement(TrackControl tc, int time, SequenceEvent se, ViewSettings vs)
		{
			this.tc = tc;
			this.time = time;
			this.se = se;
			viewSettings = vs;
            /*
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
            */
            Loaded += (sender, e) =>
            {
                //SequenceEditor.Settings.PropertyChanged += Settings_PropertyChanged;
                //this.MouseRightButtonDown += PatternElement_MouseRightButtonDown;
                //if (this.se.Type == SequenceEventType.PlayPattern)
                //    this.se.Pattern.PatternChanged += Pattern_PatternChanged;

                //tc.Editor.PropertyChanged += Editor_PropertyChanged;

                DrawElements();
            };

            Unloaded -= (sender, e) =>
            {
                Release();
            };

            patternEventHintBrush = tc.TryFindResource("SeqEdPatternEventNote") as SolidColorBrush;
            if (patternEventHintBrush == null)
                patternEventHintBrush = Brushes.LightGreen;

            patternEventHintBrushOff = tc.TryFindResource("SeqEdPatternEventNoteOff") as SolidColorBrush;
            if (patternEventHintBrushOff == null)
                patternEventHintBrushOff = Brushes.Gray;

            patternEventHintParam = tc.TryFindResource("SeqEdPatternEventParam") as SolidColorBrush;
            if (patternEventHintParam == null)
                patternEventHintParam = Brushes.Orange;

            patternEventBorder = tc.TryFindResource("SeqEdPatternEventBorder") as SolidColorBrush;
            if (patternEventBorder == null)
                patternEventBorder = Brushes.Black;

            trFill = tc.TryFindResource("PatternBoxBgRectFillBrush") as SolidColorBrush;
            trStroke = tc.TryFindResource("PatternBoxBgRectStrokeBrush") as SolidColorBrush;
            trFill = trFill != null ? trFill : new SolidColorBrush(Color.FromArgb(0x90, 0xe0, 0xe0, 0xff));
            trStroke = trStroke != null ? trStroke : new SolidColorBrush(Color.FromArgb(0x40, 0x33, 0x33, 0x33));

            patternEventHintBrush.Freeze();
            patternEventHintBrushOff.Freeze();
            patternEventHintParam.Freeze();
            patternEventBorder.Freeze();
        }

		static BrushSet[] brushSet = new BrushSet[3];
		static Brush[] borderBrush = new Brush[1];
		static Brush textBrush;

		public static Tuple<string, BrushSet>[] PatternBrushes;

		static Typeface font = new Typeface("Segoe UI");

        private Line dragLeft;
        private Line dragRight;

        public void SetDragHitTestVisibility(bool visible)
        {
            if (dragLeft != null && dragRight != null)
            {
                dragLeft.IsHitTestVisible = dragRight.IsHitTestVisible = visible;

                if (visible)
                {
                    if (dragLeft.IsMouseDirectlyOver || dragRight.IsMouseDirectlyOver)
                    {
                        Mouse.OverrideCursor = Cursors.SizeWE;
                    }
                }
                else
                {
                    Mouse.OverrideCursor = null;
                }
            }
        }

        public static void InvalidateResources()
		{
			brushSet[0] = null;
			PatternVisualCache.Clear();
		}

		private void DrawElements()
		{
			//DebugConsole.WriteLine("PatternElement.OnRender " + IsVisible.ToString());

			//if (!IsVisible)
			//{
			//	renderPending = true;
			//	return;
			//}

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

            int spanReal = se.Pattern != null ? se.Pattern.Length : viewSettings.NonPlayPattenSpan;
            double w2 = spanReal * viewSettings.TickWidth;
            w2 = Math.Max(w, 10);

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

			
			//if (w <= MaxCacheableWidth)
			//{
			//	var bcbr = PatternVisualCache.Lookup(cktext, w, ci);
			//	if (bcbr == null)
			//	{
			//		var z = new PatternVisual(w, h, text, font, textBrush, borderBrush[0], bs.Brush, bs.HighlightBrush, bs.ShadowBrush);

			//		bcbr = new BitmapCacheBrush(z)
			//		{
			//			BitmapCache = new BitmapCache() { EnableClearType = true, SnapsToDevicePixels = true, RenderAtScale = 1.0 }
			//		};
			//		PatternVisualCache.Cache(cktext, w, ci, bcbr);
			//	}

			//	dc.DrawRectangle(bcbr, null, new Rect(0, 0, w + 1, h + 1));
			//}
			//else if (childVisual == null)
			//{
			//	childVisual = new PatternVisual(w, h, text, font, textBrush, borderBrush[0], bs.Brush, bs.HighlightBrush, bs.ShadowBrush);
			//	AddVisualChild(childVisual);

			//}

            Children.Clear();

            PatternVisual childVisual = new PatternVisual(w, h, cktext, font, textBrush, borderBrush[0], bs.Brush, bs.HighlightBrush, bs.ShadowBrush);
            Children.Add(new VisualHost { Visual = childVisual });

            dragLeft = new Line() { X1 = 4, Y1 = 4, X2 = 4, Y2 = h - 4, Stroke = Brushes.Transparent, StrokeThickness = 8, IsHitTestVisible = false };
            dragLeft.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
            this.Children.Add(dragLeft);
            dragRight = new Line() { X1 = w2 - 4, Y1 = 4, X2 = w2 - 4, Y2 = h - 4, Stroke = Brushes.Transparent, StrokeThickness = 8, IsHitTestVisible = false };
            dragRight.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
            this.Children.Add(dragRight);

            //SetDragHitTestVisibility(tc.Editor.KbHook.ControlPressed);
            SetDragHitTestVisibility(Keyboard.Modifiers == ModifierKeys.Control);

            dragRight.MouseEnter += (sender, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                    Mouse.OverrideCursor = Cursors.SizeWE;
            };
            dragRight.MouseLeave += (sender, e) =>
            {
                Mouse.OverrideCursor = null;
            };
            dragRight.PreviewMouseLeftButtonDown += (sender, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    this.PropertyChanged.Raise(this, "DragBottom");
                    e.Handled = true;
                }
            };


            dragLeft.MouseEnter += (sender, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                    Mouse.OverrideCursor = Cursors.SizeWE;
            };
            dragLeft.MouseLeave += (sender, e) =>
            {
                Mouse.OverrideCursor = null;
            };
            dragLeft.PreviewMouseLeftButtonDown += (sender, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    this.PropertyChanged.Raise(this, "DragTop");
                    e.Handled = true;
                }
            };
        }

        internal void Release()
        {
            Children.Clear();
            this.tc = null;
            this.viewSettings = null;
        }

        // PatternVisual childVisual;

        public event PropertyChangedEventHandler? PropertyChanged;

        //protected override int VisualChildrenCount { get { return childVisual != null ? 1 : 0; } }
		//protected override Visual GetVisualChild(int index) { if (childVisual == null) throw new ArgumentOutOfRangeException(); else return childVisual; }


	}
}
