using BuzzGUI.Common;
using System.Linq;

namespace BuzzGUI.WaveformControl.Actions
{
    internal class SaveSelectionAction : WaveAction
    {
        readonly int targetSlotIndex;
        public SaveSelectionAction(WaveformVM waveformVm, object x) : base(waveformVm)
        {
            SaveState(x, false);
            targetSlotIndex = GetNextAvailableWaveSlotIndex(WaveformVm.SelectedSlotIndex);
        }

        protected override void DoAction()
        {
            if (UpdateFromState())
            {
                if (Selection.IsValid(Waveform))
                {
                    WaveCommandHelpers.CopySelectionToNewSlot(Wavetable, selectedSlotIndex, selectedLayerIndex, targetSlotIndex, 0, oldSelectionStartSample, oldSelectionEndSample, null);
                }
            }
            Selection.Reset(0);
        }

        private int GetNextAvailableWaveSlotIndex(int index)
        {
            var q = from w in Wavetable.Waves.Select((x, i) => new { Index = i, IsNull = x == null })
                    where w.Index > index && w.IsNull
                    select w.Index;
            return q.First();
        }

        protected override void UndoAction()
        {
            UpdateFromState();
            Wavetable.LoadWave(targetSlotIndex, null, null, false); // Remove
            RestoreSelection();
        }
    }
}
