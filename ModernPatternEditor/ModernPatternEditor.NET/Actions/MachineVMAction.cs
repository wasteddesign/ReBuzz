using BuzzGUI.Common.Actions;

namespace WDE.ModernPatternEditor.Actions
{
    abstract class MachineVMAction : BuzzAction
    {
        PatternEditor editor;
        string machineName;

        protected MachineVM MachineVM
        {
            get
            {
                var m = editor.SelectedMachine;
                if (m == null) throw new ActionException(this);
                return m;
            }
        }

        public MachineVMAction(MachineVM mvm)
        {
            this.editor = mvm.Editor;
            machineName = mvm.Name;
        }
    }
}
