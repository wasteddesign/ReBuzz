using BuzzGUI.Common.Settings;

namespace WDE.ModernSequenceEditorHorizontal
{
    public enum PatternBoxColorModes { Disabled, Pattern };
    public enum TimelineNumberModes { Bar, Tick, Time, SMPTE24, SMPTE25, SMPTE29_97, SMPTE30, SMPTE60 };
    public enum PatternBoxLooks { Invisible, Flat, ThreeDee };
    public enum PatternBoxEventHintType { None, Detail, Note, Midi, MidiSimple };
    public enum SequenceEventCloneKey { Ctrl, Shift, Alt };
    public enum SequenceEditorMode { Integrated, ParameterWindow };
    public enum BackgroundMarkerMode { None, Line, Rectangle };
    public enum TrackHeights { Small = 40, Smaller = 60, Normal = 80, Larger = 100, Large = 120 };

    public class HorizontalSequenceEditorSettings : Settings
    {
        [BuzzSetting(true, Description = "Automatically select the pattern under the cursor.")]
        public bool AutoSelectPattern { get; set; }

        [BuzzSetting(true)]
        public bool BackgroundImage { get; set; }

        [BuzzSetting(true)]
        public bool CursorBlinking { get; set; }

        //[BuzzSetting(true)]
        //public bool HideEditor { get; set; }

        [BuzzSetting(PatternBoxLooks.ThreeDee)]
        public PatternBoxLooks PatternBoxLook { get; set; }

        [BuzzSetting(PatternBoxColorModes.Pattern, Description = "Enable automatic pattern coloring.")]
        public PatternBoxColorModes PatternBoxColors { get; set; }

        [BuzzSetting(TimelineNumberModes.Tick)]
        public TimelineNumberModes TimelineNumbers { get; set; }

        [BuzzSetting(false)]
        public bool OldStyleSetEndMarkers { get; set; }

        [BuzzSetting(PatternBoxEventHintType.Midi, Description = "Show pattern events.")]
        public PatternBoxEventHintType PatternBoxEventHint { get; set; }

        [BuzzSetting(6, Minimum = 4, Maximum = 8, Description = "Pattern event height.")]
        public int PatternEventWidth { get; set; }

        [BuzzSetting(true, Description = "Show pattern event ToolTip.")]
        public bool EventToolTip { get; set; }

        [BuzzSetting(true, Description = "Update event hints automatically when play pressed.")]
        public bool AutoUpdateEventHints { get; set; }

        [BuzzSetting(false, Description = "Ctrl//Shift + mouse left button to play pattern.")]
        public bool ClickPlayPattern { get; internal set; }

        [BuzzSetting(1, Minimum = 1, Maximum = 64, Description = "Sync to tick value.")]
        public int ClickPlayPatternSyncToTick { get; set; }

        [BuzzSetting(true)]
        public bool VUMeterLevels { get; set; }

        [BuzzSetting(SequenceEventCloneKey.Ctrl, Description = "Key to clone sequence event when drag.")]
        public SequenceEventCloneKey EventDragCloneKey { get; set; }

        [BuzzSetting(SequenceEditorMode.Integrated, Description = "Replace classic sequence editor or show in parameter editor window.")]
        public SequenceEditorMode SequenceEditorMode { get; set; }

        [BuzzSetting(BackgroundMarkerMode.None, Description = "Background markers.")]
        public BackgroundMarkerMode BackgroundMarker { get; set; }

        [BuzzSetting(true, Description = "Vertical background lines.")]
        public bool VerticalBackgroundMarker { get; set; }

        [BuzzSetting(16, Minimum = 1, Maximum = 64, Description = "Draw background line ever x beats (0 = disable)")]
        public int BGMarkerPerBeat { get; set; }

        [BuzzSetting(false, Description = "Draw background markers on top of sequences.")]
        public bool BGMarkerToForeground { get; set; }

        [BuzzSetting(16, Minimum = 1, Maximum = 64, Description = "Resize pattern snap tp tick.")]
        public int ResizeSnap { get; internal set; }

        [BuzzSetting(false, Description = "Invert scroll wheel zoom.")]
        public bool InvMouseWheelZoom { get; set; }

        [BuzzSetting(true, Description = "Pattern Name Background.")]
        public bool PatternNameBackground { get; set; }

        [BuzzSetting(TrackHeights.Normal, Description = "Track Height.")]
        public TrackHeights TrackHeight { get; set; }
    }
}
