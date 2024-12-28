using System;

namespace BuzzGUI.Interfaces
{
    public interface IWaveLayer : IWaveformBase
    {
        string Path { get; }

        /// <summary>
        /// Should only be used by Work functions of managed machines. The actual format depends on IWaveformBase.Format and IWaveformBase.ChannelCount. Note that the pointer may change between Work calls.
        /// </summary>
        IntPtr RawSamples { get; }
    }
}
