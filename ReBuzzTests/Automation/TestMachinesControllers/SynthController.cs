using Buzz.MachineInterface;
using ReBuzzTests.Automation.TestMachines;

namespace ReBuzzTests.Automation.TestMachinesControllers
{
    public class SynthController(string instanceName)
        : DynamicMachineController(MachineName, instanceName)
    {
        private const string MachineName = "Synth";

        public static SynthController NewInstance(string instrumentName = MachineName) =>
            new(instrumentName);

        public static ITestMachineInfo Info => SynthInfo.Instance;

        public TestMachineInstanceCommand SetStereoSampleValueTo(Sample sampleToReturn)
            => new(this, "ConfigureSampleSource", () => (sampleToReturn.L, sampleToReturn.R));
    }
}