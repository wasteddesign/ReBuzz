using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Controls;
using BuzzGUI.Common;

namespace BuzzGUI.WaveformControl.Commands
{
    public class GainEditCommand : NoopCommand
    {
        public GainEditCommand(WaveformVM waveformVm) : base(waveformVm)
        {
        }

        public override void Execute(object parameter)
        {
            float gain = 1;

            // popup value editor (or in menu?)
            //Knob knob;
            if (UpdateFromParam(parameter))
            {
                if (Selection.IsValid(Waveform))
                {
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


                    // get gain value
                    // apply
                    Process(parameter, (buffer, i) => buffer[i] * gain);
                }
            }
        }

    }
}
