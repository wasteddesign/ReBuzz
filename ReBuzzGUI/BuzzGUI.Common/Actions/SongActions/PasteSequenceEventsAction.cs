using BuzzGUI.Interfaces;
using System.Windows;

namespace BuzzGUI.Common.Actions.SongActions
{
    public class PasteSequenceEventsAction : SongAction
    {
        readonly int time;
        readonly SequenceClipboard clipboard;
        readonly SequenceClipboard oldEvents = new SequenceClipboard();

        public PasteSequenceEventsAction(ISong song, int time, SequenceClipboard clipboard)
            : base(song)
        {
            this.time = time;
            this.clipboard = clipboard;
        }

        protected override void DoAction()
        {
            if (!clipboard.ContainsData) return;

            oldEvents.Copy(Song, new Rect(time, clipboard.FirstTrack, clipboard.Span, clipboard.RowCount));
            clipboard.Paste(Song, time);
        }

        protected override void UndoAction()
        {
            oldEvents.Paste(Song, time);
        }

    }
}
