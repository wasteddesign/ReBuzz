using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace BuzzGUI.Common.Templates
{
    public class Connection
    {
        [XmlAttribute]
        public string Source;

        [XmlAttribute]
        public string Destination;

        [XmlAttribute]
        public int SourceChannel;

        [XmlAttribute]
        public int DestinationChannel;

        [XmlAttribute]
        public int Amp;

        [XmlAttribute]
        public int Pan;

        public List<int> SourceChannels { get; set; }
        public List<int> DestinationChannels { get; set; }

        public Connection() { }
        public Connection(IMachineConnection c)
        {
            Source = c.Source.Name;
            Destination = c.Destination.Name;
            SourceChannel = c.SourceChannel;
            DestinationChannel = c.DestinationChannel;
            Amp = c.Amp;
            Pan = c.Pan;
            SourceChannels = [.. c.SourceChannels];
            DestinationChannels = [.. c.DestinationChannels];
        }
    }
}
