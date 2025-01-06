using System.Collections.Generic;

namespace WDE.ModernPatternEditor
{
    public class PatternData
    {
        public List<ColumnData> Columns1 = new List<ColumnData>();
        public List<ColumnData> Columns2 = new List<ColumnData>();
        public string PatternName;
        public PatternData()
        {
        }
    }

    public class ColumnData
    {
        public string MachineName;
        public int index;
        public Dictionary<int, TrackerEvent> Events = new Dictionary<int, TrackerEvent>();

    }

    public struct TrackerEvent
    {
        int pos;
        int value;
        int length;
    }
}
