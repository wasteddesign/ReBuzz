namespace BuzzGUI.Interfaces
{
    public interface IActionStack
    {
        void Do(IAction a);

        bool CanUndo { get; }
        bool CanRedo { get; }

        void Undo();
        void Redo();

        void BeginActionGroup();
        void EndActionGroup();
    }
}
