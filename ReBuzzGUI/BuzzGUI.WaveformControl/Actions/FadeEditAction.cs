namespace BuzzGUI.WaveformControl.Actions
{
    internal class FadeEditAction : WaveAction
    {
        public enum FadeType
        {
            LinOut,
            LinIn
        }

        private readonly FadeType fadeType;
        private readonly object param;

        public FadeEditAction(WaveformVM waveformVm, FadeType fadeType, object x) : base(waveformVm)
        {
            this.param = x;
            SaveState(x, true);
            this.fadeType = fadeType;
        }

        protected override void DoAction()
        {
            if (UpdateFromState())
            {
                if (fadeType == FadeType.LinIn)
                {
                    Process(param, (buffer, i) => buffer[i] * ((float)i / buffer.Length));
                }
                else
                {
                    Process(param, (buffer, i) => buffer[i] * (1.0f - (float)i / buffer.Length));
                }
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
