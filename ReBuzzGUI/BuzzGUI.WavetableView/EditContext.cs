using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.Windows.Input;

namespace BuzzGUI.WavetableView
{
    public class EditContextWT : IEditContext
    {
        private readonly int stackDepth = 10;
        readonly WavetableVM wavetableVM;
        ManagedActionStack actionStack;// = new ManagedActionStack(stackDepth); // Limit the max amount of undo
        readonly DummyCommand dummyCommand = new DummyCommand();

        public EditContextWT(WavetableVM e)
        {
            wavetableVM = e;
            ClearActionStack();
        }

        public void ClearActionStack()
        {
            actionStack = new ManagedActionStack(stackDepth);
        }

        public IActionStack ActionStack { get { return actionStack; } }
        public ICommand CutCommand { get { return dummyCommand; } }
        public ICommand CopyCommand { get { return dummyCommand; } }
        public ICommand PasteCommand { get { return dummyCommand; } }

    }

    // No cut, copy or paste enabled
    class DummyCommand : ICommand
    {
        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return false;
        }

        public void Execute(object parameter)
        {
        }
    }
}
