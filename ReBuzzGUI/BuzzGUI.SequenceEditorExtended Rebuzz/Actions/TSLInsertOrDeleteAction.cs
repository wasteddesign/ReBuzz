using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BuzzGUI.Interfaces;

namespace BuzzGUI.SequenceEditor.Actions
{
	class TSLInsertOrDeleteAction : IAction
	{
		TimeSignatureList tsl;
		TimeSignatureList oldtsl;

		int time;
		int span;
		bool insert;

		public TSLInsertOrDeleteAction(TimeSignatureList tsl, int t, int sp, bool ins)
		{
			this.tsl = tsl;
			oldtsl = new TimeSignatureList(tsl);
			time = t;
			span = sp;
			insert = ins;
		}

		public void Do()
		{
			if (insert)
				tsl.Insert(time, span);
			else
				tsl.Delete(time, span);
		}

		public void Undo()
		{
			tsl.Clone(oldtsl);
		}

	}
}
