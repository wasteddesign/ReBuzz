namespace BuzzGUI.Common.Settings
{
    public enum SequenceEditorType { ModernVertical, ModernHorizontal };
    public enum SongLoadWaitTime { Seconds_10 = 10, Seconds_30 = 30, Minutes_1 = 60, Minutes_5 = 5*60 };

    public enum DpiScalingType { System = -1, Scale_100 = 100, Scale_125 = 125, Scale_150 = 150, Scale_175 = 175,
        Scale_200 = 200, Scale_225 = 225, Scale_250 = 250, Scale_275 = 275, Scale_300 = 300,
        Scale_325 = 325, Scale_350 = 350, Scale_375 = 375, Scale_400 = 400 };
    public class GeneralSettings : Settings
    {
        [BuzzSetting(true, Description = "Rename previous version to <songname>.backup when saving a song.")]
        public bool AutomaticBackups { get; set; }

        [BuzzSetting(true, Description = "Check jeskola.net for updates at startup.")]
        public bool CheckForUpdates { get; set; }

        [BuzzSetting(true, Description = "Use TextFormattingMode.Ideal for UI text rendering. Usually looks better if Windows text scaling is set to >100%. Restart required.")]
        public bool WPFIdealFontMetrics { get; set; }

        [BuzzSetting(false, Description = "Disable hardware accelerated (GPU) graphics rendering. Not recommended unless your GPU/drivers suck. Restart required.")]
        public bool WPFSoftwareRendering { get; set; }

        [BuzzSetting(false, Description = "Always rescan plugins on start (ignore plugin cache).")]
        public bool AlwaysRescanPlugins { get; set; }

        [BuzzSetting(SequenceEditorType.ModernVertical, Description = "Sequence View Type.")]
        public SequenceEditorType SequenceView { get; set; }

        [BuzzSetting(SongLoadWaitTime.Seconds_30, Description = "Maximum time to wait a song to load. If the song is not loaded within this time user can choose further actions.")]
        public SongLoadWaitTime SongLoadWait { get; set; }

        //[BuzzSetting(true, Description = "Use multiple threads to initialize machines when loading a song.")]
        //public bool MultithreadSongLoading { get; set; }

        [BuzzSetting(4, Minimum = 2, Maximum = 6, Description = "Default machine base octave.")]
        public int DefaultMachineBaseOctave { get; set; }

        [BuzzSetting(16, Minimum = 1, Maximum = 512, Description = "Default Pattern Length.")]
        public int PatternLength { get; internal set; }

        [BuzzSetting(false, Description = "Make PianoKeyboard topmost.")]
        public bool PianoKeyboardTopmost { get; internal set; }

        [BuzzSetting(DpiScalingType.System, Description = "DPI scaling.")]
        public DpiScalingType DpiScaling { get; internal set; }
    }
}
