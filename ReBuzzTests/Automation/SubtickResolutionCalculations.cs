using BuzzGUI.Common.Settings;

namespace ReBuzzTests.Automation
{
    public static class SubTickResolutionCalculations
    {
        public static int GetBoundarySampleIndex(SubTickResolution resolution)
        {
            const int samplesPerSecond = 44100;
            const int beatsPerMinute = 126;
            const int ticksPerBeat = 4;
            const int subTickSize = 260;
            const int samplesPerTick = 60 * samplesPerSecond / (beatsPerMinute * ticksPerBeat);

            var subTicksPerTick = samplesPerTick / subTickSize / (int)resolution;
            var samplesPerSubTick = samplesPerTick / subTicksPerTick;

            return samplesPerSubTick;
        }
    }
}