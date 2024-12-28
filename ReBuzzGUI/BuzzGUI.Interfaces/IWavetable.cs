using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace BuzzGUI.Interfaces
{
    public interface IWavetable : INotifyPropertyChanged
    {
        ISong Song { get; }
        ReadOnlyCollection<IWave> Waves { get; }

        float Volume { get; set; }      // in decibels, default 0

        void AllocateWave(int index, string path, string name, int size, WaveFormat format, bool stereo, int root, bool add, bool wavechangedevent);
        void LoadWave(int index, string path, string name, bool add);
        void PlayWave(string path);

        event Action<int> WaveChanged;

    }
}
