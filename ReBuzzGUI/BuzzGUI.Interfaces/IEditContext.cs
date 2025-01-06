using System.Windows.Input;

namespace BuzzGUI.Interfaces
{
    public interface IEditContext
    {
        IActionStack ActionStack { get; }
        ICommand CutCommand { get; }
        ICommand CopyCommand { get; }
        ICommand PasteCommand { get; }
    }
}
