using BuzzGUI.Common.Actions;
using BuzzGUI.Interfaces;
using System.Collections.Generic;
using WDE.ModernPatternEditor.MPEStructures;

namespace WDE.ModernPatternEditor.Actions
{
    public class MPEQuantizeBeatsAction : PatternAction
    {
        Dictionary<int, List<PatternEvent>> oldData = new Dictionary<int, List<PatternEvent>>();
        MPEPattern mpePattern;
        Selection selection;

        public MPEQuantizeBeatsAction(MPEPattern pattern, Selection selection)
            : base(pattern.Pattern)
        {
            this.selection = selection;
            this.mpePattern = pattern;
        }

        protected override void DoAction()
        {
            Digit columnIterator = selection.Bounds.Item1;
            Digit columnEndDigit = selection.Bounds.Item2;

            while (true)
            {
                var buzzCol = columnIterator.ParameterColumn.PatternColumn;
                var mpeCol = mpePattern.GetColumn(buzzCol);
                int beatLength = PatternControl.BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase;
                int start = beatLength * columnIterator.Beat;
                int end = start + beatLength * (columnEndDigit.Beat - columnIterator.Beat + 1);
                List<PatternEvent> oldEvents = new List<PatternEvent>();
                foreach (var e in mpeCol.GetEvents(start, end))
                    oldEvents.Add(new PatternEvent(e.Time, e.Value, e.Duration));
                // Save
                oldData[columnIterator.Column] = oldEvents;

                // Snap to row
                mpeCol.Quantize(columnIterator, columnEndDigit.SetColumn(columnIterator.Column));
                columnIterator = columnIterator.SetColumn(columnIterator.Column + 1);
                if (columnIterator.Column > columnEndDigit.Column)
                    break;
            }
        }

        protected override void UndoAction()
        {
            Digit columnIterator = selection.Bounds.Item1;
            Digit columnEndDigit = selection.Bounds.Item2;

            while (true)
            {
                var buzzCol = columnIterator.ParameterColumn.PatternColumn;
                var mpeCol = mpePattern.GetColumn(buzzCol);
                int beatLength = PatternControl.BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase;
                int start = beatLength * columnIterator.Beat;
                int end = start + beatLength * (columnEndDigit.Beat - columnIterator.Beat + 1);
                List<PatternEvent> oldEvents = oldData[columnIterator.Column];
                mpeCol.ClearRegion(end - start, start, columnIterator);
                // Save
                mpeCol.SetEvents(oldEvents.ToArray(), true);

                columnIterator = columnIterator.SetColumn(columnIterator.Column + 1);
                if (columnIterator.Column > columnEndDigit.Column)
                    break;
            }
        }
    }
}
