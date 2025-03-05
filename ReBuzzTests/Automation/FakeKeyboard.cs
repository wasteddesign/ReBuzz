using ReBuzz.MachineManagement;
using System;

namespace ReBuzzTests.Automation
{
    public class FakeKeyboard : IKeyboard
    {
        public bool HasModifierKeyPressed(Enum modifierKey)
        {
            return false;
        }
    }
}