using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace BuzzGUI.Common.Actions
{
    public class PatternColumnClip
    {
        readonly int columnIndex;
        readonly List<PatternEvent> events;

        public PatternColumnClip(IPatternColumn pc, int startTime, int endTime)
        {
            columnIndex = pc.Pattern.Columns.IndexOf(pc);
            events = pc.GetEvents(startTime, endTime).ToList();
            // TODO: metadata
        }

        public void CopyTo(IPatternColumn p)
        {
            var pc = p.Pattern.Columns[columnIndex];
            pc.SetEvents(events, true);
        }

    }
}
