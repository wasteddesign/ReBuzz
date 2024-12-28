using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BuzzGUI.Interfaces;
using BuzzGUI.Common.Actions;

namespace BuzzGUI.SequenceEditor.Actions
{
	class CreatePatternAction : MachineAction
	{
		string name;
		int length;

		public CreatePatternAction(IMachine machine, string name, int length)
			: base(machine)
		{
			this.name = name;
			this.length = length;
		}

		protected override void DoAction()
		{
			Machine.CreatePattern(name, length);
		}

		protected override void UndoAction()
		{
			var pat = Machine.Patterns.FirstOrDefault(p => p.Name == name);
			if (pat != null) Machine.DeletePattern(pat);
		}


	}
}
