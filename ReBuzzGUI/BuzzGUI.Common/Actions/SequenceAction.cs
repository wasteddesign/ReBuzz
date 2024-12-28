using BuzzGUI.Interfaces;

namespace BuzzGUI.Common.Actions
{
    public abstract class SequenceAction : SongAction
    {
        readonly int seqIndex;
        protected ISequence Sequence
        {
            get
            {
                if (seqIndex >= Song.Sequences.Count)
                    throw new ActionException(this);
                return Song.Sequences[seqIndex];
            }
        }

        public SequenceAction(ISequence sequence)
            : base(sequence.Machine.Graph.Buzz.Song)
        {
            seqIndex = Song.Sequences.IndexOf(sequence);
        }

    }
}
