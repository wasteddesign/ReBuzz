namespace BuzzGUI.Common.Settings
{
    public enum SequenceEditorType { ModernVertical, ModernHorizontal };
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
    }
}
