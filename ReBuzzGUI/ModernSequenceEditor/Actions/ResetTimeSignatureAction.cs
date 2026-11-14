using BuzzGUI.Interfaces;

namespace WDE.ModernSequenceEditorHorizontal.Actions
{
    internal class ResetTimeSignatureAction : IAction
    {
        BuzzGUI.SequenceEditor.TimeSignatureList tsl;
        BuzzGUI.SequenceEditor.TimeSignatureList oldtsl;

        int time;
        int step;

        public ResetTimeSignatureAction(BuzzGUI.SequenceEditor.TimeSignatureList tsl, int t, int st)
        {
            this.tsl = tsl;
            oldtsl = new BuzzGUI.SequenceEditor.TimeSignatureList(tsl);
            time = t;
            step = st;
        }

        public void Do()
        {
            tsl.Delete(0, int.MaxValue);
            tsl.Set(time, step);
        }

        public void Undo()
        {
            tsl.Clone(oldtsl);
        }

    }
}
