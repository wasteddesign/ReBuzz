using BuzzGUI.Interfaces;
using ReBuzz.Core;
using System.Linq;

namespace ReBuzzTests.Automation.TestMachinesControllers
{
    public class NativeSetSampleMultipliersCommand(
        FakeNativeEffectController controller,
        int leftMultiplier,
        int rightMultiplier) : ITestMachineInstanceCommand
    {
        public void Execute(ReBuzzCore buzzCore, ReBuzzMachines machineCores)
        {
            var machineCore = machineCores.GetMachineAddedFromTest(controller.InstanceName);
            var globalParams = machineCore.ParameterGroups.Single(g => g.Type == ParameterGroupType.Global);

            globalParams.Parameters.Single(p => p.Name == "SampleValueLeftMultiplier")
                .SetValue(-1, leftMultiplier);
            globalParams.Parameters.Single(p => p.Name == "SampleValueRightMultiplier")
                .SetValue(-1, rightMultiplier);
        }
    }
}