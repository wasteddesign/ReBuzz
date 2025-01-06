using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BuzzGUI.Interfaces;

namespace BuzzGUI.SequenceEditor.Actions
{
	class AddTrackAction : IAction
	{
		ISong song;
		string machineName;

		public AddTrackAction(IMachine machine)
		{
			song = machine.Graph.Buzz.Song;
			machineName = machine.Name;
		}

		public void Do()
		{
			var mac = song.Machines.FirstOrDefault(m => m.Name == machineName);
			if (mac != null) song.AddSequence(mac, song.Sequences.Count);
		}

		public void Undo()
		{
			var seq = song.Sequences.LastOrDefault();
			if (seq != null) song.RemoveSequence(seq);
		}

	}
}
