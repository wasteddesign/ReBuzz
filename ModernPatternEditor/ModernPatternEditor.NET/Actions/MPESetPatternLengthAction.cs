using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.Linq;
using WDE.ModernPatternEditor;
using WDE.ModernPatternEditor.MPEStructures;

namespace BuzzGUI.Common.Actions.PatternActions
{
    public class MPESetPatternLengthAction : PatternAction
    {
        // Just save all data
        Dictionary<int, PatternEvent[]> oldColumnEvents;
        Dictionary<int, int[]> oldBeats;

        int oldBeatCount;
        int newBeatCount;

        MPEPattern mpePattern;

        public MPESetPatternLengthAction(MPEPattern pattern, int oldBeatCount, int newBeatCount)
            : base(pattern.Pattern)
        {
            mpePattern = pattern;
            this.newBeatCount = newBeatCount;
            this.oldBeatCount = oldBeatCount;
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

            lock (mpePattern.Editor.syncLock)
            {
                mpePattern.SetBeatCount(newBeatCount);
                mpePattern.Pattern.Length = newBeatCount * PatternControl.BUZZ_TICKS_PER_BEAT;
            }
        }

        protected override void UndoAction()
        {
            mpePattern.SetBeatCount(oldBeatCount);
            foreach (var c in mpePattern.MPEPatternColumns)
            {
                int index = mpePattern.MPEPatternColumns.IndexOf(c);
                var pe = c.GetEvents(0, Pattern.Length * PatternEvent.TimeBase).ToArray();
                c.SetEvents(pe, false); // Clear
                c.SetEvents(oldColumnEvents[index], true);
                c.SetBeats(oldBeats[index].ToList());
                c.UpdateLength();
            }

            lock (mpePattern.Editor.syncLock)
            {
                mpePattern.Pattern.Length = oldBeatCount * PatternControl.BUZZ_TICKS_PER_BEAT;
            }
        }
    }
}
