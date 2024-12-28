using BuzzGUI.Common;
using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace WDE.ModernPatternEditor
{
    public class ParameterColumnSet : ColumnRenderer.IColumnSet
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

        public bool Muted { get; set; }

        public string Label { get; set; }
        public Color Color { get { return Pattern.Pattern.Machine.GetThemeColor(); } }

        public readonly PatternVM Pattern;
        readonly ParameterColumn[] columns;
        readonly int track;
        public IList<ColumnRenderer.IColumn> Columns { get { return columns; } }

        public ParameterColumnSet(PatternVM pattern, IEnumerable<IPatternColumn> ic, int track)
        {
            this.Pattern = pattern;
            this.track = track;
            columns = ic.Select(c => new ParameterColumn(this, c, track, pattern.DefaultRPB)).ToArray();
        }

        public void Release()
        {
            foreach (var c in columns) c.Release();
        }

        public void FireBeatsInvalidated(HashSet<int> beats)
        {
            if (BeatsInvalidated != null) BeatsInvalidated(beats);
        }

        public void SendCCsAtTime(int time)
        {
            foreach (var c in columns)
            {
                c.SendCCAtTime(time);
            }
            columns[0].PatternColumn.Machine.SendControlChanges();
        }

        public event Action<HashSet<int>> BeatsInvalidated;

        #region INotifyPropertyChanged Members

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
