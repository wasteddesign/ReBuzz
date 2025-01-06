using BuzzGUI.Interfaces;
using System.ComponentModel;

namespace WDE.ModernPatternEditor
{
    public class WaveVM : INotifyPropertyChanged
    {
        public IWavetable Wavetable { get; set; }
        public int Index { get; set; }

        public string Name { get { return string.Format("{0:X2}. {1}", Index + 1, Wavetable.Waves[Index].Name); } }

        #region INotifyPropertyChanged Members

#pragma warning disable 67
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
