using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BuzzGUI.Interfaces;

namespace BuzzGUI.SequenceEditor.Actions
{
	class SetMarkerAction : IAction
	{
		ISong song;
		SongMarkers marker;
		int time;
		int oldtime;

		public SetMarkerAction(ISong song, SongMarkers marker, int t)
		{
			this.song = song;
			this.marker = marker;
			time = t;
		}

		public void Do()
		{
			switch (marker)
			{
				case SongMarkers.LoopStart: oldtime = song.LoopStart; song.LoopStart = time; break;
				case SongMarkers.LoopEnd: oldtime = song.LoopEnd; song.LoopEnd = time; break;
				case SongMarkers.SongEnd: oldtime = song.SongEnd; song.SongEnd = time; break;
			}
		}

		public void Undo()
		{
			switch (marker)
			{
				case SongMarkers.LoopStart: song.LoopStart = oldtime; break;
				case SongMarkers.LoopEnd: song.LoopEnd = oldtime; break;
				case SongMarkers.SongEnd: song.SongEnd = oldtime; break;
			}
		}

	}
}
