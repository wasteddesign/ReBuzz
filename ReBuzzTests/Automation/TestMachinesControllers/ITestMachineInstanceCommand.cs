using Buzz.MachineInterface;
using BuzzGUI.Interfaces;
using ReBuzz.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ReBuzzTests.Automation.TestMachinesControllers
{
    public interface ITestMachineInstanceCommand //bug rethink the name
    {
        void Execute(
            ReBuzzCore buzzCore,
            Dictionary<string, MachineCore> machineCores);
    }

    public class NativeSetOutputSampleValuesCommand(DynamicMachineController controller, Sample sample, int sampleValueLeftDivisor, int sampleValueRightDivisor) : ITestMachineInstanceCommand //bug move
    {
        public void Execute(ReBuzzCore buzzCore, Dictionary<string, MachineCore> machineCores)
        {
            var machineCore = machineCores[controller.InstanceName];
            var globalParams = machineCore.ParameterGroups.Single(g => g.Type == ParameterGroupType.Global);

            globalParams.Parameters.Single(p => p.Name == "SampleValueLeftIntegral")
                .SetValue(-1, (int)sample.L);
            globalParams.Parameters.Single(p => p.Name == "SampleValueLeftDivisor")
                .SetValue(-1, sampleValueLeftDivisor);
            globalParams.Parameters.Single(p => p.Name == "SampleValueRightIntegral")
                .SetValue(-1, (int)sample.R);
            globalParams.Parameters.Single(p => p.Name == "SampleValueRightDivisor")
                .SetValue(-1, sampleValueRightDivisor);
            machineCore.Commands.Single().Command.Execute(0); //bug unnecessary
        }
    }
}