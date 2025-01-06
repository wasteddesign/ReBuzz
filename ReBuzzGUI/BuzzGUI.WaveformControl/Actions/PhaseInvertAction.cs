namespace BuzzGUI.WaveformControl.Actions
{
    internal class PhaseInvertAction : WaveAction
    {
        private readonly object param;

        public PhaseInvertAction(WaveformVM waveformVm, object x) : base(waveformVm)
        {
            this.param = x;
            SaveState(x, true);
        }

        protected override void DoAction()
        {
            if (UpdateFromState())
            {
                Process(param, (buffer, i) => -buffer[i]);
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
