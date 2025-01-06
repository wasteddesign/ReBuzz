namespace BuzzGUI.Interfaces
{
    public class SongTime
    {
        public int CurrentTick;
        public int PosInTick;           // [0..SamplesPerTick-1]
        public int CurrentSubTick;      // [0..SubTicksPerTick-1]
        public int PosInSubTick;        // [0..SamplesPerSubTick-1]

        public int BeatsPerMin;         // [16..500] 	
        public int TicksPerBeat;        // [1..32]
        public int SamplesPerSec;       // usually 44100, but machines should support any rate from 11050 to 96000

        public int SubTicksPerTick;
        public int SamplesPerSubTick;

        public float AverageSamplesPerTick { get { return ((60.0f * SamplesPerSec) / (BeatsPerMin * TicksPerBeat)); } }
        public int CurrentSamplesPerTick { get { return (60 * SamplesPerSec) / (BeatsPerMin * TicksPerBeat); } }
        public float TicksPerSec { get { return SamplesPerSec / (float)AverageSamplesPerTick; } }

    }
}
