using ReBuzz.Core;
using System.Linq;
using System.Windows.Input;

namespace ReBuzzTests.Automation.TestMachinesControllers
{
    public class DynamicMachineController(string name, string instanceName)
    {
        public string Name { get; } = name;
        public string InstanceName { get; } = instanceName;

        public ICommand Command(MachineCore instrument, string commandName, ReBuzzCore buzzCore) =>
            buzzCore.MachineManager.GetCommands(instrument).Single(c => c.Text == commandName).Command;
    }
}