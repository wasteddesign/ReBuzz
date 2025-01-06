namespace Buzz.MachineInterface
{
    public class SubTickInfo
    {
        public int SubTicksPerTick;
        public int CurrentSubTick;      // [0..SubTicksPerTick-1]
        public int SamplesPerSubTick;
        public int PosInSubTick;        // [0..SamplesPerSubTick-1]
    }
}
