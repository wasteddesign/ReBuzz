using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Controls;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;

namespace BuzzGUI.WaveformControl.Commands
{
    public class InsertSilenceCommand : NoopCommand
    {
        public InsertSilenceCommand(WaveformVM waveformVm) : base(waveformVm)
        {
        }

        public override void Execute(object parameter)
        {
            //DebugConsole.WriteLine("executing insert silence");
            if ((UpdateFromParam(parameter)) && (Selection.IsValid(Waveform)))
            {
                // if there is a selection, add that much silence
                int length = Selection.LengthInSamples;
                int cursorPos = Selection.StartSample;

                // create empty waveform and apply it to the old one
                if (WaveformVm.SelectedWave.Layers[WaveformVm.SelectedLayerIndex].ChannelCount == 1)
                {
                    float[] left = new float[length];
                    TemporaryWave inWave = new TemporaryWave(left, WaveformVm.SelectedWave.Layers[WaveformVm.SelectedLayerIndex].Format, WaveformVm.SelectedWave.Layers[WaveformVm.SelectedLayerIndex].SampleRate, WaveformVm.SelectedWave.Layers[WaveformVm.SelectedLayerIndex].RootNote, "", "");
                    WaveCommandHelpers.AddSelectionToLayer(Wavetable, WaveformVm.SelectedSlotIndex, WaveformVm.SelectedLayerIndex, cursorPos, inWave);
                }
                else if (WaveformVm.SelectedWave.Layers[WaveformVm.SelectedLayerIndex].ChannelCount == 2)
                {
                    float[] left = new float[length];
                    float[] right = new float[length];
                    TemporaryWave inWave = new TemporaryWave(left, right, WaveformVm.SelectedWave.Layers[WaveformVm.SelectedLayerIndex].Format, WaveformVm.SelectedWave.Layers[WaveformVm.SelectedLayerIndex].SampleRate, WaveformVm.SelectedWave.Layers[WaveformVm.SelectedLayerIndex].RootNote, "", "");
                    WaveCommandHelpers.AddSelectionToLayer(Wavetable, WaveformVm.SelectedSlotIndex, WaveformVm.SelectedLayerIndex, cursorPos, inWave);
                }
            }
            // if there is no selection, ask the user for how much silence
            else
            {
                int length = Selection.LengthInSamples;

                // call for wavelengthwindow
                Rect rectBounds = System.Windows.Media.VisualTreeHelper.GetContentBounds(Selection.Element.cursorDrawingVisual);
                Point p = Selection.Element.cursorDrawingVisual.PointToScreen(new Point(rectBounds.Left, rectBounds.Top));
                WaveLengthEditor hw = new WaveLengthEditor(0, 0, Int32.MaxValue, false)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual, Left = p.X, Top = p.Y
                };
                new WindowInteropHelper(hw).Owner = ((HwndSource)PresentationSource.FromVisual(Selection.Element)).Handle;

                if (!(bool)hw.ShowDialog())
                {
                    //user closed window, do nothing
                    return;
                }

                // find out how much
                length = hw.Value;
                int cursorPos = Selection.Element.PlayCursor.OffsetSamples;

                // create empty waveform and apply it to the old one
                if (WaveformVm.SelectedWave.Layers[WaveformVm.SelectedLayerIndex].ChannelCount == 1)
                {
                    float[] left = new float[length];
                    TemporaryWave inWave = new TemporaryWave(left, WaveformVm.SelectedWave.Layers[WaveformVm.SelectedLayerIndex].Format, WaveformVm.SelectedWave.Layers[WaveformVm.SelectedLayerIndex].SampleRate, WaveformVm.SelectedWave.Layers[WaveformVm.SelectedLayerIndex].RootNote, "", "");
                    WaveCommandHelpers.AddSelectionToLayer(Wavetable, WaveformVm.SelectedSlotIndex, WaveformVm.SelectedLayerIndex, cursorPos, inWave);
                }
                else if (WaveformVm.SelectedWave.Layers[WaveformVm.SelectedLayerIndex].ChannelCount == 2)
                {
                    float[] left = new float[length];
                    float[] right = new float[length];
                    TemporaryWave inWave = new TemporaryWave(left, right, WaveformVm.SelectedWave.Layers[WaveformVm.SelectedLayerIndex].Format, WaveformVm.SelectedWave.Layers[WaveformVm.SelectedLayerIndex].SampleRate, WaveformVm.SelectedWave.Layers[WaveformVm.SelectedLayerIndex].RootNote, "", "");
                    WaveCommandHelpers.AddSelectionToLayer(Wavetable, WaveformVm.SelectedSlotIndex, WaveformVm.SelectedLayerIndex, cursorPos, inWave);
                }
            }

        }
    }
}
