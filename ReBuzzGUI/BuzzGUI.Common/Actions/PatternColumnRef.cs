using BuzzGUI.Interfaces;

namespace BuzzGUI.Common.Actions
{
    public struct PatternColumnRef
    {
        readonly int index;

        public IPatternColumn GetColumn(IPattern p) { return p.Columns[index]; }

        public PatternColumnRef(IPatternColumn pc)
        {
            index = pc.Pattern.Columns.IndexOf(pc);
        }
    }
}
