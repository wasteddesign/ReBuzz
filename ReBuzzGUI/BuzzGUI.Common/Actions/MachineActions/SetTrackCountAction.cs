using BuzzGUI.Interfaces;

namespace BuzzGUI.Common.Actions.MachineActions
{
    public class SetTrackCountAction : MachineAction
    {
        readonly int newTrackCount;
        readonly int oldTrackCount;

        public SetTrackCountAction(IMachine machine, int count)
            : base(machine)
        {
            newTrackCount = count;
            oldTrackCount = machine.TrackCount;
        }

        protected override void DoAction()
        {
            Machine.TrackCount = newTrackCount;
        }

        protected override void UndoAction()
        {
            Machine.TrackCount = oldTrackCount;
        }

    }
}
