using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BuzzGUI.Interfaces;

namespace BuzzGUI.SequenceEditor.Actions
{
	class ClearAction : IAction
	{
		ISong song;
		int time;
		int span;
		int seqIndex;
		Tuple<int, EventRef>[] events;

		public ClearAction(ISequence s, int t, int sp)
		{
			time = t;
			span = sp;
			song = s.Machine.Graph.Buzz.Song;
			seqIndex = song.Sequences.IndexOf(s);
		}

		public void Do()
		{
			if (seqIndex >= song.Sequences.Count) return;
			var seq = song.Sequences[seqIndex];

			events = seq.Events.Where(e => e.Key >= time && e.Key < time + span).Select(e => Tuple.Create(e.Key, new EventRef(e.Value))).ToArray();

			seq.Clear(time, span);
		}

		public void Undo()
		{
			if (seqIndex >= song.Sequences.Count) return;
			var seq = song.Sequences[seqIndex];

			seq.Clear(time, span);
			foreach (var e in events) e.Item2.Set(seq, e.Item1);
		}


	}
}
