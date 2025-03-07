using Buzz.MachineInterface;
using ReBuzzTests.Automation.TestMachines;

namespace ReBuzzTests.Automation.TestMachinesControllers
{
    public class FakeNativeGeneratorController(string instanceName)
        : DynamicMachineController(MachineName, instanceName)
    {
        private const string MachineName = "FakeNativeGenerator";

        public static FakeNativeGeneratorController NewInstance(string instrumentName = MachineName) =>
            new(instrumentName);

        public static FakeNativeGeneratorInfo Info => new();

        public ITestMachineInstanceCommand SetStereoSampleValueTo(
            Sample sample, int sampleValueLeftDivisor = 1, int sampleValueRightDivisor = 1)
        {
            return new NativeSetOutputSampleValuesCommand(this, sample, sampleValueLeftDivisor,
                sampleValueRightDivisor);
        }
    }
}
