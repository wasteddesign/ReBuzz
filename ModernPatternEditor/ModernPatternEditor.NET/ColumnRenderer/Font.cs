using BuzzGUI.Common;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace WDE.ModernPatternEditor.ColumnRenderer
{
    static class Font
    {
        static Typeface font;
        static double fontSize;
        static double fontWidth;
        static GlyphTypeface glyphTypeface;

        public static void Update()
        {
            font = null;
            BeatVisualCache.Clear();
        }

        static void EnsureFontCreated()
        {
            if (font == null)
            {

                FontStyle style = (FontStyle)typeof(FontStyles).GetProperties(BindingFlags.Public | BindingFlags.Static).Where(fi => fi.Name == PatternEditor.Settings.FontStyle).First().GetValue(null, null);
                FontWeight weight = (FontWeight)typeof(FontWeights).GetProperties(BindingFlags.Public | BindingFlags.Static).Where(fi => fi.Name == PatternEditor.Settings.FontWeight).First().GetValue(null, null);
                FontStretch stretch = (FontStretch)typeof(FontStretches).GetProperties(BindingFlags.Public | BindingFlags.Static).Where(fi => fi.Name == PatternEditor.Settings.FontStretch).First().GetValue(null, null);

                if (!Fonts.SystemFontFamilies.Any(ff => ff.Source == PatternEditor.Settings.FontFamily))
                    PatternEditor.Settings.FontFamily = "Courier New";

                font = new Typeface(new FontFamily(PatternEditor.Settings.FontFamily), style, weight, stretch);
                fontSize = PatternEditor.Settings.FontSize;

                fontWidth = GetFormattedText("A", Brushes.Black).Width;

                font.TryGetGlyphTypeface(out glyphTypeface);

            }
        }

        public static double LineHeight
        {
            get
            {
                EnsureFontCreated();
                return WPFExtensions.RoundDipForDisplayMode(font.FontFamily.LineSpacing * fontSize);
            }
        }

        public static FormattedText GetFormattedText(string text, Brush brush)
        {
            EnsureFontCreated();
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, font, fontSize, brush, null, PatternEditor.Settings.TextFormattingMode);
        }

        public static double GetWidth(int digitcount)
        {
            EnsureFontCreated();
            return fontWidth * (digitcount + 1);
        }

        public static double[] GetAdvanceWidths(string text, TextFormattingMode tfm)
        {
            double[] advanceWidths = new double[text.Length];

            for (int n = 0; n < text.Length; n++)
            {
                char ch = text[n];
                if (ch < 32) ch = '?';
                double width = 6 * fontSize;
                if (glyphTypeface != null && glyphTypeface.CharacterToGlyphMap.ContainsKey(ch))
                    width = glyphTypeface.AdvanceWidths[glyphTypeface.CharacterToGlyphMap[ch]] * fontSize;
                if (tfm == TextFormattingMode.Display) width = WPFExtensions.RoundDipForDisplayMode(width);

                advanceWidths[n] = width;
            }

            return advanceWidths;
        }

        public static void DrawGlyphRun(DrawingContext dc, string text, Point p, double maxwidth, Brush brush, TextFormattingMode tfm)
        {
            ushort[] glyphIndexes = new ushort[text.Length];
            double[] advanceWidths = new double[text.Length];

            double totalWidth = 0;

            for (int n = 0; n < text.Length; n++)
            {
                ushort glyphIndex = glyphTypeface.CharacterToGlyphMap[text[n]];
                glyphIndexes[n] = glyphIndex;

                double width = glyphTypeface.AdvanceWidths[glyphIndex] * fontSize;
                if (tfm == TextFormattingMode.Display) width = WPFExtensions.RoundDipForDisplayMode(width);

                advanceWidths[n] = width;

                totalWidth += width;
            }

            p.X += maxwidth / 2 - totalWidth / 2;
            p.Y += glyphTypeface.Baseline * fontSize;

            GuidelineSet gls = new GuidelineSet(null, new[] { p.Y });
            dc.PushGuidelineSet(gls);

            GlyphRun glyphRun = new GlyphRun(glyphTypeface, 0, false, fontSize,
                glyphIndexes, p, advanceWidths, null, null, null, null,
                null, null);

            dc.DrawGlyphRun(brush, glyphRun);

            dc.Pop();
        }

    }
}
