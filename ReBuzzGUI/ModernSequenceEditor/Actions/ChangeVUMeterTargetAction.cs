using BuzzGUI.Interfaces;

namespace WDE.ModernSequenceEditor.Actions
{
    internal class ChangeVUMeterTargetAction : IAction
    {
        IMachineConnection mc;
        IMachineConnection oldMc;
        TrackHeaderControl tc;


        public ChangeVUMeterTargetAction(TrackHeaderControl tc, IMachineConnection mc)
        {
            this.tc = tc;
            this.mc = mc;
            oldMc = tc.SelectedConnection;
        }

        public void Do()
        {
            tc.SelectedConnection = mc;
        }

        public void Undo()
        {
            tc.SelectedConnection = oldMc;
        }
    }
}
