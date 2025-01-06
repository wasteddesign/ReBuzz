using BuzzGUI.Common.Settings;

namespace BuzzGUI.ParameterWindow
{
    public class ParameterWindowSettings : Settings
    {
        [BuzzSetting(false, Description = "Keyboard sends MIDI note on/offs to the machine. May conflict with custom GUIs.")]
        public bool KeyboardMIDI { get; set; }

    }
}
