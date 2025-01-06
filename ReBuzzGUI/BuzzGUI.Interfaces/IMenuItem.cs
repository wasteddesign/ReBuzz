using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;

namespace BuzzGUI.Interfaces
{
    public interface IMenuItem : INotifyPropertyChanged
    {
        IEnumerable<IMenuItem> Children { get; }
        string Text { get; }
        int ID { get; }
        ICommand Command { get; }
        object CommandParameter { get; }
        bool IsEnabled { get; }
        bool IsSeparator { get; }
        bool IsLabel { get; }
        bool IsCheckable { get; }
        bool IsChecked { get; }
        bool IsDefault { get; }
        bool StaysOpenOnClick { get; }
        string GestureText { get; }
    }

}
