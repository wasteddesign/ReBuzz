using BuzzGUI.Interfaces;
using System.Xml.Serialization;

namespace BuzzGUI.Common.Templates
{
    public class WaveLayer
    {
        [XmlAttribute]
        public string Path;

        [XmlAttribute]
        public int RootNote;

        [XmlAttribute]
        public int SampleRate;

        [XmlAttribute]
        public int LoopStart;

        [XmlAttribute]
        public int LoopEnd;

        public WaveLayer() { }
        public WaveLayer(IWaveLayer l)
        {
            Path = l.Path;
            RootNote = l.RootNote;
            SampleRate = l.SampleRate;
            LoopStart = l.LoopStart;
            LoopEnd = l.LoopEnd;
        }

        public bool Match(IWaveLayer l)
        {
            if (Path != l.Path) return false;
            if (RootNote != l.RootNote) return false;
            if (SampleRate != l.SampleRate) return false;
            if (LoopStart != l.LoopStart) return false;
            if (LoopEnd != l.LoopEnd) return false;
            return true;
        }

    }
}
