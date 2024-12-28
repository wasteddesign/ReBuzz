using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;

namespace BuzzGUI.WaveformControl.Commands
{
    public class SaveSelectionCommand : NoopCommand
    {
        public SaveSelectionCommand(WaveformVM waveformVm) : base(waveformVm)
        {
        }

        public override void Execute(object parameter)
        {
            if (UpdateFromParam(parameter))
            {
                if (Selection.IsValid(Waveform))
                {
                    int targetSlotIndex = GetNextAvailableWaveSlotIndex(WaveformVm.SelectedSlotIndex);
                    WaveCommandHelpers.CopySelectionToNewSlot(Wavetable, WaveformVm.SelectedSlotIndex, WaveformVm.SelectedLayerIndex, targetSlotIndex, 0, Selection.StartSample, Selection.EndSample, null);
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
    }
}
