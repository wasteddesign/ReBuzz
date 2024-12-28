using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BuzzGUI.Interfaces;

namespace BuzzGUI.WaveformControl.Commands
{
    public class MuteCommand : NoopCommand
    {
        public MuteCommand(WaveformVM waveformVm) : base(waveformVm)
        {
        }

        public override void Execute(object parameter)
        {
            if (UpdateFromParam(parameter))
            {
                Process(parameter, (buffer, i) => 0);
            }
        }
    }
}
