using ReBuzz.Core;

namespace ReBuzzTests.Automation.TestMachinesControllers
{
    /// <summary>
    /// A test command that is passed to the driver
    /// and driver executes it within the context it holds.
    /// </summary>
    public class TestManagedMachineInstanceCommand(DynamicMachineController controller, string commandName, object parameter) 
        : ITestMachineInstanceCommand
    {
        public void Execute(
            ReBuzzCore buzzCore,
            ReBuzzMachines machineCores)
        {
            var instrument = machineCores.GetMachineAddedFromTest(controller.InstanceName);
            controller.Command(instrument, commandName, buzzCore).Execute(parameter);
        }
    }
}