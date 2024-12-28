using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;

namespace BuzzGUI.WaveformControl.Commands
{
    public class TrimEditCommand : NoopCommand
    {
        public TrimEditCommand(WaveformVM waveformVm) : base(waveformVm)
        {
        }

        public override void Execute(object parameter)
        {
            if (UpdateFromParam(parameter))
            {
                if (Selection.IsValid(Waveform))
                {
                    WaveCommandHelpers.TrimSelectionFromLayer(Wavetable, WaveformVm.SelectedSlotIndex, WaveformVm.SelectedLayerIndex, Selection.StartSample, Selection.EndSample);
                }
            }
            Selection.Reset(0);
        }
    }
}
