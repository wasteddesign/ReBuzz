using Buzz.MachineInterface;
using ReBuzzTests.Automation.TestMachines;

namespace ReBuzzTests.Automation.TestMachinesControllers
{
    public class FakeNativeGeneratorStereoController(string instanceName)
        : DynamicMachineController(MachineName, instanceName)
    {
        private const string MachineName = "FakeNativeStereoGenerator";

        public static FakeNativeGeneratorStereoController NewInstance(string instrumentName = MachineName) =>
            new(instrumentName);

        public static ITestMachineInfo Info => FakeNativeGeneratorInfo.StereoGeneratorInstance;

        public ITestMachineInstanceCommand SetStereoSampleValueTo(
            Sample sample, int sampleValueLeftDivisor = 1, int sampleValueRightDivisor = 1) =>
            new NativeSetOutputSampleValuesCommand(this, sample, sampleValueLeftDivisor,
                sampleValueRightDivisor);

        public ITestMachineInstanceCommand SetDebugShow(bool isEnabled) => SetGlobalParameterCommand.DebugShowEnabled(this, isEnabled);
    }
}
