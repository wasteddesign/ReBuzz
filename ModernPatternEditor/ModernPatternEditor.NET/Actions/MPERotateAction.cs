using BuzzGUI.Common.Actions;
using BuzzGUI.Interfaces;
using WDE.ModernPatternEditor.MPEStructures;

namespace WDE.ModernPatternEditor.Actions
{
    public class MPERotateAction : PatternAction
    {
        MPEPattern mpePattern;
        private Selection selection;
        private bool down;

        public MPERotateAction(MPEPattern mpePattern, Selection selection, bool down)
            : base(mpePattern.Pattern)
        {
            this.mpePattern = mpePattern;
            this.selection = selection;
            this.down = down;
        }

        protected override void DoAction()
        {
            Roll(down);
        }

        private void Roll(bool rollDown)
        {
            var columnIterator = selection.Bounds.Item1;
            var endDigit = selection.Bounds.Item2;

            if (!rollDown) // Up
            {
                while (true)
                {
                    var digitIterator = columnIterator;

                    // Get first value
                    var digitIteratorBeat = digitIterator.ParameterColumn.FetchBeat(digitIterator.Beat);

                    // Beats can have different number of rows, so use closest time
                    digitIterator = digitIterator.NearestRow(columnIterator.TimeInBeat);
                    int firstValue = digitIteratorBeat.Rows[digitIterator.RowInBeat].Type == ColumnRenderer.BeatValueType.NoValue ? -1 : digitIteratorBeat.Rows[digitIterator.RowInBeat].Value;

                    var nextDigit = digitIterator.Down;
                    var nextBeat = nextDigit.ParameterColumn.FetchBeat(nextDigit.Beat);

                    while (true)
                    {
                        int nextValue = nextBeat.Rows[nextDigit.RowInBeat].Type == ColumnRenderer.BeatValueType.NoValue ? -1 : nextBeat.Rows[nextDigit.RowInBeat].Value;

                        foreach (var act in digitIterator.ParameterColumn.EditValue(digitIterator, nextValue))
                            act.Do();

                        digitIterator = digitIterator.Down;
                        nextDigit = digitIterator.Down;
                        nextBeat = nextDigit.ParameterColumn.FetchBeat(nextDigit.Beat);

                        // Beats can have different number of rows, so use closest time
                        if (digitIterator.Beat * PatternControl.BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase + digitIterator.TimeInBeat >=
                            endDigit.Beat * PatternControl.BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase + endDigit.TimeInBeat)
                            break;
                    }

                    // Set first digit
                    foreach (var act in digitIterator.ParameterColumn.EditValue(digitIterator, firstValue))
                        act.Do();


                    if (columnIterator.ColumnSet >= endDigit.ColumnSet && columnIterator.Column >= endDigit.Column)
                        break;

                    columnIterator = columnIterator.RightColumn;
                }
            }
            else // Down
            {
                while (true)
                {
                    var digitIterator = columnIterator;

                    // Get last value
                    digitIterator = digitIterator.SetBeat(endDigit.Beat);
                    var digitIteratorBeat = digitIterator.ParameterColumn.FetchBeat(digitIterator.Beat);
                    digitIterator = digitIterator.NearestRow(endDigit.TimeInBeat);
                    int lastValue = digitIteratorBeat.Rows[digitIterator.RowInBeat].Type == ColumnRenderer.BeatValueType.NoValue ? -1 : digitIteratorBeat.Rows[digitIterator.RowInBeat].Value;

                    var prevDigit = digitIterator.Up;
                    var prevBeat = prevDigit.ParameterColumn.FetchBeat(prevDigit.Beat);

                    while (true)
                    {
                        int prevValue = prevBeat.Rows[prevDigit.RowInBeat].Type == ColumnRenderer.BeatValueType.NoValue ? -1 : prevBeat.Rows[prevDigit.RowInBeat].Value;

                        foreach (var act in digitIterator.ParameterColumn.EditValue(digitIterator, prevValue))
                            act.Do();

                        digitIterator = digitIterator.Up;
                        prevDigit = digitIterator.Up;
                        prevBeat = prevDigit.ParameterColumn.FetchBeat(prevDigit.Beat);

                        if (digitIterator.Beat * PatternControl.BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase + digitIterator.TimeInBeat <=
                            columnIterator.Beat * PatternControl.BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase + columnIterator.TimeInBeat)
                            break;
                    }

                    // Set first digit
                    foreach (var act in digitIterator.ParameterColumn.EditValue(digitIterator, lastValue))
                        act.Do();


                    if (columnIterator.ColumnSet >= endDigit.ColumnSet && columnIterator.Column >= endDigit.Column)
                        break;

                    columnIterator = columnIterator.RightColumn;
                }

            }
        }

        protected override void UndoAction()
        {
            Roll(!down);
        }
    }
}
