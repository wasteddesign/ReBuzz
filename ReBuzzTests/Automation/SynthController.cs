using Buzz.MachineInterface;

namespace ReBuzzTests.Automation
{
    public class SynthController(string instrumentName)
        : DynamicGeneratorController("Synth", instrumentName)
    {
        public static SynthController NewInstance(string instrumentName = "Synth") =>
            new(instrumentName);

        public static DynamicGeneratorDefinition Definition { get; } = new(
            TestMachines.SynthInfo.DllName,
            TestMachines.Synth.GetSourceCode());

        public SetStereoSampleValueCommand SetStereoSampleValueTo(Sample sampleToReturn) //bug needed?
            => new(this, sampleToReturn);
    }
}