namespace BuzzGUI.WaveformControl.Actions
{
    internal class MuteAction : WaveAction
    {
        private readonly object param;

        public MuteAction(WaveformVM waveformVm, object x) : base(waveformVm)
        {
            this.param = x;
            SaveState(x, true);
        }

        protected override void DoAction()
        {
            if (UpdateFromState())
            {
                Process(param, (buffer, i) => 0);
            }
        }

        protected override void UndoAction()
        {
            UpdateFromState();
            RestoreWave();
            RestoreSelection();
        }
    }
}
