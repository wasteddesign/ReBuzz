namespace BuzzGUI.WaveformControl.Actions
{
    internal class SetLoopAction : WaveAction
    {
        private readonly int oldLoopStart;
        private readonly int oldLoopEnd;

        public SetLoopAction(WaveformVM waveformVm, object x) : base(waveformVm)
        {
            SaveState(x, false);
            oldLoopStart = WaveformVm.Waveform.LoopStart;
            oldLoopEnd = WaveformVm.Waveform.LoopEnd;
        }
        protected override void DoAction()
        {
            if (UpdateFromState())
            {
                WaveformVm.Waveform.LoopStart = Selection.StartSample;
                WaveformVm.Waveform.LoopEnd = Selection.EndSample;

                WaveformVm.RaiseEditedWaveChanged();
                WaveformVm.Waveform.InvalidateData();
                RestoreSelection();
            }
        }

        protected override void UndoAction()
        {
            UpdateFromState();


            WaveformVm.Waveform.LoopStart = oldLoopStart;
            WaveformVm.Waveform.LoopEnd = oldLoopEnd;
            WaveformVm.RaiseEditedWaveChanged();
            WaveformVm.Waveform.InvalidateData();
            RestoreSelection();
        }
    }
}
