using System;
using System.ComponentModel;

namespace BuzzGUI.Interfaces
{
    public interface IMachineConnection : INotifyPropertyChanged
    {
        IMachine Source { get; }
        IMachine Destination { get; }
        int SourceChannel { get; }
        int DestinationChannel { get; }
        int Amp { get; set; }
        int Pan { get; set; }
        bool HasPan { get; }

        event Action<float[], bool, SongTime> Tap;          // fired in the GUI thread

        IEnumerable<int> SourceChannels { get; }
        IEnumerable<int> DestinationChannels { get; }

        void SetSourceChannel(int channel, bool set);
        void SetDestinationChannel(int channel, bool set);
    }
}
