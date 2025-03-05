using ReBuzz.Core;
using System.Collections.Generic;

namespace ReBuzzTests.Automation.TestMachinesControllers
{
    public interface ITestMachineInstanceCommand
    {
        void Execute(
            ReBuzzCore buzzCore,
            Dictionary<string, MachineCore> machineCores);
    }
}