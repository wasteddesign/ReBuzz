using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace ModernSequenceEditor.Interfaces
{
    public enum SequencerLayout
    {
        Horizontal,
        Vertical
    }

    public interface IModernSequencerMachineInterface
    {
        Canvas PrepareCanvasForSequencer(IPattern pat, SequencerLayout layout, double tickHeight, int pos, double width, double height);

        // Canvas PrepareCanvasForSequencer(IPattern pat, SequencerLayout layout, double tickHeight, double width, double height, IActionStack actionStack);

        event PropertyChangedEventHandler PropertyChanged;
    }

    public interface IModernSequencerMachineEditInterface
    {
        void ActionStack(IActionStack actionStack);
    }
}
