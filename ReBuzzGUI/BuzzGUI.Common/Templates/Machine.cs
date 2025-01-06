using BuzzGUI.Common.Presets;
using BuzzGUI.Interfaces;
using SevenZip.Compression.LZMA;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace BuzzGUI.Common.Templates
{
    public class Machine
    {
        [XmlAttribute]
        public string Name;

        [XmlAttribute]
        public float PositionX;

        [XmlAttribute]
        public float PositionY;

        [XmlAttribute]
        public int TrackCount;

        [XmlAttribute]
        public string PatternEditor;

        [XmlAttribute]
        public int OversampleFactor;

        [XmlAttribute]
        public int MIDIInputChannel;

        public Preset Preset;

        public byte[] CompressedData;

        public List<Pattern> Patterns;
        public byte[] CompressedPatternEditorData;


        [XmlIgnore]
        public byte[] Data
        {
            get { return CompressedData != null ? SevenZipHelper.Decompress(CompressedData) : null; }
            set { CompressedData = value != null ? SevenZipHelper.Compress(value) : null; }
        }

        [XmlIgnore]
        public byte[] PatternEditorData
        {
            get { return CompressedPatternEditorData != null ? SevenZipHelper.Decompress(CompressedPatternEditorData) : null; }
            set { CompressedPatternEditorData = value != null ? SevenZipHelper.Compress(value) : null; }
        }

        public Machine() { }
        public Machine(IMachine m, bool includepatterns)
        {
            Name = m.Name;
            PositionX = m.Position.Item1;
            PositionY = m.Position.Item2;

            var tpg = m.ParameterGroups.Where(pg => pg.Type == ParameterGroupType.Track).FirstOrDefault();
            if (tpg != null) TrackCount = tpg.TrackCount;

            PatternEditor = m.PatternEditorDLL != null ? m.PatternEditorDLL.Name : null;
            OversampleFactor = m.OversampleFactor;
            MIDIInputChannel = m.MIDIInputChannel;
            Preset = new Preset(m, false, false);
            Data = m.Data;

            if (includepatterns)
            {
                Patterns = new List<Pattern>(m.Patterns.Select(p => new Pattern(p)));
                if (m.PatternEditorDLL != null) PatternEditorData = m.PatternEditorData;
            }

        }

    }
}
