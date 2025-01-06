namespace BuzzGUI.Common.Settings
{
    public class MIDISettings : Settings
    {
        [BuzzSetting(true, Description = "Route channel 1 MIDI input to the active machine.")]
        public bool MasterKeyboardMode { get; set; }

        [BuzzSetting(true, Description = "Don't send all MIDI to all machines like old Buzz used to do.")]
        public bool MIDIFiltering { get; set; }

        [BuzzSetting(false, Description = "Try to re-open MIDI input device once per second after it closes.")]
        public bool ReopenMIDIInput { get; set; }

    }
}
