using BuzzGUI.Interfaces;

namespace WDE.ModernSequenceEditorHorizontal.Actions
{
    internal class SetTimeSignatureAction : IAction
    {
        BuzzGUI.SequenceEditor.TimeSignatureList tsl;
        BuzzGUI.SequenceEditor.TimeSignatureList oldtsl;

        int time;
        int step;

        public SetTimeSignatureAction(BuzzGUI.SequenceEditor.TimeSignatureList tsl, int t, int st)
        {
            this.tsl = tsl;
            oldtsl = new BuzzGUI.SequenceEditor.TimeSignatureList(tsl);
            time = t;
            step = st;
        }

        public void Do()
        {
            tsl.Set(time, step);
        }

        public void Undo()
        {
            tsl.Clone(oldtsl);
        }

    }
}
