using BuzzGUI.Common.Actions;
using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.Interfaces;
using WDE.ModernPatternEditor.MPEStructures;
using System.Linq;

namespace WDE.ModernPatternEditor.Actions
{
    public class ImportPatternAction : BuzzAction
    {
        XMLPattern xPattern;
        PatternEditor editor;
        IMachine machine;
        string name;

        public ImportPatternAction(PatternEditor editor, XMLPattern xPattern)
        {
            this.xPattern = xPattern;
            this.editor = editor;
            machine = editor.SelectedMachine.Machine;
        }

        protected override void DoAction()
        {
            editor.MPEPatternsDB.PatternImported(xPattern);
            if(name == null)
                name = editor.SelectedMachine.Machine.GetNewPatternName();
            machine.CreatePattern(name, xPattern.LenghtInBeats * PatternControl.BUZZ_TICKS_PER_BEAT);
        }

        protected override void UndoAction()
        {
            IPattern pattern = machine.Patterns.FirstOrDefault(x => x.Name == name);
            if (pattern != null)
                machine.DeletePattern(pattern);
        }
    }
}
