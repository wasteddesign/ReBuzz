using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.Linq;
using WDE.ModernPatternEditor;
using WDE.ModernPatternEditor.MPEStructures;

namespace BuzzGUI.Common.Actions.PatternActions
{
    public class MPESetPatternRowsPerBeatAction : PatternAction
    {
        // Just save all data
        Dictionary<int, PatternEvent[]> oldColumnEvents;
        Dictionary<int, int[]> oldBeats;

        int oldRowsPerBeat;
        int newRowsPerBeat;

        MPEPattern mpePattern;
        PatternVM patternVM;

        public MPESetPatternRowsPerBeatAction(MPEPattern pattern, PatternVM patternVM, int newRowsPerBeat)
            : base(pattern.Pattern)
        {
            mpePattern = pattern;
            this.patternVM = patternVM;
            this.newRowsPerBeat = newRowsPerBeat;
            this.oldRowsPerBeat = pattern.RowsPerBeat;
        }

        protected override void DoAction()
        {
            oldColumnEvents = new Dictionary<int, PatternEvent[]>();
            oldBeats = new Dictionary<int, int[]>();
            foreach (var c in mpePattern.MPEPatternColumns)
            {
                int index = mpePattern.MPEPatternColumns.IndexOf(c);
                var pe = c.GetEvents(0, Pattern.Length * PatternEvent.TimeBase).ToArray();
                oldColumnEvents[index] = pe;
                oldBeats[index] = c.BeatRowsList.ToArray();
            }
            mpePattern.RowsPerBeat = newRowsPerBeat;
            mpePattern.Quantize();
            patternVM.DefaultRPB = newRowsPerBeat;

            patternVM.Pattern = Pattern;

        }

        protected override void UndoAction()
        {
            mpePattern.RowsPerBeat = oldRowsPerBeat;
            patternVM.DefaultRPB = oldRowsPerBeat;
            foreach (var c in mpePattern.MPEPatternColumns)
            {
                int index = mpePattern.MPEPatternColumns.IndexOf(c);
                var pe = c.GetEvents(0, Pattern.Length * PatternEvent.TimeBase).ToArray();
                c.SetEvents(pe, false); // Clear
                c.SetEvents(oldColumnEvents[index], true);
                c.SetBeats(oldBeats[index].ToList());
            }

            patternVM.Pattern = Pattern;
        }
    }
}
