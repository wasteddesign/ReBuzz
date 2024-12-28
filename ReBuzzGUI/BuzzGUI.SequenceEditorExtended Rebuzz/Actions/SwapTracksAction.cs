using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BuzzGUI.Interfaces;

namespace BuzzGUI.SequenceEditor.Actions
{
	class SwapTracksAction : IAction
	{
		ISong song;
		int seqIndexA;
		int seqIndexB;

		public SwapTracksAction(ISequence a, ISequence b)
		{
			song = a.Machine.Graph.Buzz.Song;
			seqIndexA = song.Sequences.IndexOf(a);
			seqIndexB = song.Sequences.IndexOf(b);
		}

		public void Do()
		{
			if (seqIndexA >= song.Sequences.Count || seqIndexB >= song.Sequences.Count) return;
			song.SwapSequences(song.Sequences[seqIndexA], song.Sequences[seqIndexB]);

		}

		public void Undo()
		{
			Do();
		}

	}
}
