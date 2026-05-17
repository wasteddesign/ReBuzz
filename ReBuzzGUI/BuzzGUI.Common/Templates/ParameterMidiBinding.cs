using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace BuzzGUI.Common.Templates
{
    public class ParameterMidiBinding
    {
        [XmlAttribute]
        public string Machine { get; set; }
        [XmlAttribute]
        public int Group { get; set; }
        [XmlAttribute]
        public int Track { get; set; }
        [XmlAttribute]
        public int ParamIndex { get; set; }
        [XmlAttribute]
        public int MidiChannel { get; set; }
        [XmlAttribute]
        public int MidiController { get; set; }

        public ParameterMidiBinding() { }
        public ParameterMidiBinding(IMachine m, int group, int track, int paramIndex, int midiChannel, int midiController)
        {
            this.Machine = m.Name;
            this.Group = group;
            this.Track = track;
            this.ParamIndex = paramIndex;
            this.MidiChannel = midiChannel;
            this.MidiController = midiController;
        }
    }
}
