using System;

namespace BuzzGUI.WaveformControl.Actions
{
    internal class NormalizeAction : WaveAction
    {
        private readonly object param;

        public NormalizeAction(WaveformVM waveformVm, object x) : base(waveformVm)
        {
            this.param = x;
            SaveState(x, true);
        }

        protected override void DoAction()
        {
            if (UpdateFromState())
            {
                Process(param, 0.0f, (max, buffer, i) => Math.Max(max, Math.Abs(buffer[i])), (max, buffer, i) => max > 0 ? buffer[i] / max : 0);
            }
        }

        protected override void UndoAction()
        {
            UpdateFromState();
            RestoreWave();
            RestoreSelection();
        }
    }
}
