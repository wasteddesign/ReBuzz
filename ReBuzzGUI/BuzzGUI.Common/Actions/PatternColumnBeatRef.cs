using BuzzGUI.Interfaces;

namespace BuzzGUI.Common.Actions
{
    public struct PatternColumnBeatRef
    {
        readonly PatternColumnRef column;
        readonly int beat;

        public int Beat { get { return beat; } }
        public IPatternColumn GetColumn(IPattern p) { return column.GetColumn(p); }

        public PatternColumnBeatRef(PatternColumnRef pcr, int beat)
        {
            this.column = pcr;
            this.beat = beat;
        }
    }
}
