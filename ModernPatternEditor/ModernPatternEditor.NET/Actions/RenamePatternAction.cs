using BuzzGUI.Common.Actions;
using WDE.ModernPatternEditor.MPEStructures;

namespace WDE.ModernPatternEditor.Actions
{
    public class RenamePatternAction : PatternAction
    {
        MPEPattern mPEPattern;
        IGUICallbacks cb;
        string machineName;
        string oldName;
        string newName;

        public RenamePatternAction(MPEPattern pattern, IGUICallbacks cb, string name)
            : base(pattern.Pattern)
        {
            this.mPEPattern = pattern;
            this.cb = cb;
            this.oldName = Pattern.Name;
            this.newName = name;
            machineName = Pattern.Machine.Name;
        }

        protected override void DoAction()
        {
            if (cb != null)
                cb.SetPatternName(machineName, oldName, newName);
        }

        protected override void UndoAction()
        {
            if (cb != null)
                cb.SetPatternName(machineName, newName, oldName);
        }
    }
}
