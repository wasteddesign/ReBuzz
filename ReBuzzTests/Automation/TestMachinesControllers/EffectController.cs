using ReBuzzTests.Automation.TestMachines;
using System;

namespace ReBuzzTests.Automation.TestMachinesControllers
{
    public class EffectController(string effectName)
        : DynamicMachineController(MachineName, effectName)
    {
        private const string MachineName = "Effect";

        public static EffectController NewInstance(string effectName = MachineName) =>
            new(effectName);

        public static ITestMachineInfo Info => EffectInfo.Instance;

        public TestMachineInstanceCommand SetStereoSampleValueToInputValue()
            => ConfigureSampleTransformCommand();

        public TestMachineInstanceCommand SetStereoSampleValueToInputValueMultipliedBy(int multiplier)
            => new(this, "ConfigureSampleTransform", (float l, float r) => (l * multiplier, r * multiplier));

        private TestMachineInstanceCommand ConfigureSampleTransformCommand() 
            => new(this, "ConfigureSampleTransform", (float l, float r) => (l, r));
    }
}