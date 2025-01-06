using BuzzGUI.Interfaces;

namespace WDE.ModernSequenceEditorHorizontal.Actions
{
    internal class TSLInsertOrDeleteAction : IAction
    {
        BuzzGUI.SequenceEditor.TimeSignatureList tsl;
        BuzzGUI.SequenceEditor.TimeSignatureList oldtsl;

        int time;
        int span;
        bool insert;

        public TSLInsertOrDeleteAction(BuzzGUI.SequenceEditor.TimeSignatureList tsl, int t, int sp, bool ins)
        {
            this.tsl = tsl;
            oldtsl = new BuzzGUI.SequenceEditor.TimeSignatureList(tsl);
            time = t;
            span = sp;
            insert = ins;
        }

        public void Do()
        {
            if (insert)
                tsl.Insert(time, span);
            else
                tsl.Delete(time, span);
        }

        public void Undo()
        {
            tsl.Clone(oldtsl);
        }

    }
}
