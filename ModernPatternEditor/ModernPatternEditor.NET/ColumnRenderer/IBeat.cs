using System.Collections.Generic;

namespace WDE.ModernPatternEditor.ColumnRenderer
{
    public enum BeatValueType { RowNumber, NoValue, Note, Parameter };

    public struct BeatRow
    {
        public BeatValueType Type;
        public int Value;
        public string ValueString;
        public int Time;
        public int VisualTime;
    }

    public interface IBeat
    {
        IList<BeatRow> Rows { get; }

    }
}
