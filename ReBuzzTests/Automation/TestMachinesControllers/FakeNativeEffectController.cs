﻿using ReBuzzTests.Automation.TestMachines;

namespace ReBuzzTests.Automation.TestMachinesControllers
{
    public class FakeNativeEffectController(string instanceName)
        : DynamicMachineController(MachineName, instanceName)
    {
        private const string MachineName = "FakeNativeEffect";

        public static FakeNativeEffectController NewInstance(string instrumentName = MachineName) =>
            new(instrumentName);

        public static FakeNativeEffectInfo Info => FakeNativeEffectInfo.Instance;

        public ITestMachineInstanceCommand SetStereoSampleMultipliers(
            int leftMultiplier = 1, int rightMultiplier = 1) =>
            new NativeSetSampleMultipliersCommand(this, leftMultiplier, rightMultiplier);
        public ITestMachineInstanceCommand SetStereoSampleMultiplier(int multiplier) => new NativeSetSampleMultipliersCommand(this, multiplier, multiplier);
        public ITestMachineInstanceCommand SetDebugShow(bool isEnabled) => SetGlobalParameterCommand.DebugShowEnabled(this, isEnabled);
    }
}