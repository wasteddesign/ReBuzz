using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using BuzzGUI.WavetableView;

namespace BuzzGUI.WaveformControl.Commands
{
    public class ResampleCommand : NoopCommand
    {
        private int selectedSlot;
        private int targetFreq;

        public ResampleCommand(WaveformVM waveformVm, int targetFreq) : base(waveformVm) { this.selectedSlot = waveformVm.SelectedSlotIndex;  this.targetFreq = targetFreq; }

        public override void Execute(object parameter)
        {
            var param = parameter as Tuple<IWaveformBase, WaveformSelection>;
            var wave = param.Item1 as IWaveformBase;
            if (wave != null)
            {
                Effects.Resample(selectedSlot, (IWaveLayer)wave, targetFreq);
            }
        }
    }
}
