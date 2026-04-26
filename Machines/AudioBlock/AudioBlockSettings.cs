using BuzzGUI.Common.Settings;

namespace WDE.AudioBlock
{
    public enum SnapTo { None, Tick, Beat }
    public enum WaveViewLayout { Vertical, Horizontal }
    public class AudioBlockSettings : Settings
    {
        [BuzzSetting(WaveViewLayout.Vertical, Description = "Audio Wave View Layout.")]
        public WaveViewLayout WaveViewLayout { get; set; }

        [BuzzSetting(false, Description = "Drag & Drop: Overwrite Sample in Wavetable.")]
        public bool OverwriteSample { get; set; }

        [BuzzSetting(false, Description = "Auto Add Sequence.")]
        public bool AutoAddSequence { get; set; }

        [BuzzSetting(false, Description = "Auto Delete Sequence.")]
        public bool AutoDeleteSequence { get; set; }

        [BuzzSetting(false, Description = "Auto-resample to Buzz sample rate.")]
        public bool AutoResample { get; set; }

        [BuzzSetting(true, Description = "Resample to Buzz sample rate in real time.")]
        public bool RealTimeResampler { get; set; }

        [BuzzSetting(SnapTo.None, Description = "Snap to...")]
        public SnapTo SnapTo { get; set; }

        [BuzzSetting(200, Description = "Default Wave Width.", Maximum = 400, Minimum = 50)]
        public int DefaultWaveWidth { get; set; }

        [BuzzSetting(8, Description = "Pattern Lenght to Tick", Maximum = 32, Minimum = 1)]
        public int PatternLenghtToTick { get; set; }

        [BuzzSetting(true, Description = "Show Envelope Limits.")]
        public bool ShowEnvelopeLimits { get; set; }

        [BuzzSetting(12, Maximum = 15, Minimum = 1, Description = "Wave Graphics accuracy. Higher number more accurate, lower faster to draw.")]
        public int WaveGraphDetail { get; set; }

        [BuzzSetting(false, Description = "Start envelope boxes unfreesed.")]
        public bool StartUnfreezed { get; set; }

        [BuzzSetting(false, Description = "Invert scroll wheel zoom.")]
        public bool InvMouseWheelZoom { get; set; }
    }
}
