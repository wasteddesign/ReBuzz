using BuzzGUI.Common;
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
        
        public string NoteStr { get
            {
                string ret = string.Empty;

                try
                {
                    ret = BuzzNote.ToString(BuzzNote.FromMIDINote(noteMidi));
                }
                catch { }

                return ret;
            }
        }

        int noteMidi = -1;
        public int NoteMidi { get => noteMidi; set => noteMidi = value; }

        public ReBuzzMIDIControllerType ControllerType { get; internal set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
