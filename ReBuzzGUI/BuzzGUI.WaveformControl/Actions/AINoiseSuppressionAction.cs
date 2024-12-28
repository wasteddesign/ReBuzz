using BuzzGUI.WavetableView;
using System.Windows.Input;
using WDE.AudioBlock;

namespace BuzzGUI.WaveformControl.Actions
{
    internal class AINoiseSuppressionAction : WaveAction
    {

        public AINoiseSuppressionAction(WaveformVM waveformVm, object x) : base(waveformVm)
        {
            Ready = false;
            SaveState(x, true);
            NoiseSuppressionWindow wnd = new NoiseSuppressionWindow();
            wnd.Resources.MergedDictionaries.Add(Effects.GetBuzzThemeResources());
            wnd.ShowDialog();

            if (wnd.DialogResult.HasValue && wnd.DialogResult.Value)
            {
                this.Ready = true;
                this.modelFilePath = wnd.SelectedModel.FilePath;
            }
        }

        public bool Ready { get; }

        private readonly string modelFilePath;

        protected override void DoAction()
        {
            if (UpdateFromState())
            {
                if (WaveformVm.Waveform != null)
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    Effects.RNNoiseRemoval(Wavetable, selectedSlotIndex, selectedLayerIndex, oldSelectionStartSample, oldSelectionEndSample, this.modelFilePath);
                    Mouse.OverrideCursor = null;
                    RestoreSelection();
                }
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
