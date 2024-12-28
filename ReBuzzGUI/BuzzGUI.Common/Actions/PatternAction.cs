using BuzzGUI.Interfaces;
using System.Linq;

namespace BuzzGUI.Common.Actions
{
    public abstract class PatternAction : MachineAction
    {
        readonly string patternName;

        protected IPattern Pattern
        {
            get
            {
                var p = Machine.Patterns.FirstOrDefault(x => x.Name == patternName);
                if (p == null) throw new ActionException(this);
                return p;
            }
        }

        public PatternAction(IPattern p)
            : base(p.Machine)
        {
            patternName = p.Name;
        }


    }
}
