using BuzzGUI.Interfaces;

namespace ReBuzz.Common.Interfaces
{
    public interface IMachineExtension : IMachine
    {
        new int InputChannelCount { get; set; }
        new int OutputChannelCount { get; set; }
    }
}
