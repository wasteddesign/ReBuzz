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

        public static IDynamicTestMachineInfo Info => EffectInfo.Instance;

        /// <summary>
        /// Command to make the effect copy the input sample to the output.
        /// </summary>
        public ITestMachineInstanceCommand SetStereoSampleValueToInputValue()
            => SetStereoSampleValueToInputValueMultipliedBy(1);

        /// <summary>
        /// Command to make the effect copy the multiplied input sample to the output.
        /// </summary>
        public ITestMachineInstanceCommand SetStereoSampleValueToInputValueMultipliedBy(int multiplier)
            => new TestManagedMachineInstanceCommand(this, "ConfigureSampleTransform",
                (float l, float r) => (l * multiplier, r * multiplier));
    }
}