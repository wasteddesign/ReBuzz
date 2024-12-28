using BuzzGUI.Interfaces;
using System.Linq;

namespace BuzzGUI.Common.Actions.MachineActions
{
    public class AddSequenceAction : MachineAction
    {
        readonly ISong song;

        public AddSequenceAction(IMachine machine)
            : base(machine)
        {
            song = machine.Graph.Buzz.Song;
        }

        protected override void DoAction()
        {
            var mac = Machine;
            if (mac != null) song.AddSequence(mac, song.Sequences.Count);
        }

        protected override void UndoAction()
        {
            var seq = song.Sequences.LastOrDefault();
            if (seq != null) song.RemoveSequence(seq);
        }

    }
}
