using System.Collections.Generic;
using System.ComponentModel;

namespace BuzzGUI.Interfaces
{
    public interface IParameterMetadata : INotifyPropertyChanged
    {
        IEnumerable<int> GetValidParameterValues(int track);
        bool IsVisible { get; }
        double Indicator { get; }
    }
}
