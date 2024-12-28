using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using BuzzGUI.Interfaces;
using BuzzGUI.Common;

namespace BuzzGUI.SequenceEditor
{
	class EditContext : IEditContext
	{
		SequenceEditor editor;
		ManagedActionStack actionStack = new ManagedActionStack();

		public EditContext(SequenceEditor e)
		{
			editor = e;
		}

		public IActionStack ActionStack { get { return actionStack; } }
		public ICommand CutCommand { get { return editor.CutCommand; } }
		public ICommand CopyCommand	{ get { return editor.CopyCommand; } }
		public ICommand PasteCommand { get { return editor.PasteCommand; } }

	}
}
