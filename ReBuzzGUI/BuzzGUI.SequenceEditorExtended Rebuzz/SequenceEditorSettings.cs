using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BuzzGUI.Common.Settings;

namespace BuzzGUI.SequenceEditor
{
	public enum PatternBoxColorModes { Disabled, Pattern };
	public enum TimelineNumberModes { Bar, Tick };
	public enum PatternBoxLooks { Invisible, Flat, ThreeDee };

    public class SequenceEditorSettings : Settings
	{
		[BuzzSetting(true, Description="Automatically select the pattern under the cursor.")]
		public bool AutoSelectPattern { get; set; }

		[BuzzSetting(true)]
		public bool BackgroundImage { get; set; }

		[BuzzSetting(true)]
		public bool CursorBlinking { get; set; }

		[BuzzSetting(true)]
		public bool HideEditor { get; set; }

		[BuzzSetting(PatternBoxLooks.ThreeDee)]
		public PatternBoxLooks PatternBoxLook { get; set; }

		[BuzzSetting(PatternBoxColorModes.Pattern, Description="Enable automatic pattern coloring.")]
		public PatternBoxColorModes PatternBoxColors { get; set; }

		[BuzzSetting(TimelineNumberModes.Tick)]
		public TimelineNumberModes TimelineNumbers { get; set; }
        [BuzzSetting(16, Minimum = 1, Maximum = 64, Description = "Resize pattern snap tp tick.")]
        public int ResizeSnap { get; internal set; }
    }
}
