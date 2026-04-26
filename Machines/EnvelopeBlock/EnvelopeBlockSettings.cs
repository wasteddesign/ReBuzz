using BuzzGUI.Common.Settings;

namespace EnvelopeBlock
{
    public enum DisplayValueTypes { Dec, Hex }
    public enum UpdateRateEnum { Tick, SubTick }
    public enum UpdateThread { Audio, UI }
    public class EnvelopeBlockSettings : Settings
    {
        [BuzzSetting(DisplayValueTypes.Dec, Description = "Show values using ")]
        public DisplayValueTypes NumeralSystem { get; set; }

        [BuzzSetting(false, Description = "Start envelope boxes unfreesed.")]
        public bool StartUnfreezed { get; set; }

        [BuzzSetting(UpdateRateEnum.SubTick, Description = "Update rate.")]
        public UpdateRateEnum UpdateRate { get; set; }
        
        [BuzzSetting(false, Description = "If true, parameter changes are not send when Buzz is recording.")]
        public bool IgnoreWhenRecording { get; set; }

        //[BuzzSetting(UpdateThread.UI, Description = "Update envelope values from audio or UI thread.")]
        //public UpdateThread UpdateThread { get; set; }
    }
}
