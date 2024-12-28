using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BuzzGUI.Interfaces;

namespace BuzzGUI.SequenceEditor.Actions
{
	class SetTimeSignatureAction : IAction
	{
		TimeSignatureList tsl;
		TimeSignatureList oldtsl;

		int time;
		int step;

		public SetTimeSignatureAction(TimeSignatureList tsl, int t, int st)
		{
			this.tsl = tsl;
			oldtsl = new TimeSignatureList(tsl);
			time = t;
			step = st;
		}

		public void Do()
		{
			tsl.Set(time, step);
		}

		public void Undo()
		{
			tsl.Clone(oldtsl);
		}

	}
}
