using BuzzGUI.Common.Actions;
using WDE.ModernPatternEditor.MPEStructures;

namespace WDE.ModernPatternEditor.Actions
{
    public class CutOrCopyPatternEventsAction : PatternAction
    {
        Selection r;
        PatternClipboard clipboard;
        PatternClipboard oldClipboard = new PatternClipboard();
        PatternClipboard oldEvents = new PatternClipboard();
        bool cut;
        MPEPattern mPEPattern;

        public CutOrCopyPatternEventsAction(MPEPattern pattern, Selection r, PatternClipboard clipboard, bool cut)
            : base(pattern.Pattern)
        {
            this.mPEPattern = pattern;
            this.r = r;

            this.clipboard = clipboard;
            this.cut = cut;
        }

        protected override void DoAction()
        {
            oldClipboard.Clone(Pattern, clipboard);

            if (cut)
            {
                oldEvents.Copy(mPEPattern, r);
                clipboard.Cut(mPEPattern, r);
            }
            else
            {
                clipboard.Copy(mPEPattern, r);
            }
        }

        protected override void UndoAction()
        {
            if (cut) oldEvents.Paste(mPEPattern, r);
            clipboard.Clone(Pattern, oldClipboard);
        }

    }
}
