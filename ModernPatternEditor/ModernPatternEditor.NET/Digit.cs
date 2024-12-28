using BuzzGUI.Common;
using BuzzGUI.Common.Actions;
using System;
using WDE.ModernPatternEditor.MPEStructures;

namespace WDE.ModernPatternEditor
{
    public struct Digit
    {
        public readonly PatternVM PatternVM;
        public readonly int ColumnSet;
        public readonly int Column;
        public readonly int Beat;
        public readonly int RowInBeat;
        public readonly int Index;

        public Digit(PatternVM patternVM)
        {
            this.PatternVM = patternVM;
            this.ColumnSet = 0;
            this.Column = 0;
            this.Beat = 0;
            this.RowInBeat = 0;
            this.Index = 0;
        }

        public Digit(PatternVM patternVM, int columnSet, int column, int beat, int rowInBeat, int index)
        {
            this.PatternVM = patternVM;
            this.ColumnSet = columnSet;
            this.Column = column;
            this.Beat = beat;
            this.RowInBeat = rowInBeat;
            this.Index = index;
        }

        public ParameterColumn? ParameterColumn { get { return PatternVM.GetColumn(this) as ParameterColumn; } }

        public MPEPatternColumnBeatRef ColumnAndBeat
        {
            get
            {
                // ToDo: 1. Rename all MPEPatternColumnBeatRef to MPEMPEPatternColumnBeatRef and same with MPEPatternColumnRef
                //       2. Create new versions of these
                //       3. Make sure MPEMPEPatternColumnBeatRef return correct index
                return new MPEPatternColumnBeatRef(new MPEPatternColumnRef(ParameterColumn.PatternColumn), Beat);
            }
        }

        public Digit SetPatternVM(PatternVM patternVM) { return new Digit(patternVM, ColumnSet, Column, Beat, RowInBeat, Index); }
        public Digit SetColumnSet(int columnSet) { return new Digit(PatternVM, columnSet, Column, Beat, RowInBeat, Index); }
        public Digit SetColumn(int column) { return new Digit(PatternVM, ColumnSet, column, Beat, RowInBeat, Index); }
        public Digit SetBeat(int beat) { return new Digit(PatternVM, ColumnSet, Column, beat, RowInBeat, Index); }
        public Digit SetRowInBeat(int rowInBeat) { return new Digit(PatternVM, ColumnSet, Column, Beat, rowInBeat, Index); }
        public Digit SetIndex(int index) { return new Digit(PatternVM, ColumnSet, Column, Beat, RowInBeat, index); }

        public Digit FirstColumnSet { get { return new Digit(PatternVM, 0, Column, Beat, RowInBeat, Index); } }
        public Digit FirstColumn { get { return new Digit(PatternVM, ColumnSet, 0, Beat, RowInBeat, Index); } }
        public Digit FirstBeat { get { return new Digit(PatternVM, ColumnSet, Column, 0, RowInBeat, Index); } }
        public Digit FirstRowInBeat { get { return new Digit(PatternVM, ColumnSet, Column, Beat, 0, Index); } }
        public Digit FirstIndex { get { return new Digit(PatternVM, ColumnSet, Column, Beat, RowInBeat, 0); } }

        public Digit LastColumnSet { get { return new Digit(PatternVM, PatternVM.ColumnSets.Count - 1, Column, Beat, RowInBeat, Index); } }
        public Digit LastColumn { get { return new Digit(PatternVM, ColumnSet, PatternVM.GetColumnSet(this).Columns.Count - 1, Beat, RowInBeat, Index); } }
        public Digit LastBeat { get { return new Digit(PatternVM, ColumnSet, Column, PatternVM.GetColumnSet(this).BeatCount - 1, RowInBeat, Index); } }
        public Digit LastRowInBeat { get { return new Digit(PatternVM, ColumnSet, Column, Beat, PatternVM.GetBeat(this).Rows.Count - 1, Index); } }
        public Digit LastIndex { get { return new Digit(PatternVM, ColumnSet, Column, Beat, RowInBeat, PatternVM.GetColumn(this).DigitCount - 1); } }

        public Digit PreviousColumnSet { get { return new Digit(PatternVM, ColumnSet - 1, Column, Beat, RowInBeat, Index).ConstrainedColumnSet; } }
        public Digit PreviousColumn { get { return new Digit(PatternVM, ColumnSet, Column - 1, Beat, RowInBeat, Index).ConstrainedColumn; } }
        public Digit PreviousBeat { get { return new Digit(PatternVM, ColumnSet, Column, Beat - 1, RowInBeat, Index).ConstrainedBeat; } }
        public Digit PreviousRowInBeat { get { return new Digit(PatternVM, ColumnSet, Column, Beat, RowInBeat - 1, Index).ConstrainedRowInBeat; } }
        public Digit PreviousIndex { get { return new Digit(PatternVM, ColumnSet, Column, Beat, RowInBeat, Index - 1).ConstrainedIndex; } }

        public Digit NextColumnSet { get { return new Digit(PatternVM, ColumnSet + 1, Column, Beat, RowInBeat, Index).ConstrainedColumnSet; } }
        public Digit NextColumn { get { return new Digit(PatternVM, ColumnSet, Column + 1, Beat, RowInBeat, Index).ConstrainedColumn; } }
        public Digit NextBeat { get { return new Digit(PatternVM, ColumnSet, Column, Beat + 1, RowInBeat, Index).ConstrainedBeat; } }
        public Digit NextRowInBeat { get { return new Digit(PatternVM, ColumnSet, Column, Beat, RowInBeat + 1, Index).ConstrainedRowInBeat; } }

        public Digit NextIndex
        {
            get
            {
                if (PatternVM.GetColumn(this).Type == ColumnRenderer.ColumnType.Note && Index == 0)
                    return new Digit(PatternVM, ColumnSet, Column, Beat, RowInBeat, Index + 2).ConstrainedIndex;
                else
                    return new Digit(PatternVM, ColumnSet, Column, Beat, RowInBeat, Index + 1).ConstrainedIndex;
            }
        }

        public Digit ConstrainedColumnSet
        {
            get
            {
                return SetColumnSet(Math.Min(Math.Max(ColumnSet, 0), PatternVM.ColumnSets.Count - 1));
            }
        }

        public Digit ConstrainedColumn
        {
            get
            {
                var cs = PatternVM.GetColumnSet(this);
                return SetColumn(Math.Min(Math.Max(Column, 0), cs.Columns.Count - 1));
            }
        }

        public Digit ConstrainedIndex
        {
            get
            {
                var column = PatternVM.GetColumn(this);
                int newIndex = Math.Min(Math.Max(Index, 0), column.DigitCount - 1);
                if (column.Type == ColumnRenderer.ColumnType.Note && column.DigitCount == 3 && Index == 1) newIndex = 0;        // note middle digit
                return SetIndex(newIndex);
            }
        }

        public Digit ConstrainedBeat
        {
            get
            {
                var cs = PatternVM.GetColumnSet(this);
                return SetBeat(Math.Min(Math.Max(Beat, 0), cs.BeatCount - 1));
            }
        }

        public Digit ConstrainedRowInBeat
        {
            get
            {
                var beat = PatternVM.GetBeat(this);
                return SetRowInBeat(Math.Min(Math.Max(0, RowInBeat), beat.Rows.Count - 1));
            }
        }

        public Digit Constrained
        {
            get
            {
                if (PatternVM == null || PatternVM.ColumnSets.Count == 0) return this;
                return ConstrainedColumnSet.ConstrainedColumn.ConstrainedIndex.ConstrainedBeat.ConstrainedRowInBeat;
            }
        }

        public bool IsFirstColumnSet { get { return ColumnSet == 0; } }
        public bool IsFirstColumn { get { return Column == 0; } }
        public bool IsFirstIndex { get { return Index == 0; } }
        public bool IsFirstBeat { get { return Beat == 0; } }
        public bool IsFirstRowInBeat { get { return RowInBeat == 0; } }

        public bool IsLastColumnSet { get { return ColumnSet == PatternVM.ColumnSets.Count - 1; } }
        public bool IsLastColumn { get { return Column == PatternVM.GetColumnSet(this).Columns.Count - 1; } }
        public bool IsLastIndex { get { return Index == PatternVM.GetColumn(this).DigitCount - 1; } }
        public bool IsLastBeat { get { return Beat == PatternVM.GetColumnSet(this).BeatCount - 1; } }
        public bool IsLastRowInBeat { get { return RowInBeat == PatternVM.GetBeat(this).Rows.Count - 1; } }

        public Digit Down
        {
            get
            {
                if (!IsLastRowInBeat)
                    return NextRowInBeat;
                else if (!IsLastBeat)
                    return NextBeat.FirstRowInBeat;
                else
                    return this;
            }
        }

        public Digit Up
        {
            get
            {
                if (!IsFirstRowInBeat)
                    return PreviousRowInBeat;
                else if (!IsFirstBeat)
                    return PreviousBeat.LastRowInBeat;
                else
                    return this;
            }
        }

        public Digit RightColumn
        {
            get
            {
                if (!IsLastColumn)
                    return NextColumn.FirstIndex.NearestRow(TimeInBeat);
                else if (!IsLastColumnSet)
                    return NextColumnSet.FirstColumn.FirstIndex.NearestRow(TimeInBeat);
                else
                    return this;
            }
        }


        public Digit Right
        {
            get
            {
                if (!IsLastIndex)
                    return NextIndex;
                else
                    return RightColumn;
            }
        }

        public Digit LeftColumn
        {
            get
            {
                if (!IsFirstColumn)
                    return PreviousColumn.LastIndex.NearestRow(TimeInBeat);
                else if (!IsFirstColumnSet)
                    return PreviousColumnSet.LastColumn.LastIndex.NearestRow(TimeInBeat);
                else
                    return this;

            }
        }

        public Digit Left
        {
            get
            {
                if (!IsFirstIndex)
                    return PreviousIndex;
                else
                    return LeftColumn;
            }
        }

        public int TimeInBeat { get { return PatternVM.GetBeat(this).Rows[RowInBeat].Time; } }

        public Digit NearestRow(int time)
        {
            return SetRowInBeat(PatternVM.GetBeat(this).Rows.IndexOfMinBy(r => Math.Abs(r.Time - time)));
        }

        public Digit Offset(int dx, int dy, bool skipdigits = false)
        {
            var d = this;

            while (dy > 0)
            {
                d = d.Down;
                dy--;
            }

            while (dy < 0)
            {
                d = d.Up;
                dy++;
            }

            while (dx > 0)
            {
                d = skipdigits ? d.RightColumn : d.Right;
                dx--;
            }

            while (dx < 0)
            {
                d = skipdigits ? d.LeftColumn : d.Left;
                dx++;
            }

            return d;
        }

        public Digit Home()
        {
            if (!IsFirstColumn)
                return FirstColumn.FirstIndex;
            else if (!IsFirstColumnSet)
                return FirstColumnSet.FirstIndex;
            else
                return FirstBeat.FirstRowInBeat;
        }

        public Digit End()
        {
            if (!IsLastColumn)
                return LastColumn.FirstIndex;
            else if (!IsLastColumnSet)
                return LastColumnSet.LastColumn.FirstIndex;
            else
                return LastBeat.LastRowInBeat;
        }

        public Digit Tab(bool reverse)
        {
            if (reverse)
            {
                if (!IsFirstColumn || !IsFirstIndex)
                    return FirstColumn.FirstIndex;
                else
                    return PreviousColumnSet;
            }
            else
            {
                return NextColumnSet.FirstColumn.FirstIndex;
            }

        }


    }
}
