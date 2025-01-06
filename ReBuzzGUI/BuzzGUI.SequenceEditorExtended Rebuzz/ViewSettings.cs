using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BuzzGUI.Interfaces;
using BuzzGUI.Common;
using System.Collections.Concurrent;

namespace BuzzGUI.SequenceEditor
{
	public class ViewSettings
	{
		public ViewSettings(SequenceEditor e)
		{
			EditContext = new EditContext(e);
		}

		double tickWidth = 2;
		public double TickWidth { get { return tickWidth; } set { tickWidth = value; } }
		public int SongEnd { get; set; }
		public int TrackCount { get; set; }

		public int LastCellTime { get { return TimeSignatureList.Snap(int.MaxValue, SongEnd); } }

		public double Width { get { return TickWidth * SongEnd; } }

		public double TrackHeight { get { return 20 /*16*/; } }

		TimeSignatureList timeSignatureList = new TimeSignatureList();
		public TimeSignatureList TimeSignatureList { get { return timeSignatureList; } set { timeSignatureList = value; } }

		public IEditContext EditContext { get; set; }

		public Dictionary<IPattern, PatternEx> PatternAssociations = new Dictionary<IPattern, PatternEx>();
		//public PatternAssociationsList PatternAssociationsList = new PatternAssociationsList();

		// double tickHeight = 2;
		//public double TickHeight { get { return tickHeight; } set { tickHeight = value; } }
		// public double Height { get { return TickHeight * SongEnd; } }

		//public double TrackWidth { get { return 120; } }
	}
}
