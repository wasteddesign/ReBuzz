using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System.Windows.Input;

namespace WDE.ModernSequenceEditorHorizontal
{
    internal class EditContext : IEditContext
    {
        SequenceEditor editor;
        ManagedActionStack actionStack = new ManagedActionStack();

        public EditContext(SequenceEditor e)
        {
            editor = e;
        }

        public IActionStack ActionStack { get { return actionStack; } }
        public ICommand CutCommand { get { return editor.CutCommand; } }
        public ICommand CopyCommand { get { return editor.CopyCommand; } }
        public ICommand PasteCommand { get { return editor.PasteCommand; } }

    }
}
