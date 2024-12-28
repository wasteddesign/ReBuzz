using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace BuzzGUI.Common.Templates
{
    public class Pattern
    {
        [XmlAttribute]
        public string Name;

        [XmlAttribute]
        public int Length;

        public List<PatternColumn> Columns;

        public Pattern() { }
        public Pattern(IPattern p)
        {
            Name = p.Name;
            Length = p.Length;

            Columns = new List<PatternColumn>(p.Columns.Select(c => new PatternColumn(c)));

        }
    }
}
