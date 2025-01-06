using BuzzGUI.Interfaces;

namespace BuzzGUI.Common.Actions.PatternActions
{
    public class SetLengthAction : PatternAction
    {
        readonly int newLength;
        readonly int oldLength;

        public SetLengthAction(IPattern pattern, int length)
            : base(pattern)
        {
            this.newLength = length;
            this.oldLength = pattern.Length;
        }

        protected override void DoAction()
        {
            Pattern.Length = newLength;
        }

        protected override void UndoAction()
        {
            Pattern.Length = oldLength;
        }

    }
}
