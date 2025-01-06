using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BuzzGUI.Interfaces;

namespace BuzzGUI.SequenceEditor.Actions
{
	class InsertOrDeleteAction : IAction
	{
		ISong song;
		int seqIndex;
		Tuple<int, EventRef>[] events;

		int time;
		int span;
		bool insert;

		public InsertOrDeleteAction(ISequence s, int t, int sp, bool ins)
		{
			song = s.Machine.Graph.Buzz.Song;
			seqIndex = song.Sequences.IndexOf(s);
			time = t;
			span = sp;
			insert = ins;
		}

		public void Do()
		{
			if (seqIndex >= song.Sequences.Count) return;
			var seq = song.Sequences[seqIndex];

			events = seq.Events.Select(e => Tuple.Create(e.Key, new EventRef(e.Value))).ToArray();

			if (insert)
				seq.Insert(time, span);
			else
				seq.Delete(time, span);
		}

		public void Undo()
		{
			if (seqIndex >= song.Sequences.Count) return;
			var seq = song.Sequences[seqIndex];

			seq.Clear(0, int.MaxValue);
			foreach (var e in events) e.Item2.Set(seq, e.Item1);
		}

	}
}
