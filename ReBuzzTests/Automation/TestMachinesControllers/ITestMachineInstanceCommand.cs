using Buzz.MachineInterface;
using BuzzGUI.Interfaces;
using ReBuzz.Core;
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

    public class NativeSetOutputSampleValuesCommand(DynamicMachineController controller, Sample sample) : ITestMachineInstanceCommand //bug move
    {
        public void Execute(ReBuzzCore buzzCore, Dictionary<string, MachineCore> machineCores)
        {
            var machineCore = machineCores[controller.InstanceName];
            var globalParams = machineCore.ParameterGroups.Single(g => g.Type == ParameterGroupType.Global);
            globalParams.Parameters.Single(p => p.Name == "SampleLeft").SetValue(-1, (int)sample.L); //bug
            globalParams.Parameters.Single(p => p.Name == "SampleRight").SetValue(-1, (int)sample.R); //bug
            machineCore.Commands.Single().Command.Execute(0);
        }
    }
}