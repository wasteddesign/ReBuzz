using BuzzGUI.Interfaces;
using WDE.ModernPatternEditor.MPEStructures;

namespace WDE.ModernPatternEditor.Actions
{
    public class PatternClipboard
    {
        // Column <set, index>, Beat related data; <beat, rows>
        Dictionary<int, OrderedDictionary<int, int>> columnBeatData;
        // Column <set, index>, events
        Dictionary<int, List<PatternEvent>> eventData;
        private int regionLenght;
        private int numColumns;

        public bool ContainsData { get { return columnBeatData != null; } }

        public int RegionLenght { get => regionLenght; }
        public int NumColumns { get => numColumns; }

        public PatternClipboard()
        {
        }

        public PatternClipboard(IPattern pattern, PatternClipboard c)
        {
            Clone(pattern, c);
        }

        public void Clone(IPattern pattern, PatternClipboard c)
        {
            if (c.columnBeatData != null)
            {
                columnBeatData = new Dictionary<int, OrderedDictionary<int, int>>();
                foreach (var key in c.columnBeatData.Keys)
                {
                    OrderedDictionary<int, int> dict = new OrderedDictionary<int, int>();
                    foreach (var e in c.columnBeatData[key])
                        dict[e.Key] = e.Value;
                    columnBeatData[key] = dict;
                }
            }
            if (c.eventData != null)
            {
                eventData = new Dictionary<int, List<PatternEvent>>();
                foreach (var key in c.eventData.Keys)
                {
                    List<PatternEvent> edata = new List<PatternEvent>();
                    foreach (var e in c.eventData[key])
                        edata.Add(e);
                    eventData[key] = edata;
                }

            }
            regionLenght = c.RegionLenght;
            numColumns = c.NumColumns;

        }

        public void Clear()
        {
            columnBeatData = null;
            eventData = null;
        }

        public void Cut(MPEPattern pattern, Selection r)
        {
            CutOrCopy(pattern, r, true);
        }

        public void Copy(MPEPattern pattern, Selection r)
        {
            CutOrCopy(pattern, r, false);
        }

        public void Paste(MPEPattern pattern, Selection r)
        {
            if (!ContainsData) return;

            Digit topLeftDigit = r.Bounds.Item1;
            int topTime = topLeftDigit.ParameterColumn.GetDigitTime(topLeftDigit);
            Digit columnIterator = topLeftDigit;

            foreach (int columnNumber in eventData.Keys)
            {
                var column = pattern.GetColumn(columnIterator.ParameterColumn.PatternColumn);
                var events = eventData[columnNumber];
                column.ClearRegion(RegionLenght, topTime, columnIterator);
                column.SetEvents(events.ToArray(), topTime, true);
                var beatData = columnBeatData[columnNumber];
                Digit beatIterator = new Digit(columnIterator.PatternVM, columnIterator.ColumnSet, columnIterator.Column, columnIterator.Beat, columnIterator.RowInBeat, columnIterator.Index);

                foreach (int beatRows in columnBeatData[columnNumber].Values)
                {
                    // Set rows in beat
                    pattern.Columns[columnNumber].SetBeatSubdivision(beatIterator.Beat, beatRows);
                    if (beatIterator.IsLastBeat)
                        break;
                    beatIterator = beatIterator.NextBeat;
                }

                var nextColumn = columnIterator.RightColumn;
                if (columnIterator.ColumnSet == nextColumn.ColumnSet && columnIterator.Column == nextColumn.Column)
                    break;
                columnIterator = nextColumn;
            }
        }

        void CutOrCopy(MPEPattern pattern, Selection r, bool cut)
        {
            var bounds = r.Bounds;
            Digit topLeftDigit = bounds.Item1;
            Digit bottomRightDigit = bounds.Item2;
            int start = topLeftDigit.ParameterColumn.GetDigitTime(topLeftDigit);
            int end = bottomRightDigit.ParameterColumn.GetDigitTime(bottomRightDigit.Down);
            if (bottomRightDigit.IsLastRowInBeat && bottomRightDigit.IsLastBeat)
                end = pattern.Pattern.Length * PatternEvent.TimeBase;
            regionLenght = end - start;
            numColumns = 1;
            Digit columnIterator = topLeftDigit;

            int firstBeat = topLeftDigit.Beat;
            int columnNumber = pattern.GetParamIndex(columnIterator.ParameterColumn.PatternColumn.Parameter, columnIterator.ParameterColumn.PatternColumn.Track);

            eventData = new Dictionary<int, List<PatternEvent>>();
            columnBeatData = new Dictionary<int, OrderedDictionary<int, int>>();

            while (true)
            {
                // Save event data
                var column = pattern.GetColumn(columnNumber);
                var events = column.GetEvents(start, end);
                List<PatternEvent> colEvents = new List<PatternEvent>();

                foreach (var e in events)
                {
                    PatternEvent patternEvent = new PatternEvent(e.Time - start, e.Value, e.Duration);
                    // Save to List
                    colEvents.Add(patternEvent);
                }
                eventData.Add(columnNumber, colEvents);

                if (cut)
                {
                    column.SetEvents(events.ToArray(), true);
                    column.ClearRegion(RegionLenght, start, topLeftDigit);
                }

                Digit beatIterator = new Digit(columnIterator.PatternVM, columnIterator.ColumnSet, columnIterator.Column, columnIterator.Beat, columnIterator.RowInBeat, columnIterator.Index);
                // Save beat data
                OrderedDictionary<int, int> beatData = new OrderedDictionary<int, int>();
                while (true)
                {
                    beatData[beatIterator.Beat - firstBeat] = columnIterator.PatternVM.GetBeat(beatIterator).Rows.Count;

                    if (beatIterator.Beat == bottomRightDigit.Beat)
                        break;
                    beatIterator = beatIterator.NextBeat;
                }
                columnBeatData.Add(columnNumber, beatData);

                if (columnIterator.Column == bottomRightDigit.Column && columnIterator.ColumnSet == bottomRightDigit.ColumnSet)
                    break;
                columnIterator = columnIterator.RightColumn;
                columnNumber++;
                numColumns++;
            }
        }
    }
}
