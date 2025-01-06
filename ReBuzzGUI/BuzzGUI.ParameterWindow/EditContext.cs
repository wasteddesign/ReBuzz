using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System.Windows.Input;

namespace BuzzGUI.ParameterWindow
{
    public class EditContext : IEditContext
    {
        readonly ParameterWindowVM window;
        readonly ManagedActionStack actionStack = new ManagedActionStack();
        public ManagedActionStack ManagedActionStack { get { return actionStack; } }

        public EditContext(ParameterWindowVM window)
        {
            this.window = window;
        }

        public IActionStack ActionStack { get { return actionStack; } }
        public ICommand CutCommand { get { return null; } }
        public ICommand CopyCommand { get { return window.CopyPresetCommand; } }
        public ICommand PasteCommand { get { return window.PastePresetCommand; } }

    }
}
