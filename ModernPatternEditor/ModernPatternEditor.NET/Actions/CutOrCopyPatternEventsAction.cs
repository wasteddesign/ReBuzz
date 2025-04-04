using BuzzGUI.Common.Actions;
using WDE.ModernPatternEditor.MPEStructures;

namespace WDE.ModernPatternEditor.Actions
{
    public class CutOrCopyPatternEventsAction : PatternAction
    {
        Selection r;
        PatternClipboard oldClipboard = new PatternClipboard();
        PatternClipboard oldEvents = new PatternClipboard();
        bool cut;
        MPEPattern mPEPattern;

        public CutOrCopyPatternEventsAction(MPEPattern pattern, Selection r, PatternClipboard clipboard, bool cut)
            : base(pattern.Pattern)
        {
            this.mPEPattern = pattern;
            this.r = r;

            this.cut = cut;

            // Copy pattern data to shared clipboard
            clipboard.Copy(mPEPattern, r);

            oldClipboard.Clone(Pattern, clipboard);

            if (cut)
            {
                oldEvents.Copy(mPEPattern, r);
            }
        }

        protected override void DoAction()
        {
            if (cut)
            {
                oldClipboard.Cut(mPEPattern, r);
            }
        }

        protected override void UndoAction()
        {
            if (cut) oldEvents.Paste(mPEPattern, r);
        }
    }
}
