using FluentAssertions;
using ReBuzz.Core;
using System.Collections.Generic;

namespace ReBuzzTests.Automation
{
    public class InstrumentCommand(DynamicGeneratorController controller, string commandName, object parameter)
    {
        public void Execute(
            ReBuzzCore buzzCore,
            Dictionary<string, MachineCore> machineCores)
        {
            machineCores.Should().ContainKey(controller.InstrumentName);
            machineCores[controller.InstrumentName].Should().NotBeNull();

            var instrument = machineCores[controller.InstrumentName];
            controller.Command(instrument, commandName, buzzCore).Execute(parameter);
        }
    }
}