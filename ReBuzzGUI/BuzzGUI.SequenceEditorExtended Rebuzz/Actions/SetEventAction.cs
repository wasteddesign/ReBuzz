using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BuzzGUI.Interfaces;

namespace BuzzGUI.SequenceEditor.Actions
{
	class SetEventAction : IAction
	{
		ISong song;
		int time;
		int seqIndex;
		EventRef newer;
		EventRef older;
		int oldLoopEnd;
		int oldSongEnd;

		public SetEventAction(ISequence s, int t, SequenceEvent e)
		{
			time = t;
			song = s.Machine.Graph.Buzz.Song;
			seqIndex = song.Sequences.IndexOf(s);
			newer = new EventRef(e);
		}

		public void Do()
		{
			if (seqIndex >= song.Sequences.Count) return;
			var seq = song.Sequences[seqIndex];

			if (seq.Events.ContainsKey(time))
				older = new EventRef(seq.Events[time]);

			newer.Set(seq, time);

			if (newer.Type == SequenceEventType.PlayPattern)
			{
				oldLoopEnd = song.LoopEnd;
				oldSongEnd = song.SongEnd;
				if (song.LoopEnd < time + newer.PatternLength) song.LoopEnd = time + newer.PatternLength;
			}

		}

		public void Undo()
		{
			if (seqIndex >= song.Sequences.Count) return;
			var seq = song.Sequences[seqIndex];

			if (older != null)
				older.Set(seq, time);
			else
				seq.SetEvent(time, null);

			if (newer.Type == SequenceEventType.PlayPattern)
			{
				song.LoopEnd = oldLoopEnd;
				song.SongEnd = oldSongEnd;
			}
		}


	}
}
