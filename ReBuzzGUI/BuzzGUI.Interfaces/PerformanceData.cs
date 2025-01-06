namespace BuzzGUI.Interfaces
{
    public class BuzzPerformanceData
    {
        public long PerformanceCount;           // QueryPerformanceCounter at the time of the snapshot
        public long EnginePerformanceCount;     // total QueryPerformanceCounter time used by the audio engine
    }

    public class MachinePerformanceData
    {
        public long SampleCount;                // total samples of Work
        public long PerformanceCount;           // total QueryPerformanceCounter time
        public long CycleCount;                 // total rdtsc time
        public long MaxEngineLockTime;          // maximum global audio engine lock time in microseconds
    }
}
