using Sanford.Multimedia.Midi;
using System.ComponentModel;

namespace ReBuzz.Midi
{
    public enum ReBuzzMIDIControllerType { Play, Stop, Record, Forward, Backward, Beginning, Loop, SpeedUp, SpeedDown }
    internal class MidiController : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public int Channel { get; set; }
        public int Contoller { get; set; }
        public int Value { get; internal set; }

        public ReBuzzMIDIControllerType ControllerType { get; internal set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
