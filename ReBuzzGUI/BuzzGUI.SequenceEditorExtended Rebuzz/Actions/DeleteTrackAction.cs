using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BuzzGUI.Interfaces;

namespace BuzzGUI.SequenceEditor.Actions
{
	class DeleteTrackAction : IAction
	{
		ISong song;
		int seqIndex;
		string machineName;
		Tuple<int, EventRef>[] events;

		public DeleteTrackAction(ISequence s)
		{
			song = s.Machine.Graph.Buzz.Song;
			seqIndex = song.Sequences.IndexOf(s);
			machineName = s.Machine.Name;
		}

		public void Do()
		{
			if (seqIndex >= song.Sequences.Count) return;
			var seq = song.Sequences[seqIndex];

			events = seq.Events.Select(e => Tuple.Create(e.Key, new EventRef(e.Value))).ToArray();

			song.RemoveSequence(seq);
		}

		public void Undo()
		{
			var mac = song.Machines.FirstOrDefault(m => m.Name == machineName);
			if (mac == null) return;

			song.AddSequence(mac, seqIndex);

			var seq = song.Sequences[seqIndex];

			foreach (var e in events)
				e.Item2.Set(seq, e.Item1);
			
		}

	}
}
