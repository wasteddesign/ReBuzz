using BuzzGUI.Common.Settings;

namespace BuzzGUI.MachineView
{
    public enum ZoomMouseGestures { MouseWheel, CtrlMouseWheel };
    public enum ShadowModes { None, Machines, All };

    public enum SignalAnalysisModes { Classic, Modern, VST }

    public class MachineViewSettings : Settings
    {
        [BuzzSetting(true)]
        public bool ChannelOnWire { get; set; }

        [BuzzSetting(0, Minimum = -8, Maximum = 8)]
        public int DefaultZoomLevel { get; set; }

        [BuzzSetting(true, Description = "Show custom machine skins for machines that provide them.")]
        public bool EnableSkins { get; set; }

        [BuzzSetting(true, Description = "Hide machine delete button when mouse cursor is not over the machine.")]
        public bool HideDeleteButton { get; set; }

        [BuzzSetting(true, Description = "Enable machine disconnection by dragging a machine to the edge of the screen.")]
        public bool ScreenEdgeDisconnect { get; set; }

        [BuzzSetting(ShadowModes.Machines, Description = "Shadow mode. Note: 'All' may use a lot of CPU.")]
        public ShadowModes Shadows { get; set; }

        [BuzzSetting(false, Description = "Use non-linear wires to improve transparency and definition.")]
        public bool ShowCurvedWires { get; set; }

        [BuzzSetting(false, Description = "Use colors to show the thread that is rendering the audio (Multithreading)")]
        public bool ShowEngineThreads { get; set; }

        [BuzzSetting(ZoomMouseGestures.MouseWheel)]
        public ZoomMouseGestures ZoomMouseGesture { get; set; }

        [BuzzSetting(SignalAnalysisModes.Modern, Description = "Signal Analysis Mode")]
        public SignalAnalysisModes SignalAnalysisMode { get; set; }

        [BuzzSetting(10, Minimum = 5, Maximum = 30, Description = "Snap to grid size")]
        public int SnapGridSize { get; set; }

    }
}
