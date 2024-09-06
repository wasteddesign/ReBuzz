using BuzzGUI.Common;
using BuzzGUI.Interfaces;

namespace ReBuzz.Core
{
    internal class AttributeCore : IAttribute
    {
        public string Name { get; set; }

        public int MinValue { get; set; }

        public int MaxValue { get; set; }

        public int DefValue { get; set; }

        int value;
        private readonly MachineCore machine;

        public int Value
        {
            get => value; set
            {
                if (this.value != value)
                {
                    this.value = value;
                    var buzz = Global.Buzz as ReBuzzCore;
                    buzz.MachineManager.AttributesChanged(machine);
                }
            }
        }

        public bool HasUserDefValue { get; set; }

        public int UserDefValue { get; set; }

        public bool UserDefValueOverridesPreset { get; set; }

        internal AttributeCore(MachineCore machine)
        {
            this.machine = machine;
        }
    }
}
