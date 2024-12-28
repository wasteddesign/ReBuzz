using BuzzGUI.Common;
using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace WDE.ModernPatternEditor
{
    public class RowNumberColumnSet : ColumnRenderer.IColumnSet
    {
        int beatCount;
        public int BeatCount
        {
            get { return beatCount; }
            set
            {
                beatCount = value;
                PropertyChanged.Raise(this, "BeatCount");
            }
        }

        public string Label { get { return null; } }
        public Color Color { get { return Colors.Black; } }

        readonly RowNumberColumn[] column;
        public IList<ColumnRenderer.IColumn> Columns { get { return column; } }

        public RowNumberColumnSet(int rpb)
        {
            column = new[] { new RowNumberColumn(this, rpb) };
        }

#pragma warning disable 67
        public event Action<HashSet<int>> BeatsInvalidated;

        #region INotifyPropertyChanged Members

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
