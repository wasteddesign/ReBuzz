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

        // Fired synchronously on the audio engine thread that processed the source
        // machine, not the GUI thread. The array and SongTime are reused by the host:
        // read them during the call and copy anything you need to keep.
        event Action<float[], bool, SongTime> Tap;

        IEnumerable<int> SourceChannels { get; }
        IEnumerable<int> DestinationChannels { get; }

        void SetSourceChannel(int channel, bool set);
        void SetDestinationChannel(int channel, bool set);
    }
}
