using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System.Windows.Input;

namespace WDE.ModernPatternEditor
{
    public class EditContext : IEditContext
    {
        PatternEditor editor;
        ManagedActionStack actionStack = new ManagedActionStack();
        public ManagedActionStack ManagedActionStack { get { return actionStack; } }

        public EditContext(PatternEditor e)
        {
            editor = e;
        }

        public IActionStack ActionStack { get { return actionStack; } }
        public ICommand CutCommand { get { return editor.patternControl.CutCommand; } }
        public ICommand CopyCommand { get { return editor.patternControl.CopyCommand; } }
        public ICommand PasteCommand { get { return editor.patternControl.PasteCommand; } }

    }
}
