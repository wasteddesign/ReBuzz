using BuzzGUI.Common.Actions;
using BuzzGUI.Interfaces;

namespace BuzzGUI.SequenceEditor.Actions
{
    public class SetEventAction : SequenceAction
    {
        readonly int time;
        readonly EventRef newer;
        EventRef older;
        int oldLoopEnd;
        int oldSongEnd;

        public SetEventAction(ISequence s, int t, SequenceEvent e)
            : base(s)
        {
            time = t;
            newer = new EventRef(e);
        }

        protected override void DoAction()
        {
            var seq = Sequence;

            if (seq.Events.ContainsKey(time))
                older = new EventRef(seq.Events[time]);

            newer.Set(seq, time);

            if (newer.Type == SequenceEventType.PlayPattern)
            {
                oldLoopEnd = Song.LoopEnd;
                oldSongEnd = Song.SongEnd;
                if (Song.LoopEnd < time + newer.PatternLength) Song.LoopEnd = time + newer.PatternLength;
            }

        }

        protected override void UndoAction()
        {
            var seq = Sequence;

            if (older != null)
                older.Set(seq, time);
            else
                seq.SetEvent(time, null);

            if (newer.Type == SequenceEventType.PlayPattern)
            {
                Song.LoopEnd = oldLoopEnd;
                Song.SongEnd = oldSongEnd;
            }
        }


    }
}
