using System;
using System.Windows.Input;

namespace ReBuzz.MachineManagement
{
    public interface IKeyboard
    {
        bool HasModifierKeyPressed(Enum modifierKey);
    }

    internal class WindowsKeyboard : IKeyboard
    {
        public bool HasModifierKeyPressed(Enum modifierKey)
        {
            return Keyboard.Modifiers.HasFlag(modifierKey);
        }
    }
}