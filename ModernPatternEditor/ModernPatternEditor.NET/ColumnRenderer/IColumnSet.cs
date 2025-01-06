using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;

namespace WDE.ModernPatternEditor.ColumnRenderer
{
    public interface IColumnSet : INotifyPropertyChanged
    {
        int BeatCount { get; }
        string Label { get; }
        Color Color { get; }
        IList<IColumn> Columns { get; }

        event Action<HashSet<int>> BeatsInvalidated;
    }
}
