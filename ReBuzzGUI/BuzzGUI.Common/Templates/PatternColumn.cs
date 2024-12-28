using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace BuzzGUI.Common.Templates
{
    public class PatternColumn
    {
        [XmlAttribute]
        public string Machine;

        [XmlAttribute]
        public string Parameter;

        [XmlAttribute]
        public long[] Events;

        [XmlIgnore]
        public IEnumerable<PatternEvent> EnumerableEvents
        {
            get
            {
                if (Events == null)
                    return Enumerable.Empty<PatternEvent>();
                else
                    return Events.SelectFromTwo((t, v) => new PatternEvent((int)t, (int)(v & 0xffffffff), (int)(v >> 32)));
            }
        }

        public PatternColumn() { }
        public PatternColumn(IPatternColumn c)
        {
            if (c.Machine != null) Machine = c.Machine.Name;
            if (c.Parameter != null)
            {
                Parameter = c.Parameter.Name;
            }

            var ev = c.GetEvents(int.MinValue, int.MaxValue).SelectTwo(e => e.Time, e => e.Value + ((long)e.Duration << 32));
            if (ev.Any()) Events = ev.ToArray();

        }



    }
}
