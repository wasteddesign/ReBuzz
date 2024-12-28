using System;

namespace BuzzGUI.Interfaces
{
    [AttributeUsage(AttributeTargets.Class)]
    public class MachineGUIFactoryDecl : Attribute
    {
        public bool PreferWindowedGUI;
        public bool IsGUIResizable;
        public bool UseThemeStyles;
    }

    public interface IMachineGUIFactory
    {
        IMachineGUI CreateGUI(IMachineGUIHost host);
    }
}
