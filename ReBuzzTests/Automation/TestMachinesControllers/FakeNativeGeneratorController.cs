﻿using Buzz.MachineInterface;
using ReBuzzTests.Automation.TestMachines;

namespace ReBuzzTests.Automation.TestMachinesControllers
{
    public class FakeNativeGeneratorController(string instanceName)
        : DynamicMachineController(MachineName, instanceName)
    {
        private const string MachineName = "FakeNativeGenerator";

        public static FakeNativeGeneratorController NewInstance(string instrumentName = MachineName) =>
            new(instrumentName);

        public static ITestMachineInfo Info => FakeNativeGeneratorInfo.Instance;

        public ITestMachineInstanceCommand SetStereoSampleValueTo(
            Sample sample, int sampleValueLeftDivisor = 1, int sampleValueRightDivisor = 1) =>
            new NativeSetOutputSampleValuesCommand(this, sample, sampleValueLeftDivisor,
                sampleValueRightDivisor);

        public ITestMachineInstanceCommand SetDebugShow(bool isEnabled) => SetGlobalParameterCommand.DebugShowEnabled(this, isEnabled);
    }
}
