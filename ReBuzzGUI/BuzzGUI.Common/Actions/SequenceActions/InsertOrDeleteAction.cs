using BuzzGUI.Common.Actions;
using BuzzGUI.Interfaces;
using System;
using System.Linq;

namespace BuzzGUI.SequenceEditor.Actions
{
    public class InsertOrDeleteAction : SequenceAction
    {
        Tuple<int, EventRef>[] events;

        readonly int time;
        readonly int span;
        readonly bool insert;

        public InsertOrDeleteAction(ISequence s, int t, int sp, bool ins)
            : base(s)
        {
            time = t;
            span = sp;
            insert = ins;
        }

        protected override void DoAction()
        {
            var seq = Sequence;

            events = seq.Events.Select(e => Tuple.Create(e.Key, new EventRef(e.Value))).ToArray();

            if (insert)
                seq.Insert(time, span);
            else
                seq.Delete(time, span);
        }

        protected override void UndoAction()
        {
            var seq = Sequence;

            seq.Clear(0, int.MaxValue);
            foreach (var e in events) e.Item2.Set(seq, e.Item1);
        }

    }
}
