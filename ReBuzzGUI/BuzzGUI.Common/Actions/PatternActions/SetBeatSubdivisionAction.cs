using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BuzzGUI.Common.Actions.PatternActions
{
    public class SetBeatSubdivisionAction : PatternAction
    {
        readonly Tuple<PatternColumnBeatRef, int>[] newSubdiv;
        readonly int[] oldSubdiv;

        public SetBeatSubdivisionAction(IPattern pattern, IEnumerable<Tuple<PatternColumnBeatRef, int>> subdiv)
            : base(pattern)
        {
            this.newSubdiv = subdiv.ToArray();
            oldSubdiv = newSubdiv
                .Select(s => s.Item1.GetColumn(pattern).BeatSubdivision.ContainsKey(s.Item1.Beat)
                ? s.Item1.GetColumn(pattern).BeatSubdivision[s.Item1.Beat] : -1).ToArray();
        }

        protected override void DoAction()
        {
            var p = Pattern;
            newSubdiv.Run(s => s.Item1.GetColumn(p).SetBeatSubdivision(s.Item1.Beat, s.Item2));
        }

        protected override void UndoAction()
        {
            var p = Pattern;
            newSubdiv.Run((s, i) => { if (oldSubdiv[i] >= 0) s.Item1.GetColumn(p).SetBeatSubdivision(s.Item1.Beat, oldSubdiv[i]); });
        }

    }
}
