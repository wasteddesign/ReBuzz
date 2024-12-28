using BuzzGUI.Interfaces;
using System.Windows;

namespace BuzzGUI.Common.Actions.SongActions
{
    public class CutOrCopySequenceEventsAction : SongAction
    {
        Rect rect;
        readonly SequenceClipboard clipboard;
        readonly SequenceClipboard oldClipboard = new SequenceClipboard();
        readonly SequenceClipboard oldEvents = new SequenceClipboard();
        readonly bool cut;

        public CutOrCopySequenceEventsAction(ISong song, Rect r, SequenceClipboard clipboard, bool cut)
            : base(song)
        {
            rect = r;
            this.clipboard = clipboard;
            this.cut = cut;
        }

        protected override void DoAction()
        {
            oldClipboard.Clone(clipboard);

            if (cut)
            {
                oldEvents.Copy(Song, rect);
                clipboard.Cut(Song, rect);
            }
            else
            {
                clipboard.Copy(Song, rect);
            }
        }

        protected override void UndoAction()
        {
            if (cut) oldEvents.Paste(Song, (int)rect.Left);
            clipboard.Clone(oldClipboard);
        }

    }
}
