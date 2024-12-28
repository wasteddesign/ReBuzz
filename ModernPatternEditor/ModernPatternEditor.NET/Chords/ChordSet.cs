using BuzzGUI.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace WDE.ModernPatternEditor.Chords
{
    public class ChordSet : INotifyPropertyChanged
    {
        [XmlAttribute]
        public string Name = "";

        public ChordMapping[] Mappings;

        [XmlIgnore]
        public string DisplayName { get { return Name; } }

        public ChordSet() { }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
