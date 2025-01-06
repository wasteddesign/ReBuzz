using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BuzzGUI.Common;

namespace BuzzGUI.WaveformControl.Commands
{
    public class DCRemoveCommand : NoopCommand
    {
        public DCRemoveCommand(WaveformVM waveformVm) : base(waveformVm) { }

        public override void Execute(object parameter)
        {
            if (UpdateFromParam(parameter))
            {
                TemporaryWave wave = new TemporaryWave(WaveformVm.Waveform);
                float offset, total;
                float roffset, rtotal;

                total = rtotal = 0;
                for (int i = Selection.StartSample; i < Selection.EndSample; i++)
                {
                    total += wave.Left[i];
                    if (wave.ChannelCount == 2)
                    {
                        rtotal += wave.Right[i];
                    }
                }

                if (wave.ChannelCount == 1)
                {
                    offset = total / Selection.LengthInSamples;
                    Process(parameter, (buffer, i) => buffer[i] - offset);
                }
                else
                {
                    offset = total / Selection.LengthInSamples;
                    roffset = rtotal / Selection.LengthInSamples;
                    Process(parameter, (buffer, i) => buffer[i] - offset, (buffer, i) => buffer[i] - roffset);
                }

            }
        }
    }
}
