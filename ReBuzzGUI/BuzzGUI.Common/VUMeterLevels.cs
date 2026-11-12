using System.ComponentModel;

namespace BuzzGUI.Common
{
    /// <summary>
    /// Left and right VU meter levels (0..1) for data binding. Themes bind
    /// <c>VUMeterLevel.Item1</c> and <c>VUMeterLevel.Item2</c>, the member names of the
    /// Tuple this replaces, so existing theme XAML keeps working.
    /// </summary>
    /// <remarks>
    /// One instance lives as long as its meter and is updated in place. The old pattern,
    /// assigning a new Tuple and raising PropertyChanged for it 30 times a second, made WPF
    /// re-resolve every binding path on each update. Tuple has no change notification, so
    /// each binding detached a PropertyDescriptor value-changed listener from the old tuple
    /// and attached one to the new one, and every detach scheduled a purge of WPF's weak
    /// event table. Here the bindings attach once and only receive value changes.
    /// </remarks>
    public sealed class VUMeterLevels : INotifyPropertyChanged
    {
        static readonly PropertyChangedEventArgs item1Changed = new PropertyChangedEventArgs(nameof(Item1));
        static readonly PropertyChangedEventArgs item2Changed = new PropertyChangedEventArgs(nameof(Item2));

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Left level.</summary>
        public double Item1 { get; private set; }

        /// <summary>Right level.</summary>
        public double Item2 { get; private set; }

        /// <summary>
        /// Sets both levels, raising PropertyChanged only for a level that changed.
        /// </summary>
        public void Set(double left, double right)
        {
            if (left != Item1)
            {
                Item1 = left;
                PropertyChanged?.Invoke(this, item1Changed);
            }
            if (right != Item2)
            {
                Item2 = right;
                PropertyChanged?.Invoke(this, item2Changed);
            }
        }
    }
}
