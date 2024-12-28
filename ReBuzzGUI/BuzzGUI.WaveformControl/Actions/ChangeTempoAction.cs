using BuzzGUI.WavetableView;
using System.Windows.Input;

namespace BuzzGUI.WaveformControl.Actions
{
    internal class ChangeTempoAction : WaveAction
    {

        public ChangeTempoAction(WaveformVM waveformVm, object x) : base(waveformVm)
        {
            Ready = false;
            SaveState(x, true);
            InputDialogNumber inputDialogNumber = new InputDialogNumber(Effects.GetBuzzThemeResources(), "Change tempo (percents)", 0, -50, 100, 1, (decimal)0.1);
            inputDialogNumber.ShowDialog();
            if (inputDialogNumber.DialogResult.HasValue && inputDialogNumber.DialogResult.Value)
            {
                this.Ready = true;
                semiTones = (float)inputDialogNumber.GetAnswer();
                sequenceMs = (int)inputDialogNumber.numSequence.Value;
                seekWindowMs = (int)inputDialogNumber.numSeekWindow.Value;
                overlapMs = (int)inputDialogNumber.numOverlap.Value;
            }

        }

        public bool Ready { get; }

        private readonly float semiTones;
        private readonly int sequenceMs;
        private readonly int seekWindowMs;
        private readonly int overlapMs;

        protected override void DoAction()
        {
            if (UpdateFromState(false))
            {
                if (WaveformVm.Waveform != null)
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    Effects.ChangeTempo(Wavetable, selectedSlotIndex, semiTones, sequenceMs, seekWindowMs, overlapMs);
                    Mouse.OverrideCursor = null;
                    RestoreSelection();
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
