using BuzzGUI.Interfaces;
using System.Linq;

namespace BuzzGUI.Common.Actions.MachineActions
{
    public class DeletePatternAction : MachineAction
    {
        readonly string name;
        readonly int length;
        readonly PatternClip data;

        public DeletePatternAction(IPattern p)
            : base(p.Machine)
        {
            this.name = p.Name;
            this.length = p.Length;
            this.data = new PatternClip(p);
        }

        protected override void DoAction()
        {
            var pat = Machine.Patterns.FirstOrDefault(p => p.Name == name);
            if (pat != null) Machine.DeletePattern(pat);
        }

        protected override void UndoAction()
        {
            Machine.CreatePattern(name, length);
            data.CopyTo(Machine.Patterns.First(p => p.Name == name));
        }


    }
}
