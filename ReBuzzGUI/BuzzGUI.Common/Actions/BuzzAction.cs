using BuzzGUI.Interfaces;

namespace BuzzGUI.Common.Actions
{
    public abstract class BuzzAction : IAction
    {
        public void Do()
        {
            try
            {
                DoAction();
            }
            catch (ActionException e)
            {
                Global.Buzz.DCWriteLine("{statusbar}" + e.ToString());
            }
        }

        public void Undo()
        {
            try
            {
                UndoAction();
            }
            catch (ActionException e)
            {
                Global.Buzz.DCWriteLine("{statusbar}" + e.ToString());
            }
        }

        protected abstract void DoAction();
        protected abstract void UndoAction();

    }
}
