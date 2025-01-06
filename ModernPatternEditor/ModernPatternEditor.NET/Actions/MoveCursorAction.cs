namespace WDE.ModernPatternEditor.Actions
{
    class MoveCursorAction : PatternVMAction
    {
        Digit oldPosition;
        Digit newPosition;

        public MoveCursorAction(PatternVM pvm, Digit p)
            : base(pvm)
        {
            // store PatternVM-independent digits
            oldPosition = pvm.CursorPosition.SetPatternVM(null);
            newPosition = p.SetPatternVM(null);
        }

        protected override void DoAction()
        {
            PatternVM.CursorPosition = newPosition.SetPatternVM(PatternVM);
        }

        protected override void UndoAction()
        {
            PatternVM.CursorPosition = oldPosition.SetPatternVM(PatternVM);
        }
    }
}
