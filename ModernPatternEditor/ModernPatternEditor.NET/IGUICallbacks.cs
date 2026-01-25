using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;

namespace WDE.ModernPatternEditor
{
    public interface IGUICallbacks
    {
        void SetPatternName(string machine, string oldName, string newName);

        void ControlChange(IMachine machine, int group, int track, int param, int value);

        void SetModifiedFlag();
    }
}
