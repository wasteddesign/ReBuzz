using Buzz.MachineInterface;
using FluentAssertions;
using ReBuzz.Core;
using System.Collections.Generic;

namespace ReBuzzTests.Automation
{
    public class SetStereoSampleValueCommand(DynamicGeneratorController controller, Sample s)
    {
        public void Execute(
            ReBuzzCore buzzCore,
            Dictionary<string, MachineCore> machineCores)
        {
            machineCores.Should().ContainKey(controller.InstrumentName);
            machineCores[controller.InstrumentName].Should().NotBeNull();

            var instrument = machineCores[controller.InstrumentName];
            controller.Command(instrument, "ConfigureSampleSource", buzzCore).Execute(() => (s.L, s.R)); //bug
        }
    }
}