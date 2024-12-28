using BuzzGUI.Common;
using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace WDE.ModernPatternEditor.ColumnRenderer
{
    static class BeatVisualCache
    {
        const int MaxSize = 200;

        static Dictionary<BeatVisualCacheKey, LinkedListNode<Tuple<BeatVisualCacheKey, BitmapCacheBrush>>> cache = new Dictionary<BeatVisualCacheKey, LinkedListNode<Tuple<BeatVisualCacheKey, BitmapCacheBrush>>>();
        static LinkedList<Tuple<BeatVisualCacheKey, BitmapCacheBrush>> list = new LinkedList<Tuple<BeatVisualCacheKey, BitmapCacheBrush>>();

        public static void Clear()
        {
            DebugConsole.WriteLine("BeatVisualCache.CLEAR {0}", cache.Count);
            cache.Clear();
            list.Clear();
        }

        public static BitmapCacheBrush Lookup(BeatVisualCacheKey key)
        {
            LinkedListNode<Tuple<BeatVisualCacheKey, BitmapCacheBrush>> node;
            cache.TryGetValue(key, out node);
            if (node == null) return null;

            list.Remove(node);
            list.AddFirst(node);

            return node.Value.Item2;
        }

        public static void Cache(BeatVisualCacheKey key, BitmapCacheBrush br)
        {
            if (list.Count >= MaxSize)
            {
                DebugConsole.WriteLine("BeatVisualCache.Cache {0} (remove)", cache.Count);

                var last = list.Last;
                list.RemoveLast();
                cache.Remove(last.Value.Item1);
            }
            else
            {
                DebugConsole.WriteLine("BeatVisualCache.Cache {0}", cache.Count);
            }

            var node = new LinkedListNode<Tuple<BeatVisualCacheKey, BitmapCacheBrush>>(Tuple.Create(key, br));
            cache[key] = node;
            list.AddFirst(node);
        }

    }
}
