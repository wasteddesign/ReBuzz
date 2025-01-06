using BuzzGUI.Common;

namespace BuzzGUI.WaveformControl.Actions
{
    internal class DCRemoveAction : WaveAction
    {
        private readonly object param;

        public DCRemoveAction(WaveformVM waveformVm, object x) : base(waveformVm)
        {
            this.param = x;
            SaveState(x, true);
        }

        protected override void DoAction()
        {
            if (UpdateFromState())
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
                    Process(param, (buffer, i) => buffer[i] - offset);
                }
                else
                {
                    offset = total / Selection.LengthInSamples;
                    roffset = rtotal / Selection.LengthInSamples;
                    Process(param, (buffer, i) => buffer[i] - offset, (buffer, i) => buffer[i] - roffset);
                }
            }
            Selection.Reset(0);
        }

        protected override void UndoAction()
        {
            UpdateFromState();
            RestoreWave();
            RestoreSelection();
        }
    }
}
