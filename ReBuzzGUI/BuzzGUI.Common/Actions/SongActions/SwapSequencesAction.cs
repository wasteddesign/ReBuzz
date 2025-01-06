using BuzzGUI.Interfaces;

namespace BuzzGUI.Common.Actions.SongActions
{
    public class SwapSequencesAction : SongAction
    {
        readonly int seqIndexA;
        readonly int seqIndexB;

        public SwapSequencesAction(ISequence a, ISequence b)
            : base(a.Machine.Graph.Buzz.Song)
        {
            seqIndexA = Song.Sequences.IndexOf(a);
            seqIndexB = Song.Sequences.IndexOf(b);
        }

        protected override void DoAction()
        {
            if (seqIndexA >= Song.Sequences.Count || seqIndexB >= Song.Sequences.Count) return;
            Song.SwapSequences(Song.Sequences[seqIndexA], Song.Sequences[seqIndexB]);

        }

        protected override void UndoAction()
        {
            Do();
        }

    }
}
