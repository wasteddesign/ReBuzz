using BuzzGUI.Interfaces;
using System;
using System.Linq;

namespace BuzzGUI.Common.Actions.MachineActions
{
    public class DeleteSequenceAction : MachineAction
    {
        readonly ISong song;
        readonly int seqIndex;
        Tuple<int, EventRef>[] events;

        public DeleteSequenceAction(ISequence s)
            : base(s.Machine)
        {
            song = s.Machine.Graph.Buzz.Song;
            seqIndex = song.Sequences.IndexOf(s);
        }

        protected override void DoAction()
        {
            if (seqIndex >= song.Sequences.Count) return;
            var seq = song.Sequences[seqIndex];

            events = seq.Events.Select(e => Tuple.Create(e.Key, new EventRef(e.Value))).ToArray();

            song.RemoveSequence(seq);
        }

        protected override void UndoAction()
        {
            var mac = Machine;
            if (mac == null) return;

            song.AddSequence(mac, seqIndex);

            var seq = song.Sequences[seqIndex];

            foreach (var e in events)
                e.Item2.Set(seq, e.Item1);

        }

    }
}
