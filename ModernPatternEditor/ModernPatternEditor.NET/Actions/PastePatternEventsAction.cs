using BuzzGUI.Common.Actions;
using BuzzGUI.Interfaces;
using WDE.ModernPatternEditor.MPEStructures;

namespace WDE.ModernPatternEditor.Actions
{
    public class PastePatternEventsAction : PatternAction
    {
        Selection r;
        PatternClipboard clipboard;
        PatternClipboard oldEvents = new PatternClipboard();
        MPEPattern mPEPattern;

        public PastePatternEventsAction(MPEPattern pattern, Selection r, PatternClipboard clipboard)
            : base(pattern.Pattern)
        {
            this.mPEPattern = pattern;
            this.r = Selection.Start(r.Bounds.Item1).SetEnd(r.Bounds.Item2);
            this.clipboard = clipboard;
        }

        protected override void DoAction()
        {
            if (!clipboard.ContainsData) return;

            Selection copyRange = CreatePasteSelection(); // Create copy range bease on clipboard rect

            oldEvents.Copy(mPEPattern, copyRange); // Save old events from 
            clipboard.Paste(mPEPattern, copyRange);
        }

        protected override void UndoAction()
        {
            Selection copyRange = CreatePasteSelection(); // Create copy range bease on clipboard rect
            oldEvents.Paste(mPEPattern, copyRange);
        }

        private Selection CreatePasteSelection()
        {
            //int rpb = r.Bounds.Item1.PatternVM.DefaultRPB;
            Digit rangeStart = r.Bounds.Item1.Offset(0, 0);
            Digit rangeEnd = rangeStart;//.SetColumn(rangeStart.Column + clipboard.NumColumns);
            for (int i = 0; i < clipboard.NumColumns; i++)
            {
                rangeEnd = rangeEnd.RightColumn;
            }

            int startTime = rangeStart.ParameterColumn.GetDigitTime(rangeStart);// rangeStart.Beat * (rpb * PatternEvent.TimeBase) + rangeStart.TimeInBeat;
            int endTime = startTime + clipboard.RegionLenght;
            int targetBeat = (endTime - 1) / (PatternControl.BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase);
            int timeInBeat = (endTime - 1) % (PatternControl.BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase);
            rangeEnd = rangeEnd.SetBeat(targetBeat);
            rangeEnd = rangeEnd.NearestRow(timeInBeat);
            if (endTime > Pattern.Length * PatternEvent.TimeBase)
                rangeEnd = rangeEnd.LastRowInBeat;

            return Selection.Start(rangeStart).SetEnd(rangeEnd);
        }
    }
}
