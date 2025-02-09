using ReBuzz.Core;
using System.Linq;
using System.Windows.Input;

namespace ReBuzzTests.Automation
{
    public class DynamicGeneratorController(
        string name,
        string instrumentName) //bug move
    {
        public string Name { get; } = name;
        public string InstrumentName { get; } = instrumentName;

        public ICommand Command(MachineCore instrument, string commandName, ReBuzzCore buzzCore) => 
            buzzCore.MachineManager.GetCommands(instrument).Single(c => c.Text == commandName).Command;
    }
}