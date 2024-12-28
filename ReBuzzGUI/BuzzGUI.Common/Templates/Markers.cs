using BuzzGUI.Interfaces;
using System.Xml.Serialization;

namespace BuzzGUI.Common.Templates
{
    public class Markers
    {
        [XmlAttribute]
        public int LoopStart;

        [XmlAttribute]
        public int LoopEnd;

        [XmlAttribute]
        public int SongEnd;

        public Markers() { }
        public Markers(ISong song)
        {
            LoopStart = song.LoopStart;
            LoopEnd = song.LoopEnd;
            SongEnd = song.SongEnd;
        }
    }
}
