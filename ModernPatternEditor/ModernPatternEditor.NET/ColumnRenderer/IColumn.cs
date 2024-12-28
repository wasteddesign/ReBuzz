namespace WDE.ModernPatternEditor.ColumnRenderer
{
    public enum ColumnType { DecValue, HexValue, Note, Ascii };

    public interface IColumn
    {
        ColumnType Type { get; }
        int DigitCount { get; }
        bool TiedToNext { get; }
        string Label { get; }
        string Description { get; }
        IBeat FetchBeat(int index);

    }
}
