using Buzz.MachineInterface;
using ReBuzzTests.Automation.TestMachines;

namespace ReBuzzTests.Automation.TestMachinesControllers
{
    /// <summary>
    /// Controller for the fake Synth machine.
    /// </summary>
    public class SynthController(string instanceName)
        : DynamicMachineController(MachineName, instanceName)
    {
        private const string MachineName = "Synth";

        public static SynthController NewInstance(string instrumentName = MachineName) =>
            new(instrumentName);

        public static ITestMachineInfo Info => SynthInfo.Instance;

        /// <summary>
        /// Command to set the returned stereo sample value to the passes constant value.
        /// </summary>
        public ITestMachineInstanceCommand SetStereoSampleValueTo(Sample sampleToReturn)
            => new TestManagedMachineInstanceCommand(this, "ConfigureSampleSource", () => (sampleToReturn.L, sampleToReturn.R));
    }
}