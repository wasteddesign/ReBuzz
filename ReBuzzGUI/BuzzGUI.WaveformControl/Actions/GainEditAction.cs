using BuzzGUI.WaveformControl.Commands;
using System.Windows;
using System.Windows.Interop;

namespace BuzzGUI.WaveformControl.Actions
{
    internal class GainEditAction : WaveAction
    {
        private readonly object param;
        readonly float gain = 1;

        public GainEditAction(WaveformVM waveformVm, object x) : base(waveformVm)
        {
            this.param = x;
            SaveState(x, true);

            // call for wavelengthwindow
            Rect rectBounds = System.Windows.Media.VisualTreeHelper.GetContentBounds(Selection.Element.cursorDrawingVisual);
            Point p = Selection.Element.cursorDrawingVisual.PointToScreen(new Point(rectBounds.Left, rectBounds.Top));
            DecibelValueEditor hw = new DecibelValueEditor()
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = p.X,
                Top = p.Y
            };
            new WindowInteropHelper(hw).Owner = ((HwndSource)PresentationSource.FromVisual(Selection.Element)).Handle;

            if (!(bool)hw.ShowDialog())
            {
                //user closed window, do nothing
                return;
            }

            // find out how much
            gain = (float)(hw.Value);
        }

        protected override void DoAction()
        {
            if (UpdateFromState())
            {
                if (Selection.IsValid(Waveform))
                {
                    // get gain value
                    // apply
                    Process(param, (buffer, i) => buffer[i] * gain);
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
