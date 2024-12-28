using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace BuzzGUI.Interfaces
{
    public interface IEnvelope : INotifyPropertyChanged
    {
        ReadOnlyCollection<Tuple<int, int>> Points { get; }
        int SustainPoint { get; }
        int PlayPosition { get; }
        bool IsEnabled { get; set; }

        void Update(IEnumerable<Tuple<int, int>> points, int sustainpoint);
    }
}
