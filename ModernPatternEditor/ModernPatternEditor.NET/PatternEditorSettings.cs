using BuzzGUI.Common.Settings;
using System.Windows.Media;

namespace WDE.ModernPatternEditor
{
    public enum CursorScrollMode { Standard, Center, CenterWithMargins };
    public enum ColorNoteMode { None, Note, NoteAndOctave };

    public class PatternEditorSettings : Settings
    {
        [BuzzSetting(null, Presets = new object[]
        {
            "Consolas 13",          "FontFamily", "Consolas", "FontSize", 13, "FontWeight", "Normal", "FontClearType", "True", "FontStyle", "Normal", "FontStretch", "Normal", "TextFormattingMode", "Display", null,
            "Consolas 13 Bold",     "FontFamily", "Consolas", "FontSize", 13, "FontWeight", "Bold", "FontClearType", "True", "FontStyle", "Normal", "FontStretch", "Normal", "TextFormattingMode", "Display", null,
            "Consolas 13 Ideal",    "FontFamily", "Consolas", "FontSize", 13, "FontWeight", "Normal", "FontClearType", "True", "FontStyle", "Normal", "FontStretch", "Normal", "TextFormattingMode", "Ideal", null,
            "Consolas 15 Ideal",    "FontFamily", "Consolas", "FontSize", 15, "FontWeight", "Normal", "FontClearType", "True", "FontStyle", "Normal", "FontStretch", "Normal", "TextFormattingMode", "Ideal", null,
            "Consolas 17 Ideal",    "FontFamily", "Consolas", "FontSize", 17, "FontWeight", "Normal", "FontClearType", "True", "FontStyle", "Normal", "FontStretch", "Normal", "TextFormattingMode", "Ideal", null,
            "Consolas 18 Bold",     "FontFamily", "Consolas", "FontSize", 18, "FontWeight", "Bold", "FontClearType", "True", "FontStyle", "Normal", "FontStretch", "Normal", "TextFormattingMode", "Ideal", null,
            "Courier New 13 Bold",  "FontFamily", "Courier New", "FontSize", 13, "FontWeight", "Bold", "FontClearType", "True", "FontStyle", "Normal", "FontStretch", "Normal", "TextFormattingMode", "Display", null,
            "DOS",                  "FontFamily", "Perfect DOS VGA 437 Win", "FontSize", 16, "FontWeight", "Normal", "FontClearType", "False", "FontStyle", "Normal", "FontStretch", "Normal", "TextFormattingMode", "Display", null,
            "Fixedsys",             "FontFamily", "Fixedsys Excelsior 3.01", "FontSize", 16, "FontWeight", "Normal", "FontClearType", "False", "FontStyle", "Normal", "FontStretch", "Normal", "TextFormattingMode", "Display", null,
            "Lucida Console 13",    "FontFamily", "Lucida Console", "FontSize", 13, "FontWeight", "Normal", "FontClearType", "True", "FontStyle", "Normal", "FontStretch", "Normal", "TextFormattingMode", "Display", null,
        })]
        public object[] FontPreset { get; set; }

        [BuzzSetting(true)]
        public bool BackgroundImage { get; set; }

        [BuzzSetting(ColorNoteMode.Note)]
        public ColorNoteMode ColorNote { get; set; }

        [BuzzSetting(true)]
        public bool ColumnLabels { get; set; }

        [BuzzSetting(true)]
        public bool CursorBlinking { get; set; }

        [BuzzSetting(true)]
        public bool CursorRowHighlight { get; set; }

        [BuzzSetting(CursorScrollMode.Standard, Description = "Select CenterWithMargins for classic tracker style scrolling.")]
        public CursorScrollMode CursorScrollMode { get; set; }

        [BuzzSetting(false)]
        public bool EditStayOnRow { get; set; }

        [BuzzSetting("Consolas", Type = BuzzSettingType.FontFamily)]
        public string FontFamily { get; set; }

        [BuzzSetting(13, Minimum = 5, Maximum = 40)]
        public int FontSize { get; set; }

        [BuzzSetting("Normal", Type = BuzzSettingType.FontStyle)]
        public string FontStyle { get; set; }

        [BuzzSetting("Bold", Type = BuzzSettingType.FontWeight)]
        public string FontWeight { get; set; }

        [BuzzSetting("Normal", Type = BuzzSettingType.FontStretch)]
        public string FontStretch { get; set; }

        [BuzzSetting(true)]
        public bool FontClearType { get; set; }

        [BuzzSetting(false)]
        public bool HexRowNumbers { get; set; }

        [BuzzSetting(true)]
        public bool ParameterKnobs { get; set; }

        [BuzzSetting(true)]
        public bool TextDropShadow { get; set; }

        [BuzzSetting(TextFormattingMode.Display)]
        public TextFormattingMode TextFormattingMode { get; set; }

        //[BuzzSetting(false)]
        //public bool BuzzToolbars { get; set; }

        [BuzzSetting(false)]
        public bool FollowPlayPositioninPattern { get; set; }

        [BuzzSetting(false)]
        public bool FollowPlayingPattern { get; set; }

        [BuzzSetting(false, Description = "Save data compatible to PatternXP. Some Modern Pattern Editor data might not be saved.")]
        public bool PXPDataFormat { get; set; }

        [BuzzSetting(true, Description = "Send control changes immediately. Might not work well with older machines.")]
        public bool ForceControlChange { get; set; }

        [BuzzSetting(true, Description = "Use default tracker base octave.")]
        public bool DefaultBaseOctave { get; set; }

    }
}
