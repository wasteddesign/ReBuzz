using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace BuzzGUI.Common.InterfaceExtensions
{
    public static class PatternColumnExtensions
    {
        public static IEnumerable<PatternEvent> GetGridMIDINoteOnEvents(this IPatternColumn pc, int columncount, int step, IEnumerable<int> notelist)
        {
            return pc.GetEvents(0, columncount * step).Where(e => e.Time % step == 0 && MIDI.IsNoteOn(e.Value, notelist));
        }

        public static void RemapValues(this IPatternColumn pc, IDictionary<int, int> map)
        {
            var oldvalues = pc.GetEvents(int.MinValue, int.MaxValue).ToArray();
            pc.SetEvents(oldvalues, false);
            pc.SetEvents(oldvalues.Select(e => new PatternEvent(e.Time, map.ContainsKey(e.Value) ? map[e.Value] : e.Value, e.Duration)), true);
        }

        public static IEnumerable<int> GetRowTimes(this IPatternColumn pc, int beatIndex, int subdivision)
        {
            return Enumerable.Range(0, subdivision).Select(t => beatIndex * PatternEvent.TimeBase * 4 + PatternEvent.TimeBase * 4 * t / subdivision);
        }

        public static IEnumerable<int> GetRowTimes(this IPatternColumn pc, int beatIndex)
        {
            int subdiv;
            if (!pc.BeatSubdivision.TryGetValue(beatIndex, out subdiv))
                subdiv = 4;

            return pc.GetRowTimes(beatIndex, subdiv);
        }

    }
}
