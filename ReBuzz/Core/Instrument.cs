using BuzzGUI.Interfaces;

namespace ReBuzz.Core
{
    internal class Instrument : IInstrument
    {
        public string Name { get; set; }

        public string Path { get; set; }

        public IMachineDLL MachineDLL { get; set; }

        public InstrumentType Type { get; set; }
    }
}
