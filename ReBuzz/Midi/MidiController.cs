using System.ComponentModel;

namespace ReBuzz.Midi
{
    public enum EMidiContollerTupe
    {
        Custom,
        Play,
        Record,
        Stop,
        Forward,
        Backward
    }
    internal class MidiController : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public int Channel { get; set; }
        public int Contoller { get; set; }
        public int Value { get; internal set; }

        public EMidiContollerTupe Type { get; internal set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
