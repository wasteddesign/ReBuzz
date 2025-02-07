using ReBuzz.Core;
using ReBuzzTests.Automation.TestMachines;
using System.Linq;
using System.Windows.Input;

namespace ReBuzzTests.Automation
{
    public class DynamicGeneratorDefinition(string dllName, string sourceCode, string name) //bug move
    {
        public string SourceCode { get; } = sourceCode;
        public string DllName { get; } = dllName;
        public string Name { get; } = name;

        public static DynamicGeneratorDefinition Synth { get; } =
            new(
                SynthInfo.DllName,
                TestMachines.Synth.GetSourceCode(),
                TestMachines.Synth.GetMachineDecl().Name);

        public ICommand Command(MachineCore instrument, string commandName, ReBuzzCore buzzCore)
        {
            return buzzCore.MachineManager.GetCommands(instrument).Single(c => c.Text == commandName).Command;
        }
    }
}