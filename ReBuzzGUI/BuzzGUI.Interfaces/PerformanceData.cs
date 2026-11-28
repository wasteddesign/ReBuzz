namespace BuzzGUI.Interfaces
{
    public class BuzzPerformanceData
    {
        public long PerformanceCount;           // QueryPerformanceCounter at the time of the snapshot
        public long EnginePerformanceCount;     // total QueryPerformanceCounter time used by the audio engine
        public long MaxCallbackPerformanceCount;            // QueryPerformanceCounter time of the most loaded audio driver callback since the previous snapshot
        public long MaxCallbackDeadlinePerformanceCount;    // block period (frames / sample rate) of that callback in QueryPerformanceCounter time
        public long DeadlineMissCount;          // total audio driver callbacks that took longer than their own block period
        public long MaxWorkPerformanceCount;    // longest single machine Work call (any machine) since the previous snapshot in QueryPerformanceCounter time
    }

    public class MachinePerformanceData
    {
        public long SampleCount;                // total samples of Work
        public long PerformanceCount;           // total QueryPerformanceCounter time
        public long CycleCount;                 // total rdtsc time
        public long MaxEngineLockTime;          // maximum global audio engine lock time in microseconds
        public long AllocatedBytes;             // total managed memory allocated on the audio thread during Work

        // Total number of Work calls by duration, in WorkTimeHistogramSize buckets of
        // QueryPerformanceCounter time (see WorkTimeHistogramIndex). The counts only
        // grow, so a percentile over any period comes from the difference of two
        // readings. Snapshots share the engine's live array instead of copying it:
        // treat it as read-only. Null when the engine does not provide it.
        public long[] WorkTimeHistogram;

        public const int WorkTimeHistogramSize = 128;

        // Bucket for a duration: 0..3 ticks are exact, above that there are four
        // buckets per doubling (each about 19% to 25% wide).
        public static int WorkTimeHistogramIndex(long ticks)
        {
            if (ticks < 4) return ticks < 0 ? 0 : (int)ticks;
            int log = System.Numerics.BitOperations.Log2((ulong)ticks);
            int index = (log - 1) * 4 + (int)((ticks >> (log - 2)) & 3);
            return index < WorkTimeHistogramSize ? index : WorkTimeHistogramSize - 1;
        }

        // Shortest duration that falls in a bucket
        public static long WorkTimeHistogramLowerBound(int index)
        {
            if (index < 4) return index;
            return (long)(4 + (index & 3)) << (index / 4 - 1);
        }
    }
}
