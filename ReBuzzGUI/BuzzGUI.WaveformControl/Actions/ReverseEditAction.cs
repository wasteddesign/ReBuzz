namespace BuzzGUI.WaveformControl.Actions
{
    internal class ReverseEditAction : WaveAction
    {
        private readonly object param;

        public ReverseEditAction(WaveformVM waveformVm, object x) : base(waveformVm)
        {
            this.param = x;
            SaveState(x, true);
        }

        protected override void DoAction()
        {
            if (UpdateFromState())
            {
                Process(param, (buffer, i) => buffer[buffer.Length - 1 - i]);
            }
            Selection.Reset(0);
        }

        protected override void UndoAction()
        {
            UpdateFromState();
            RestoreWave();
            RestoreSelection();
        }
    }
}
