using BuzzGUI.Interfaces;
using ReBuzz.Core;
using System.Linq;

namespace ReBuzzTests.Automation.TestMachinesControllers
{
    public class SetDebugShowCommand(DynamicMachineController controller, bool isEnabled) : ITestMachineInstanceCommand
    {
        public void Execute(ReBuzzCore buzzCore, ReBuzzMachines machineCores)
        {
            var machineCore = machineCores.GetMachineAddedFromTest(controller.InstanceName);
            var globalParams = machineCore.ParameterGroups.Single(g => g.Type == ParameterGroupType.Global);
            globalParams.Parameters.Single(p => p.Name == "DebugShowEnabled")
                .SetValue(-1, isEnabled ? 1 : 0);
        }
    }
}