using System.Linq;

namespace WDE.ModernPatternEditor.ColumnRenderer
{
    class BeatVisualCacheKey
    {
        readonly int index;
        readonly int h;
        readonly int[] w;
        readonly int[] bc;
        readonly int[] values;
        readonly int hash;

        public BeatVisualCacheKey(IBeat[] beats, int index, int[] w, int h)
        {
            this.index = index;
            this.h = h;
            this.w = w;     // assuming w is immutable
            bc = beats.Select(b => b.Rows.Count).ToArray();
            values = beats.SelectMany(b => b.Rows.Select(r => r.Value)).ToArray();
            hash = CreateHash();
        }

        internal static int CombineHashCodes(int h1, int h2) { return (((h1 << 5) + h1) ^ h2); }

        int CreateHash()
        {
            var hash = CombineHashCodes(index, h);

            for (int i = 0; i < w.Length; i++) hash = ((hash << 5) + hash) ^ w[i];
            for (int i = 0; i < bc.Length; i++) hash = ((hash << 5) + hash) ^ bc[i];
            for (int i = 0; i < values.Length; i++) hash = ((hash << 5) + hash) ^ values[i];

            return hash;
        }

        public override bool Equals(object obj)
        {
            var x = obj as BeatVisualCacheKey;

            if (index != x.index || h != x.h || w.Length != bc.Length || bc.Length != x.bc.Length || values.Length != x.values.Length) return false;
            if (!w.SequenceEqual(x.w)) return false;
            if (!bc.SequenceEqual(x.bc)) return false;
            if (!values.SequenceEqual(x.values)) return false;

            return true;
        }


        public override int GetHashCode()
        {
            return hash;
        }

    }
}
