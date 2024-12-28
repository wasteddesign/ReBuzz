using System.ComponentModel;

namespace BuzzGUI.Interfaces
{
    public enum WaveFormat { Int16, Float32, Int32, Int24 };

    public interface IWaveformBase : INotifyPropertyChanged
    {
        WaveFormat Format { get; }
        int SampleCount { get; }
        int RootNote { get; set; }
        int SampleRate { get; set; }
        int LoopStart { get; set; }
        int LoopEnd { get; set; }

        int ChannelCount { get; }

        void GetDataAsFloat(float[] output, int outoffset, int outstride, int channel, int offset, int count);
        void SetDataAsFloat(float[] input, int inoffset, int instride, int channel, int offset, int count);

        void InvalidateData();
    }
}
