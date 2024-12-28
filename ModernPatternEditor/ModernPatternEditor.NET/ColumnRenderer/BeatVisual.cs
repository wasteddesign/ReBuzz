using BuzzGUI.Common;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WDE.ModernPatternEditor.ColumnRenderer
{
    class BeatVisual : DrawingVisual
    {
        static Random rnd = new Random();

        static bool stretch = false;
        static bool usecache = true;

        public void Render(ColumnSetElement cse, IColumnSet columnSet, int index, int h)
        {
            var beats = columnSet.Columns.Select(c => c.FetchBeat(index)).ToArray();
            var widths = columnSet.Columns.Select(c => GetColumnWidth(c)).ToArray();

            if (usecache)
            {
                var key = new BeatVisualCacheKey(beats, index & 1, widths, h);

                var br = BeatVisualCache.Lookup(key);
                if (br == null)
                {
                    br = CreateBitmapCacheBrush(cse, beats, index, widths, h);
                    BeatVisualCache.Cache(key, br);
                }

                var dc = RenderOpen();
                dc.DrawRectangle(br, null, new Rect(0, 0, widths.Sum(), h));
                dc.Close();
            }
            else
            {
                var dc = RenderOpen();
                RenderVisual(cse, dc, beats, index, widths, h);
                dc.Close();
            }
        }

        public static int GetColumnWidth(IColumn column)
        {
            int w = (int)Math.Ceiling(Font.GetWidth(column.TiedToNext ? column.DigitCount - 1 : column.DigitCount));
            return w;
        }


        BitmapCacheBrush CreateBitmapCacheBrush(ColumnSetElement cse, IBeat[] beats, int index, int[] w, int h)
        {
            var dv = new DrawingVisual();
            var dc = dv.RenderOpen();

            RenderVisual(cse, dc, beats, index, w, h);

            dc.Close();

            return new BitmapCacheBrush(dv) { BitmapCache = new BitmapCache() { EnableClearType = PatternEditor.Settings.FontClearType, SnapsToDevicePixels = true, RenderAtScale = 1.0 } };
        }

        static Brush backgroundBrush;
        static Brush darkBackgroundBrush;
        static Brush veryDarkBackgroundBrush;
        static Brush textBrush;
        static Brush textDropShadowBrush;
        static Brush noValueBrush;
        static Brush noValueDropShadowBrush;
        static Brush rowNumberBrush;
        static Brush rowNumberDropShadowBrush;
        static Brush[] noteBrushes;

        public static void InvalidateResources()
        {
            backgroundBrush = null;
        }

        SolidColorBrush GetBrush(ColumnSetElement cse, string themeColorName)
        {
            string xamlname = "ThemeColorOverride_" + themeColorName.Replace(" ", "_");
            var br = cse.TryFindResource(xamlname) as SolidColorBrush;
            if (br == null) br = new SolidColorBrush(Global.Buzz.ThemeColors[themeColorName]);
            br.Freeze();
            return br;
        }

        SolidColorBrush[] GetBrushArray(ColumnSetElement cse, string name)
        {
            var x = cse.TryFindResource(name);

            if (x is ObjectDataProvider)
            {
                var odp = x as ObjectDataProvider;
                if (odp.ObjectInstance == null || !(odp.ObjectInstance is HSPAColorProvider)) return null;
                return (odp.ObjectInstance as HSPAColorProvider)
                    .Colors.Select(c => new SolidColorBrush(c)).Do(b => b.Freeze()).ToArray();
            }
            else if (x is Color[])
            {
                // TODO
            }

            return null;
        }

        void RenderVisual(ColumnSetElement cse, DrawingContext dc, IBeat[] beats, int index, int[] w, int h)
        {
            if (backgroundBrush == null)
            {
                backgroundBrush = GetBrush(cse, "PE BG");
                darkBackgroundBrush = GetBrush(cse, "PE BG Dark");
                veryDarkBackgroundBrush = GetBrush(cse, "PE BG Very Dark");
                textBrush = GetBrush(cse, "PE Text");
                textDropShadowBrush = cse.TryFindResource("TextDropShadowBrush") as SolidColorBrush;
                noValueBrush = cse.TryFindResource("NoValueBrush") as SolidColorBrush;
                noValueDropShadowBrush = cse.TryFindResource("NoValueDropShadowBrush") as SolidColorBrush;
                rowNumberBrush = cse.TryFindResource("RowNumberBrush") as SolidColorBrush;
                rowNumberDropShadowBrush = cse.TryFindResource("RowNumberDropShadowBrush") as SolidColorBrush;
                noteBrushes = GetBrushArray(cse, "PatternNoteColors");
            }

            Brush br;

            /* if ((index & 3) == 0)
				br = veryDarkBackgroundBrush;
			else */
            if ((index & 1) == 0)
                br = darkBackgroundBrush;
            else
                br = backgroundBrush;

            //br = new SolidColorBrush(Color.FromRgb((byte)rnd.Next(), (byte)rnd.Next(), (byte)rnd.Next()));

            dc.DrawRectangle(br, null, new Rect(0, 0, w.Sum(), h));

            int x = 0;

            for (int i = 0; i < beats.Length; i++)
            {
                int xoffset = cse.ColumnSet.Columns[i].TiedToNext ? (int)(Math.Ceiling(Font.GetWidth(0)) / 2) : 0;

                if (PatternEditor.Settings.TextDropShadow && textDropShadowBrush != null)
                    DrawColumn(dc, i, beats[i], x + xoffset + 1, 1, w[i], h, true);

                DrawColumn(dc, i, beats[i], x + xoffset, 0, w[i], h, false);
                x += w[i];
            }
        }


        void DrawColumn(DrawingContext dc, int column, IBeat beat, int x, int yoffs, int w, int h, bool shadow)
        {
            var rows = beat.Rows;

            var lineheight = Font.LineHeight;

            double scaling = h / (lineheight * rows.Count);
            scaling *= 0.999;

            if (stretch)
                dc.PushTransform(new ScaleTransform(1.0, scaling));
            else
                dc.PushTransform(new ScaleTransform(1.0, Math.Min(scaling, 1.0)));

            if (!stretch)
                lineheight *= Math.Max(scaling, 1.0);


            for (int row = 0; row < rows.Count; row++)
            {
                var text = rows[row].ValueString;

                FormattedText ft;

                if (rows[row].Type == BeatValueType.RowNumber)
                    ft = Font.GetFormattedText(text, shadow ? rowNumberDropShadowBrush : rowNumberBrush);
                else if (rows[row].Type == BeatValueType.NoValue)
                    ft = Font.GetFormattedText(text, shadow ? noValueDropShadowBrush : noValueBrush);
                else if (rows[row].Type == BeatValueType.Note)
                {
                    if (!shadow)
                    {
                        int nv = (rows[row].Value & 15) - 1;
                        nv = nv * 7 % 12;

                        if (PatternEditor.Settings.ColorNote != ColorNoteMode.None && noteBrushes != null && rows[row].Value != BuzzNote.Off)
                            ft = Font.GetFormattedText(text, noteBrushes[nv]);
                        else
                            ft = Font.GetFormattedText(text, textBrush);
                    }
                    else
                    {
                        ft = Font.GetFormattedText(text, textDropShadowBrush);
                    }
                }
                else
                    ft = Font.GetFormattedText(text, shadow ? textDropShadowBrush : textBrush);

                ft.MaxTextWidth = w;
                ft.TextAlignment = TextAlignment.Center;
                ft.Trimming = TextTrimming.None;
                ft.MaxLineCount = 1;

                double y = row * lineheight;
                dc.DrawText(ft, new Point(x, y + yoffs));


                //Font.DrawGlyphRun(dc, values[row], new Point(x, y), w, textBrush, PatternEditor.Settings.TextFormattingMode);

            }

            dc.Pop();

        }
    }
}
