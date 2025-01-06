using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BuzzGUI.Interfaces;

namespace BuzzGUI.SequenceEditor
{
	class EventRef
	{
		public SequenceEventType Type { get; private set; }
		public string PatternName { get; private set; }
		public int PatternLength { get; private set; }

		public EventRef(SequenceEvent e)
		{
			Type = e.Type;
			if (e.Type == SequenceEventType.PlayPattern)
			{
				PatternName = e.Pattern.Name;
				PatternLength = e.Pattern.Length;
			}

		}

		public void Set(ISequence seq, int time)
		{
			if (Type == SequenceEventType.PlayPattern)
			{
				var pat = seq.Machine.Patterns.FirstOrDefault(p => p.Name == PatternName);
				if (pat == null) return;
				seq.SetEvent(time, new SequenceEvent(Type, pat));
			}
			else
			{
				seq.SetEvent(time, new SequenceEvent(Type, null));
			}
		}

	}
}
