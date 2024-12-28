using BuzzGUI.Interfaces;

namespace BuzzGUI.Common.Actions.SongActions
{
    public class SetMarkerAction : SongAction
    {
        readonly SongMarkers marker;
        readonly int time;
        int oldtime;

        public SetMarkerAction(ISong song, SongMarkers marker, int t)
            : base(song)
        {
            this.marker = marker;
            time = t;
        }

        protected override void DoAction()
        {
            switch (marker)
            {
                case SongMarkers.LoopStart: oldtime = Song.LoopStart; Song.LoopStart = time; break;
                case SongMarkers.LoopEnd: oldtime = Song.LoopEnd; Song.LoopEnd = time; break;
                case SongMarkers.SongEnd: oldtime = Song.SongEnd; Song.SongEnd = time; break;
            }
        }

        protected override void UndoAction()
        {
            switch (marker)
            {
                case SongMarkers.LoopStart: Song.LoopStart = oldtime; break;
                case SongMarkers.LoopEnd: Song.LoopEnd = oldtime; break;
                case SongMarkers.SongEnd: Song.SongEnd = oldtime; break;
            }
        }

    }
}
