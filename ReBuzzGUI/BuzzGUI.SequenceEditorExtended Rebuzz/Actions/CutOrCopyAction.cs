using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BuzzGUI.Interfaces;
using System.Windows;

namespace BuzzGUI.SequenceEditor.Actions
{
	class CutOrCopyAction : IAction
	{
		ISong song;
		Rect rect;
		Clipboard clipboard;
		Clipboard oldClipboard = new Clipboard();
		Clipboard oldEvents = new Clipboard();
		bool cut;

		public CutOrCopyAction(ISong song, Rect r, Clipboard clipboard, bool cut)
		{
			this.song = song;
			rect = r;
			this.clipboard = clipboard;
			this.cut = cut;
		}

		public void Do()
		{
			oldClipboard.Clone(clipboard);

			if (cut)
			{
				oldEvents.Copy(song, rect);
				clipboard.Cut(song, rect);
			}
			else
			{
				clipboard.Copy(song, rect);
			}
		}

		public void Undo()
		{
			if (cut) oldEvents.Paste(song, (int)rect.Left);
			clipboard.Clone(oldClipboard);
		}

	}
}
