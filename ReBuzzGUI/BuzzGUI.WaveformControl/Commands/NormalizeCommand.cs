using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using BuzzGUI.Interfaces;

namespace BuzzGUI.WaveformControl.Commands
{
    public class NormalizeEditCommand : NoopCommand
    {
        public NormalizeEditCommand(WaveformVM waveformVm) : base(waveformVm)
        {
        }

        public override void Execute(object parameter)
        {
            if (UpdateFromParam(parameter))
            {
                Process(parameter, 0.0f, (max, buffer, i) => Math.Max(max, Math.Abs(buffer[i])), (max, buffer, i) => max > 0 ? buffer[i] / max : 0);
            }
        }

    }
}
