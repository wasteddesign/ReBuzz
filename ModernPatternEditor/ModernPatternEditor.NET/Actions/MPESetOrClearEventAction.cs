using BuzzGUI.Common.Actions;
using BuzzGUI.Common.Templates;
using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.Linq;
using WDE.ModernPatternEditor.MPEStructures;

namespace WDE.ModernPatternEditor.Actions
{
    public class MPESetOrClearEventsAction : PatternAction
    {
        PatternEvent[] newevents;
        PatternEvent[] oldevents;
        bool set;
        int columnIndex;

        MPEPattern mpePattern;

        public MPESetOrClearEventsAction(IPattern Pattern, MPEPatternColumn mpePatternColumn, IEnumerable<PatternEvent> events, bool set)
            : base(Pattern)
        {
            this.newevents = events.ToArray();
            this.set = set;

            this.mpePattern = mpePatternColumn.MPEPattern;
            columnIndex = mpePattern.MPEPatternColumns.IndexOf(mpePatternColumn);
        }

        protected override void DoAction()
        {
            var mpeColumn = mpePattern.MPEPatternColumns[columnIndex];

            if (set)
            {
                var times = newevents.Select(e => e.Time).ToHashSet();
                oldevents = mpeColumn.GetEvents(times.Min(), times.Max() + 1).Where(e => times.Contains(e.Time)).ToArray();
                mpeColumn.SetEvents(oldevents, false);
                mpeColumn.SetEvents(newevents, true);
            }
            else
            {
                mpeColumn.SetEvents(newevents, false);
            }


            var pattern = this.mpePattern.Pattern;
            if (pattern != null)
            {
                mpePattern.Editor.PatternChanged(pattern);
            }

            mpePattern.Editor.SetModifiedFlag();
        }

        protected override void UndoAction()
        {
            //var col = Pattern.Columns[columnIndex];
            var mpeColumn = mpePattern.MPEPatternColumns[columnIndex];

            if (set)
            {
                mpeColumn.SetEvents(newevents, false);
                mpeColumn.SetEvents(oldevents, true);
            }
            else
            {
                mpeColumn.SetEvents(newevents, true);
            }

            mpePattern.Editor.SetModifiedFlag();
        }
    }
}
