using BuzzGUI.Common;

namespace BuzzGUI.WaveformControl.Actions
{
    internal class DeleteAction : WaveAction
    {

        public DeleteAction(WaveformVM waveformVm, object x) : base(waveformVm)
        {
            SaveState(x, true);
        }

        protected override void DoAction()
        {
            if (UpdateFromState())
            {
                if (Selection.IsValid(Waveform))
                {
                    WaveCommandHelpers.DeleteSelectionFromLayer(Wavetable, WaveformVm.SelectedSlotIndex, WaveformVm.SelectedLayerIndex, Selection.StartSample, Selection.EndSample);
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
