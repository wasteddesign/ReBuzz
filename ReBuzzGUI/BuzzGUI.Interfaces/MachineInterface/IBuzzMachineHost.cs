using BuzzGUI.Interfaces;

namespace Buzz.MachineInterface
{
    public interface IBuzzMachineHost
    {
        IMachine Machine { get; }

        // MasterInfo and SubTickInfo are only valid in Work and parameter setters
        // you should always check for samplerate changes in Work
        MasterInfo MasterInfo { get; }
        SubTickInfo SubTickInfo { get; }

        int MsToSamples(float t);

        int OutputChannelCount { get; set; }
        int InputChannelCount { get; set; }
    }
}
