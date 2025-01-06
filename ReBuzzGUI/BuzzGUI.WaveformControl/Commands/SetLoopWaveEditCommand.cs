using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using BuzzGUI.Interfaces;
using System.Reflection;
using BuzzGUI;
using BuzzGUI.WaveformControl.Commands;

namespace BuzzGUI.WaveformControl
{
    public class SetLoopWaveEditCommand : NoopCommand
	{
        public SetLoopWaveEditCommand(WaveformVM waveformVm) : base(waveformVm) { }

		public override void Execute(object parameter)
		{
			if (UpdateFromParam(parameter))
			{
				Waveform.LoopStart = Selection.StartSample;
				Waveform.LoopEnd = Selection.EndSample;
				Waveform.InvalidateData();
			}
		}

	}

	

    

}
