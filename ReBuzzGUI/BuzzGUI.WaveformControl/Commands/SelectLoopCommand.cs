using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BuzzGUI.WaveformControl.Commands
{
    public class SelectLoopCommand : NoopCommand
    {
        public SelectLoopCommand(WaveformVM waveformVm) : base(waveformVm) { }

        public override void Execute(object parameter)
        {
            UpdateFromParam(parameter);
            if(Selection != null)
            {
                Selection.StartSample = WaveformVm.Waveform.LoopStart;
                Selection.EndSample = WaveformVm.Waveform.LoopEnd;
            }
        }
    }
}
