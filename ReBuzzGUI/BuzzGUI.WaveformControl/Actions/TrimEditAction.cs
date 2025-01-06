using BuzzGUI.Common;

namespace BuzzGUI.WaveformControl.Actions
{
    internal class TrimEditAction : WaveAction
    {
        private readonly object param;

        public TrimEditAction(WaveformVM waveformVm, object x) : base(waveformVm)
        {
            this.param = x;
            SaveState(x, true);
        }

        protected override void DoAction()
        {
            if (UpdateFromState())
            {
                if (Selection.IsValid(Waveform))
                {
                    WaveCommandHelpers.TrimSelectionFromLayer(Wavetable, WaveformVm.SelectedSlotIndex, WaveformVm.SelectedLayerIndex, Selection.StartSample, Selection.EndSample);
                }
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
