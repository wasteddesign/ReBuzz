using BuzzGUI.Interfaces;

namespace BuzzGUI.Common.Actions
{
    public abstract class SongAction : BuzzAction
    {
        protected ISong Song { get; private set; }

        public SongAction(ISong song)
        {
            this.Song = song;
        }

    }
}
