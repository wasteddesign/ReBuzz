using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace BuzzGUI.Common.Templates
{
    public class Wave
    {
        [XmlAttribute]
        public int Index;

        [XmlAttribute]
        public string Name;

        [XmlAttribute]
        public int Flags;

        [XmlAttribute]
        public float Volume;

        public List<WaveLayer> Layers;

        public Wave() { }
        public Wave(IWave w)
        {
            Index = w.Index;
            Name = w.Name;
            Flags = (int)w.Flags;
            Volume = w.Volume;
            Layers = w.Layers.Select(l => new WaveLayer(l)).ToList();
        }

        public bool Match(IWave w)
        {
            // Name does not have to match
            if (Layers.Count != w.Layers.Count) return false;

            for (int i = 0; i < Layers.Count; i++)
                if (!Layers[i].Match(w.Layers[i]))
                    return false;

            return true;
        }
    }
}
