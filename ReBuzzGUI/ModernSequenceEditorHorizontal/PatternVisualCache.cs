using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace WDE.ModernSequenceEditorHorizontal
{
    internal static class PatternVisualCache
    {
        static Dictionary<Tuple<string, double, int>, BitmapCacheBrush> cache = new Dictionary<Tuple<string, double, int>, BitmapCacheBrush>();

        public static void Clear()
        {
            cache.Clear();
        }

        public static BitmapCacheBrush Lookup(string text, double width, int colorindex)
        {
            BitmapCacheBrush br;
            cache.TryGetValue(Tuple.Create(text, width, colorindex), out br);
            return br;
        }

        public static void Cache(string text, double width, int colorindex, BitmapCacheBrush br)
        {
            cache[Tuple.Create(text, width, colorindex)] = br;
        }

    }
}
