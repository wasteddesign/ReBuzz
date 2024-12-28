using System;

namespace Buzz.MachineInterface
{
    public class MasterInfo
    {
        public int BeatsPerMin;     // [16..500] 	
        public int TicksPerBeat;        // [1..32]
        public int SamplesPerSec;       // usually 44100, but machines should support any rate from 11050 to 96000
        public int SamplesPerTick;      // (int)((60 * SPS) / (BPM * TPB))  
        public int PosInTick;           // [0..SamplesPerTick-1]
        public float TicksPerSec;       // (float)SPS / (float)SPT  

        public int GrooveSize;
        public int PosInGroove;     // [0..GrooveSize-1]
        public IntPtr GrooveData;       // GrooveSize floats containing relative lengths of ticks
    }
}
