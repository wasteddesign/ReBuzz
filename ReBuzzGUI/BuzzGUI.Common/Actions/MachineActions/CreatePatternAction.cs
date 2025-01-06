using BuzzGUI.Interfaces;
using System.Linq;

namespace BuzzGUI.Common.Actions.MachineActions
{
    public class CreatePatternAction : MachineAction
    {
        readonly string name;
        readonly string cloneName;
        readonly int length;

        public CreatePatternAction(IMachine machine, string name, int length, IPattern clone = null)
            : base(machine)
        {
            this.name = name;
            this.length = length;

            if (clone != null)
                cloneName = clone.Name;

        }

        protected override void DoAction()
        {
            if (cloneName != null)
            {
                var clone = Machine.Patterns.FirstOrDefault(p => p.Name == cloneName);
                if (clone != null) Machine.ClonePattern(name, clone);
            }
            else
            {
                Machine.CreatePattern(name, length);
            }
        }

        protected override void UndoAction()
        {
            var pat = Machine.Patterns.FirstOrDefault(p => p.Name == name);
            if (pat != null) Machine.DeletePattern(pat);
        }


    }
}
