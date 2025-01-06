using BuzzGUI.Interfaces;
using BuzzGUI.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;

namespace BuzzGUI.SequenceEditor
{
    public class PatternAssociationsList : INotifyPropertyChanged
    {
        public Dictionary<IPattern, PatternEx> PatternAssociations = new Dictionary<IPattern, PatternEx>();

        public PatternAssociationsList()
        {
            
        }

        public event PropertyChangedEventHandler PropertyChanged;

        internal void Remove(IMachine m)
        {
            PatternAssociations.Remove(k => k.Machine == m);
        }

        internal void PatternColor(IPattern pattern)
        {
            PropertyChangedEventArgsPattern e = new PropertyChangedEventArgsPattern("PatternColor", pattern);
            if (PropertyChanged != null) PropertyChanged.Invoke(this, e);
        }

        internal void RemovedPatterns(IMachine m)
        {
            PatternAssociations.Remove(k => k.Machine == m && !m.Patterns.Contains(k));
        }

        internal bool ContainsKey(IPattern cursorPattern)
        {
            return PatternAssociations.ContainsKey(cursorPattern);
        }

        internal void Add(IPattern cursorPattern, PatternEx patternEx)
        {
            PatternAssociations[cursorPattern] = patternEx;
        }

        internal void SetColorIndex(IPattern cursorPattern, int index)
        {
            PatternAssociations[cursorPattern].ColorIndex = index;
            PropertyChangedEventArgsPattern e = new PropertyChangedEventArgsPattern("PatternColor", cursorPattern, index);
            if (PropertyChanged != null) PropertyChanged.Invoke(this, e);
        }
    }
}
