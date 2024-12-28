using BuzzGUI.Common.Actions;
using WDE.ModernPatternEditor.MPEStructures;

namespace WDE.ModernPatternEditor.Actions
{
    public class MPEInsertOrDeleteAction : PatternAction
    {
        int oldValue = -1;
        Digit cursorPos;
        MPEPattern mpePattern;
        bool delete;


        public MPEInsertOrDeleteAction(MPEPattern mpePattern, Digit cursorPos, bool delete)
            : base(mpePattern.Pattern)
        {
            this.mpePattern = mpePattern;
            this.cursorPos = new Digit(cursorPos.PatternVM, cursorPos.ColumnSet, cursorPos.Column, cursorPos.Beat, cursorPos.RowInBeat, cursorPos.Index);
            this.delete = delete;
        }

        protected override void DoAction()
        {
            if (delete)
            {
                var backupBeat = cursorPos.ParameterColumn.FetchBeat(cursorPos.Beat);
                oldValue = backupBeat.Rows[cursorPos.RowInBeat].Type == ColumnRenderer.BeatValueType.NoValue ? -1 : backupBeat.Rows[cursorPos.RowInBeat].Value;

                var iterator = new Digit(cursorPos.PatternVM, cursorPos.ColumnSet, cursorPos.Column, cursorPos.Beat, cursorPos.RowInBeat, cursorPos.Index);

                while (true)
                {
                    var downDigit = iterator.Down;
                    int downRowInBeat = downDigit.RowInBeat;
                    var downBeat = downDigit.ParameterColumn.FetchBeat(downDigit.Beat);
                    var newValue = downBeat.Rows[downRowInBeat].Type == ColumnRenderer.BeatValueType.NoValue ? -1 : downBeat.Rows[downRowInBeat].Value;

                    foreach (var act in iterator.ParameterColumn.EditValue(iterator, newValue))
                        act.Do();

                    iterator = iterator.Down;
                    if (iterator.IsLastBeat && iterator.IsLastRowInBeat)
                        break;
                }
                foreach (var act in iterator.ParameterColumn.EditValue(iterator, -1))
                    act.Do();
            }
            else // Insert
            {
                var iterator = cursorPos.LastBeat.LastRowInBeat;
                var backupBeat = iterator.ParameterColumn.FetchBeat(iterator.Beat);
                oldValue = backupBeat.Rows[iterator.RowInBeat].Type == ColumnRenderer.BeatValueType.NoValue ? -1 : backupBeat.Rows[iterator.RowInBeat].Value;

                while (iterator.ParameterColumn.GetDigitTime(iterator) > cursorPos.ParameterColumn.GetDigitTime(cursorPos))
                {
                    var upDigit = iterator.Up;
                    var upBeat = upDigit.ParameterColumn.FetchBeat(upDigit.Beat);
                    var upRow = upDigit.RowInBeat;
                    var newValue = upBeat.Rows[upRow].Type == ColumnRenderer.BeatValueType.NoValue ? -1 : upBeat.Rows[upRow].Value;

                    foreach (var act in iterator.ParameterColumn.EditValue(iterator, newValue))
                        act.Do();

                    iterator = iterator.Up;
                }
                foreach (var act in iterator.ParameterColumn.EditValue(iterator, -1))
                    act.Do();
            }
        }

        protected override void UndoAction()
        {
            if (delete)
            {
                var iterator = cursorPos.LastBeat.LastRowInBeat;

                while (iterator.ParameterColumn.GetDigitTime(iterator) > cursorPos.ParameterColumn.GetDigitTime(cursorPos))
                {
                    var upDigit = iterator.Up;
                    var upBeat = upDigit.ParameterColumn.FetchBeat(upDigit.Beat);
                    var upRow = upDigit.RowInBeat;
                    var newValue = upBeat.Rows[upRow].Type == ColumnRenderer.BeatValueType.NoValue ? -1 : upBeat.Rows[upRow].Value;

                    foreach (var act in iterator.ParameterColumn.EditValue(iterator, newValue))
                        act.Do();

                    iterator = iterator.Up;
                }
                foreach (var act in iterator.ParameterColumn.EditValue(iterator, oldValue))
                    act.Do();
            }
            else // Insert
            {
                var iterator = new Digit(cursorPos.PatternVM, cursorPos.ColumnSet, cursorPos.Column, cursorPos.Beat, cursorPos.RowInBeat, cursorPos.Index);

                while (true)
                {
                    var downDigit = iterator.Down;
                    int downRowInBeat = downDigit.RowInBeat;
                    var downBeat = downDigit.ParameterColumn.FetchBeat(downDigit.Beat);
                    var newValue = downBeat.Rows[downRowInBeat].Type == ColumnRenderer.BeatValueType.NoValue ? -1 : downBeat.Rows[downRowInBeat].Value;

                    foreach (var act in iterator.ParameterColumn.EditValue(iterator, newValue))
                        act.Do();

                    iterator = iterator.Down;
                    if (iterator.IsLastBeat && iterator.IsLastRowInBeat)
                        break;
                }
                foreach (var act in iterator.ParameterColumn.EditValue(iterator, oldValue))
                    act.Do();
            }
        }
    }
}
