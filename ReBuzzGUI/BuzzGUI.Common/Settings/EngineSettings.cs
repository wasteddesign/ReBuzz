namespace BuzzGUI.Common.Settings
{
    public class EngineSettings : Settings
    {
        [BuzzSetting(true, Description = "Make BPM accurate by alternating between two SamplesPerTick values.")]
        public bool AccurateBPM { get; set; }

        [BuzzSetting(true, Description = "Use an equal power panning law. Old songs may sound slightly different.")]
        public bool EqualPowerPanning { get; set; }

        [BuzzSetting(true, Description = "Enable low latency garbage collector mode while the engine is running. Only affects .NET machines.")]
        public bool LowLatencyGC { get; set; }

        [BuzzSetting(false, Description = "Compensate latency of machines by delaying other machines.")]
        public bool MachineDelayCompensation { get; set; }

        [BuzzSetting(true, Description = "Use multiple threads to render audio. Incompatible with many old machines. Use it.")]
        public bool Multithreading { get; set; }

        [BuzzSetting(true, Description = "Keep processing machines even when they are muted. Uses more CPU but makes transitions smoother.")]
        public bool ProcessMutedMachines { get; set; }

        [BuzzSetting(true, Description = "Enables new internal timing stuff. Required by many new machines.")]
        public bool SubTickTiming { get; set; }

    }
}
