using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Windows.Input;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;

namespace BuzzGUI.WaveformControl.Commands
{
    public class DeleteEditCommand : NoopCommand
    {
        public DeleteEditCommand(WaveformVM waveformVm) : base (waveformVm)
        {
        }

        public override void Execute(object parameter)
        {
            if (UpdateFromParam(parameter))
            {
                if (Selection.IsValid(Waveform))
                {
                    WaveCommandHelpers.DeleteSelectionFromLayer(Wavetable, WaveformVm.SelectedSlotIndex, WaveformVm.SelectedLayerIndex, Selection.StartSample, Selection.EndSample);
                }
            }
            Selection.Reset(0);                                 
        }
    }
}
