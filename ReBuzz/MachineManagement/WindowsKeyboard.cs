using System;
using System.Windows.Input;

namespace ReBuzz.MachineManagement
{
    /// <summary>
    /// Keyboard access hidden behind an interface to allow for easier testing,
    /// because the Keyboard class requires STA thread.
    /// </summary>
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