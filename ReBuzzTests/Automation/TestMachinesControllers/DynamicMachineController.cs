using ReBuzz.Core;
using System.Linq;
using System.Windows.Input;

namespace ReBuzzTests.Automation.TestMachinesControllers
{
    /// <summary>
    /// A superclass for test machine controllers.
    /// The controllers are something like a remote controller but for machines.
    /// They don't control the machines directly, but they provide the driver
    /// with a way to interact with them.
    /// </summary>
    public class DynamicMachineController(string name, string instanceName)
    {
        public string Name { get; } = name;
        public string InstanceName { get; } = instanceName;

        /// <summary>
        /// Used for creating commands to send to the test machines.
        /// </summary>
        public ICommand Command(MachineCore instrument, string commandName, ReBuzzCore buzzCore) =>
            buzzCore.MachineManager.GetCommands(instrument).Single(c => c.Text == commandName).Command;
    }
}