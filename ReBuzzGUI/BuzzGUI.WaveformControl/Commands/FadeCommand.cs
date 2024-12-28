using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using BuzzGUI.Interfaces;

namespace BuzzGUI.WaveformControl.Commands
{
    public class FadeEditCommand : NoopCommand
    {
        public enum FadeType
        {
            LinOut,
            LinIn
        }
        FadeType fadeType;
        public FadeEditCommand(WaveformVM waveformVm, FadeType fadeType) : base(waveformVm)
        {
            this.fadeType = fadeType;
        }

        public override void Execute(object parameter)
        {
            if (UpdateFromParam(parameter))
            {
                if (fadeType == FadeEditCommand.FadeType.LinIn)
                {
                    Process(parameter, (buffer, i) => buffer[i] * ((float)i / buffer.Length));
                }
                else
                {
                    Process(parameter, (buffer, i) => buffer[i] * (1.0f - (float)i / buffer.Length));
                }
            }
        }

    }
}
