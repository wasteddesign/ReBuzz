using BuzzGUI.WavetableView;

namespace BuzzGUI.WaveformControl.Actions
{
    internal class ResampleAction : WaveAction
    {
        private readonly int targetFreq;

        public ResampleAction(WaveformVM waveformVm, object x, int targetFreq) : base(waveformVm)
        {
            this.targetFreq = targetFreq;
            SaveState(x, true);
        }

        protected override void DoAction()
        {
            if (UpdateFromState(false))
            {
                if (WaveformVm.Waveform != null)
                {
                    Effects.Resample(selectedSlotIndex, WaveformVm.Waveform, targetFreq);
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
