using Buzz.MachineInterface;
using ReBuzzTests.Automation.TestMachines;

namespace ReBuzzTests.Automation.TestMachinesControllers
{
    public class EffectController(string effectName) //bug move
        : DynamicGeneratorController("Effect", effectName) //bug clean up the literal strings
    {
        public static EffectController NewInstance(string effectName = "Effect") =>
            new(effectName);

        public static ITestMachineInfo Info => EffectInfo.Instance;

        public TestMachineInstanceCommand SetStereoSampleValueTo(Sample sampleToReturn)
            => new(this, "ConfigureSampleSource", (float l, float r) => (sampleToReturn.L, sampleToReturn.R));

        public TestMachineInstanceCommand SetStereoSampleValueToInputValue()
            => new(this, "ConfigureSampleSource", (float l, float r) => (l, r));

        public TestMachineInstanceCommand SetStereoSampleValueToInputValueMultipliedBy(float multiplier)
            => new(this, "ConfigureSampleSource", (float l, float r) => (l * multiplier, r * multiplier));
    }
}