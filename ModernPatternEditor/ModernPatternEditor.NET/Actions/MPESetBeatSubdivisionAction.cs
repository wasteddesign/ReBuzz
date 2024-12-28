using System;
using System.Collections.Generic;
using System.Linq;
using WDE.ModernPatternEditor;
using WDE.ModernPatternEditor.MPEStructures;

namespace BuzzGUI.Common.Actions.PatternActions
{
    public class MPESetBeatSubdivisionAction : PatternAction
    {
        Tuple<MPEPatternColumnBeatRef, int>[] newSubdiv;
        int[] oldSubdiv;
        MPEPattern mpePattern;

        public MPESetBeatSubdivisionAction(MPEPattern pattern, IEnumerable<Tuple<MPEPatternColumnBeatRef, int>> subdiv)
            : base(pattern.Pattern)
        {
            mpePattern = pattern;
            this.newSubdiv = subdiv.ToArray();
            oldSubdiv = newSubdiv
                .Select(s => s.Item1.GetColumn(mpePattern).BeatSubdivision.ContainsKey(s.Item1.Beat)
                ? s.Item1.GetColumn(mpePattern).BeatSubdivision[s.Item1.Beat] : mpePattern.RowsPerBeat).ToArray();
        }

        protected override void DoAction()
        {
            foreach (var subdiv in newSubdiv)
            {
                var mpeColumn = mpePattern.GetColumn(subdiv.Item1.GetColumn(mpePattern));
                mpeColumn.BeatRowsList[subdiv.Item1.Beat] = subdiv.Item2;
            }
            var p = Pattern;
            newSubdiv.Run(s => s.Item1.GetColumn(mpePattern).SetBeatSubdivision(s.Item1.Beat, s.Item2));

            mpePattern.Editor.patternControl.MoveCursorDelta(0, 0);
        }

        protected override void UndoAction()
        {
            var p = Pattern;
            newSubdiv.Run((s, i) =>
            {
                if (oldSubdiv[i] >= 0)
                {
                    s.Item1.GetColumn(mpePattern).SetBeatSubdivision(s.Item1.Beat, oldSubdiv[i]);
                    var mpeColumn = mpePattern.GetColumn(s.Item1.GetColumn(mpePattern));
                    mpeColumn.BeatRowsList[s.Item1.Beat] = oldSubdiv[i];
                }
            });

            mpePattern.Editor.patternControl.MoveCursorDelta(0, 0);
        }

    }
}
