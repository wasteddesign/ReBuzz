using Buzz.MachineInterface;
using BuzzGUI.Interfaces;
using ReBuzz.Core;
using System.Linq;

namespace ReBuzzTests.Automation.TestMachinesControllers
{
    public class NativeSetOutputSampleValuesCommand(
        DynamicMachineController controller,
        Sample sample,
        int sampleValueLeftDivisor,
        int sampleValueRightDivisor) : ITestMachineInstanceCommand
    {
        public void Execute(ReBuzzCore buzzCore, ReBuzzMachines machineCores)
        {
            var machineCore = machineCores.GetMachineAddedFromTest(controller.InstanceName);
            var globalParams = machineCore.ParameterGroups.Single(g => g.Type == ParameterGroupType.Global);

            // Native machine interface does not allow float values,
            // so as a workaround we represent the value as two integers:
            // the integral part and the divisor. For example when the integral
            // part is 1 and the divisor is 10, the value set in the fake generator is 0.1.

            globalParams.Parameters.Single(p => p.Name == "SampleValueLeftIntegral")
                .SetValue(-1, (int)sample.L);
            globalParams.Parameters.Single(p => p.Name == "SampleValueLeftDivisor")
                .SetValue(-1, sampleValueLeftDivisor);
            globalParams.Parameters.Single(p => p.Name == "SampleValueRightIntegral")
                .SetValue(-1, (int)sample.R);
            globalParams.Parameters.Single(p => p.Name == "SampleValueRightDivisor")
                .SetValue(-1, sampleValueRightDivisor);
        }
    }
}