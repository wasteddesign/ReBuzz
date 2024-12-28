namespace BuzzGUI.WaveformControl.Actions
{
    internal class SelectLoopAction : WaveAction
    {
        public SelectLoopAction(WaveformVM waveformVm, object x) : base(waveformVm)
        {
            SaveState(x, false);
        }
        protected override void DoAction()
        {
            UpdateFromState();
            if (Selection != null)
            {
                Selection.StartSample = WaveformVm.Waveform.LoopStart;
                Selection.EndSample = WaveformVm.Waveform.LoopEnd;
            }
        }

        protected override void UndoAction()
        {
            UpdateFromState();

            if (Selection != null)
            {
                Selection.StartSample = oldSelectionStartSample;
                Selection.EndSample = oldSelectionEndSample;
            }
        }
    }
}
