using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;

namespace WDE.ModernPatternEditor
{
    public class RowNumberColumn : ColumnRenderer.IColumn
    {
        int RowsPerBeat = 4;
        const int MinWidth = 4;

        RowNumberColumnSet set;

        class Beat : ColumnRenderer.IBeat
        {
            int RowsPerBeat = 4;
            readonly ColumnRenderer.BeatRow[] rows;

            public IList<ColumnRenderer.BeatRow> Rows { get { return rows; } }

            internal Beat(int index, int rpb)
            {
                RowsPerBeat = rpb;
                rows = new ColumnRenderer.BeatRow[RowsPerBeat];

                for (int i = 0; i < rows.Length; i++)
                {
                    rows[i].Type = ColumnRenderer.BeatValueType.RowNumber;
                    rows[i].Value = index * RowsPerBeat + i;
                    rows[i].ValueString = rows[i].Value.ToString(PatternEditor.Settings.HexRowNumbers ? "X4" : "D");
                    rows[i].VisualTime = i * RowsPerBeat * PatternEvent.TimeBase;
                    rows[i].Time = i * RowsPerBeat * PatternEvent.TimeBase * Global.Buzz.TPB / RowsPerBeat;
                }
            }
        }

        public ColumnRenderer.ColumnType Type { get { return PatternEditor.Settings.HexRowNumbers ? ColumnRenderer.ColumnType.HexValue : ColumnRenderer.ColumnType.DecValue; } }
        public int DigitCount { get { return Math.Max(MinWidth, (int)Math.Log10(set.BeatCount * RowsPerBeat - 1) + 1); } }
        public bool TiedToNext { get { return false; } }
        public string Label { get { return null; } }
        public string Description { get { return null; } }
        public ColumnRenderer.IBeat FetchBeat(int index) { return new Beat(index, RowsPerBeat); }
        public RowNumberColumn(RowNumberColumnSet set, int rpb)
        {
            this.set = set;
            RowsPerBeat = rpb;
        }
    }
}
