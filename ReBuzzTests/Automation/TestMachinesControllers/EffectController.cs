using ReBuzzTests.Automation.TestMachines;

namespace ReBuzzTests.Automation.TestMachinesControllers
{
    /// <summary>
    /// A controller for the fake Effect machine.
    /// </summary>
    public class EffectController(string instanceName)
        : DynamicMachineController(MachineName, instanceName)
    {
        private const string MachineName = "Effect";

        public static EffectController NewInstance(string effectName = MachineName) =>
            new(effectName);

        public static ITestMachineInfo Info => EffectInfo.Instance;

        /// <summary>
        /// Command to make the effect copy the input sample to the output.
        /// </summary>
        public TestMachineInstanceCommand SetStereoSampleValueToInputValue()
            => new(this, "ConfigureSampleTransform", (float l, float r) => (l, r));

        /// <summary>
        /// Command to make the effect copy the multiplied input sample to the output.
        /// </summary>
        public TestMachineInstanceCommand SetStereoSampleValueToInputValueMultipliedBy(int multiplier)
            => new(this, "ConfigureSampleTransform", (float l, float r) => (l * multiplier, r * multiplier));
    }
}