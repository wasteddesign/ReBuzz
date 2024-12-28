using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BuzzGUI.Interfaces;
using System.Windows;

namespace BuzzGUI.SequenceEditor.Actions
{
	class PasteAction : IAction
	{
		ISong song;
		int time;
		Clipboard clipboard;
		Clipboard oldEvents = new Clipboard();

		public PasteAction(ISong song, int time, Clipboard clipboard)
		{
			this.song = song;
			this.time = time;
			this.clipboard = clipboard;
		}

		public void Do()
		{
			if (!clipboard.ContainsData) return;

			oldEvents.Copy(song, new Rect(time, clipboard.FirstTrack, clipboard.Span, clipboard.RowCount));
			clipboard.Paste(song, time);
		}

		public void Undo()
		{
			oldEvents.Paste(song, time);
		}

	}
}
