using Buzz.MachineInterface;
using ReBuzzTests.Automation.TestMachines;

namespace ReBuzzTests.Automation.TestMachinesControllers
{
    public class FakeNativeMonoGeneratorController(string instanceName)
        : DynamicMachineController(MachineName, instanceName)
    {
        private const string MachineName = "FakeNativeMonoGenerator";

        public static FakeNativeMonoGeneratorController NewInstance(string instrumentName = MachineName) =>
            new(instrumentName);

        public static ITestMachineInfo Info => FakeNativeGeneratorInfo.MonoGeneratorInstance;

        public ITestMachineInstanceCommand SetMonoSampleValueTo(float value, int divisor = 1) =>
            new NativeSetOutputSampleValuesCommand(this, new Sample(value, value), divisor, divisor);
    }
}
