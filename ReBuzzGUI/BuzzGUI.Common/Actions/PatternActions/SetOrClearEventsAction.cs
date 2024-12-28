using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace BuzzGUI.Common.Actions.PatternActions
{
    public class SetOrClearEventsAction : PatternAction
    {
        readonly PatternEvent[] newevents;
        PatternEvent[] oldevents;
        readonly bool set;
        readonly int columnIndex;

        public SetOrClearEventsAction(IPatternColumn column, IEnumerable<PatternEvent> events, bool set)
            : base(column.Pattern)
        {
            this.newevents = events.ToArray();
            this.set = set;
            columnIndex = column.Pattern.Columns.IndexOf(column);
        }

        protected override void DoAction()
        {
            var col = Pattern.Columns[columnIndex];

            if (set)
            {
                var times = newevents.Select(e => e.Time).ToHashSet();
                oldevents = col.GetEvents(times.Min(), times.Max() + 1).Where(e => times.Contains(e.Time)).ToArray();
                col.SetEvents(oldevents, false);
                col.SetEvents(newevents, true);
            }
            else
            {
                col.SetEvents(newevents, false);
            }
        }

        protected override void UndoAction()
        {
            var col = Pattern.Columns[columnIndex];

            if (set)
            {
                col.SetEvents(newevents, false);
                col.SetEvents(oldevents, true);
            }
            else
            {
                col.SetEvents(newevents, true);
            }
        }

    }
}
