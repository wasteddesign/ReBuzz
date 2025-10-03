using BuzzGUI.Interfaces;
using ReBuzz.Core;
using System.Linq;

namespace ReBuzzTests.Automation.TestMachinesControllers
{
    public class SetGlobalParameterCommand(DynamicMachineController controller, string commandName, int value) : ITestMachineInstanceCommand
    {
        public void Execute(ReBuzzCore buzzCore, ReBuzzMachines machineCores)
        {
            var machineCore = machineCores.GetMachineAddedFromTest(controller.InstanceName);
            var globalParams = machineCore.ParameterGroups.Single(g => g.Type == ParameterGroupType.Global);
            globalParams.Parameters.Single(p => p.Name == commandName)
                .SetValue(-1, value);
        }

        public static SetGlobalParameterCommand DebugShowEnabled(DynamicMachineController controller, bool isEnabled)
        {
            return new SetGlobalParameterCommand(controller, "DebugShowEnabled", isEnabled ? 1 : 0);
        }
    }

}