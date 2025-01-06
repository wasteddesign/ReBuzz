using BuzzGUI.Interfaces;
using System;
using System.Linq;

namespace BuzzGUI.Common.Actions.SequenceActions
{
    public class ClearAction : SequenceAction
    {
        readonly int time;
        readonly int span;
        Tuple<int, EventRef>[] events;

        public ClearAction(ISequence s, int t, int sp)
            : base(s)
        {
            time = t;
            span = sp;
        }

        protected override void DoAction()
        {
            var seq = Sequence;

            events = seq.Events.Where(e => e.Key >= time && e.Key < time + span).Select(e => Tuple.Create(e.Key, new EventRef(e.Value))).ToArray();

            seq.Clear(time, span);
        }

        protected override void UndoAction()
        {
            var seq = Sequence;

            seq.Clear(time, span);
            foreach (var e in events) e.Item2.Set(seq, e.Item1);
        }


    }
}
