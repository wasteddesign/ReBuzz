using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using BuzzGUI.Interfaces;

namespace BuzzGUI.WaveformControl.Commands
{
    public class ReverseEditCommand : NoopCommand
    {
        public ReverseEditCommand(WaveformVM waveformVm) : base(waveformVm)
        {
        }

        public override void Execute(object parameter)
        {
            if (UpdateFromParam(parameter))
            {
                Process(parameter, (buffer, i) => buffer[buffer.Length - 1 - i]);
            }
        }

    }
}
