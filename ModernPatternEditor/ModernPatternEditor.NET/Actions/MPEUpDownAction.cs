using BuzzGUI.Common;
using BuzzGUI.Common.Actions;
using BuzzGUI.Interfaces;
using WDE.ModernPatternEditor.MPEStructures;

namespace WDE.ModernPatternEditor.Actions
{
    public class MPEUpDownAction : PatternAction
    {
        MPEPattern mpePattern;
        private Selection selection;
        private int delta;

        public MPEUpDownAction(MPEPattern mpePattern, Selection selection, int delta)
            : base(mpePattern.Pattern)
        {
            this.mpePattern = mpePattern;
            this.selection = selection;
            this.delta = delta;
        }

        protected override void DoAction()
        {
            ShiftValue(delta);
        }

        private void ShiftValue(int shiftdelta)
        {
            var columniterator = selection.Bounds.Item1;
            var endDigit = selection.Bounds.Item2;

            while (true)
            {
                var digitIterator = columniterator;

                // Get last Beat & Row
                var columnEndDigit = digitIterator.SetBeat(endDigit.Beat);
                columnEndDigit = columnEndDigit.NearestRow(endDigit.TimeInBeat);

                while (true)
                {
                    int rowInBeat = digitIterator.RowInBeat;
                    var beat = digitIterator.ParameterColumn.FetchBeat(digitIterator.Beat);
                    var newValue = beat.Rows[rowInBeat].Type == ColumnRenderer.BeatValueType.NoValue ? -1 : beat.Rows[rowInBeat].Value;

                    if (newValue >= 0)
                    {
                        if (digitIterator.ParameterColumn.PatternColumn.Parameter.Type == ParameterType.Note)
                        {
                            if (newValue != BuzzNote.Off && newValue + shiftdelta > BuzzNote.Min && newValue + shiftdelta < BuzzNote.Max)
                            {
                                newValue = BuzzNote.FromMIDINote(BuzzNote.ToMIDINote(newValue) + shiftdelta);
                            }
                        }
                        else
                        {
                            newValue += shiftdelta;
                        }

                        foreach (var act in digitIterator.ParameterColumn.EditValue(digitIterator, newValue))
                            act.Do();
                    }

                    // Beats can have different number of rows, so use closest time
                    if (digitIterator.Beat * PatternControl.BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase + digitIterator.TimeInBeat >=
                        columnEndDigit.Beat * PatternControl.BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase + columnEndDigit.TimeInBeat)
                        break;

                    digitIterator = digitIterator.Down;
                }

                if (columniterator.ColumnSet >= endDigit.ColumnSet && columniterator.Column >= endDigit.Column)
                    break;

                columniterator = columniterator.RightColumn;
            }
        }

        protected override void UndoAction()
        {
            ShiftValue(-delta);
        }
    }
}
