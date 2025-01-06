namespace BuzzGUI.Interfaces
{
    public enum InstrumentType { Unknown, Generator, Effect, Control }

    public interface IInstrument
    {
        string Name { get; }            // if empty, this is actually the machine itself (~= default instrument)
        string Path { get; }
        IMachineDLL MachineDLL { get; }
        InstrumentType Type { get; }
    }
}
