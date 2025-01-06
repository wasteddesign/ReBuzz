using BuzzGUI.Common.Actions;
using System;
using System.Collections.Generic;
using WDE.ModernPatternEditor.MPEStructures;

namespace WDE.ModernPatternEditor
{
    public struct Selection
    {
        public enum SelectionUnit { Field, Row, Beat, FullRow, FullBeat };

        readonly Digit start;
        readonly Digit end;
        readonly SelectionUnit unit;
        readonly bool active;

        public bool Active { get { return active; } }

        public Selection(PatternVM vm)
        {
            this.start = new Digit(vm);
            this.end = new Digit(vm);
            this.unit = SelectionUnit.Field;
            this.active = false;
        }

        private Selection(Digit start, Digit end, SelectionUnit unit, bool active)
        {
            this.start = start;
            this.end = end;
            this.unit = unit;
            this.active = active;
        }

        public Selection Constrained
        {
            get
            {
                return new Selection(start.Constrained, end.Constrained, unit, active);
            }
        }

        public static Selection Start(Digit d)
        {
            return new Selection(d, new Digit(), Selection.SelectionUnit.Field, false);
        }

        public Selection SetEnd(Digit d)
        {
            Digit e;

            switch (unit)
            {
                case SelectionUnit.Row:
                    e = new Digit(d.PatternVM, d.ColumnSet, end.Column, d.Beat, d.RowInBeat, end.Index);
                    break;

                case SelectionUnit.Beat:
                    e = new Digit(d.PatternVM, d.ColumnSet, end.Column, d.Beat, end.RowInBeat, end.Index);
                    break;

                case SelectionUnit.FullRow:
                    e = new Digit(d.PatternVM, end.ColumnSet, end.Column, d.Beat, d.RowInBeat, end.Index);
                    break;

                case SelectionUnit.FullBeat:
                    e = new Digit(d.PatternVM, end.ColumnSet, end.Column, d.Beat, end.RowInBeat, end.Index);
                    break;

                default:
                    e = d;
                    break;
            }

            bool singleField = start.ColumnSet == e.ColumnSet && start.Column == e.Column && start.Beat == e.Beat && start.RowInBeat == e.RowInBeat;
            // a single-field selection is not considered active
            return new Selection(start, e, unit, !singleField).Constrained;
        }

        public static Selection Empty(PatternVM p)
        {
            return new Selection(p);
        }

        public static Selection All(PatternVM p)
        {
            return new Selection(
                new Digit(p), new Digit(p).LastColumnSet.LastColumn.LastIndex.LastBeat.LastRowInBeat,
                SelectionUnit.Field, true);
        }

        public static Selection ColumnSetRow(Digit p)
        {
            return new Selection(p.FirstColumn.FirstIndex, p.LastColumn.FirstIndex, Selection.SelectionUnit.Row, true)
                .Constrained;
        }

        public static Selection ColumnSetBeat(Digit p)
        {
            return new Selection(p.FirstColumn.FirstRowInBeat.FirstIndex, p.LastColumn.LastRowInBeat, Selection.SelectionUnit.Beat, true)
                .Constrained;
        }

        public static Selection Column(Digit d)
        {
            return new Selection(d.FirstBeat.FirstRowInBeat, d.LastBeat.LastRowInBeat, Selection.SelectionUnit.Field, true);
        }

        public static Selection ColumnSet(Digit d)
        {
            return new Selection(d.FirstColumn.FirstBeat.FirstRowInBeat, d.LastColumn.LastBeat.LastRowInBeat, Selection.SelectionUnit.Field, true);
        }

        public static Selection Row(Digit d)
        {
            return new Selection(d.FirstColumnSet.FirstColumn, d.LastColumnSet.LastColumn, Selection.SelectionUnit.FullRow, true);
        }

        public static Selection AllColumnsOfBeat(Digit d)
        {
            return new Selection(d.FirstColumnSet.FirstColumn.FirstRowInBeat,
                d.LastColumnSet.LastColumn.LastRowInBeat,
                Selection.SelectionUnit.FullBeat, true);
        }



        public Tuple<Digit, Digit> Bounds
        {
            get
            {

                var a = start;
                var b = end;

                Digit tl = new Digit();
                Digit br = new Digit();

                if (a.ColumnSet < b.ColumnSet || (a.ColumnSet == b.ColumnSet && a.Column < b.Column))
                {
                    tl = a;
                    br = b;
                }
                else
                {
                    tl = b;
                    br = a;
                }

                if (a.Beat < b.Beat || (a.Beat == b.Beat && a.RowInBeat < b.RowInBeat))
                {
                    tl = tl.SetBeat(a.Beat).SetRowInBeat(a.RowInBeat);
                    br = br.SetBeat(b.Beat).SetRowInBeat(b.RowInBeat);
                }
                else
                {
                    tl = tl.SetBeat(b.Beat).SetRowInBeat(b.RowInBeat);
                    br = br.SetBeat(a.Beat).SetRowInBeat(a.RowInBeat);
                }

                if (unit == SelectionUnit.Beat || unit == SelectionUnit.FullBeat)
                {
                    tl = tl.FirstRowInBeat;
                    br = br.LastRowInBeat;
                }

                if (unit == SelectionUnit.Row || unit == SelectionUnit.Beat)
                {
                    tl = tl.FirstColumn;
                    br = br.LastColumn;
                }

                return Tuple.Create(tl, br);
            }
        }

        public IEnumerable<MPEPatternColumnBeatRef> ColumnsAndBeats
        {
            get
            {
                if (!active)
                    yield break;

                var bounds = Bounds;
                var cd = bounds.Item1;

                while (true)
                {
                    var cr = new MPEPatternColumnRef(cd.ParameterColumn.PatternColumn);

                    for (int beat = bounds.Item1.Beat; beat <= bounds.Item2.Beat; beat++)
                        yield return new MPEPatternColumnBeatRef(cr, beat);

                    if (cd.ColumnSet == bounds.Item2.ColumnSet && cd.Column == bounds.Item2.Column)
                        break;

                    cd = cd.Right;
                }

            }
        }
    }
}
