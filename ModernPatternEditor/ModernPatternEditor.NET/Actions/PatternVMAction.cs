using BuzzGUI.Common.Actions;
using System.Linq;

namespace WDE.ModernPatternEditor.Actions
{
    abstract class PatternVMAction : MachineVMAction
    {
        string patternName;

        protected PatternVM PatternVM
        {
            get
            {
                var p = MachineVM.Patterns.FirstOrDefault(x => x.Name == patternName);
                if (p == null) throw new ActionException(this);
                return p;
            }
        }

        public PatternVMAction(PatternVM pvm)
            : base(pvm.MachineVM)
        {
            patternName = pvm.Name;
        }

    }
}
