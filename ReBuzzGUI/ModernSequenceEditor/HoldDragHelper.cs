using BuzzGUI.Common;
using BuzzGUI.Common.Actions.SequenceActions;
using BuzzGUI.Common.Actions.SongActions;
using BuzzGUI.Interfaces;
using BuzzGUI.SequenceEditor.Actions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace WDE.ModernSequenceEditor
{
    public class HoldDragHelper
    {
        int draggedPatternInitialTime;
        private int draggedPatternPreviousTime;

        public ISequence Sequence { get; private set; }
        public int Column { get; private set; }
        public ISong Song { get; private set; }
        public bool Clone { get; set; }

        Dictionary<int, SequenceEvent> sequenceEventsCopy;
        private bool isHolding = false;

        public bool IsHolding
        {
            get { return isHolding; }
            set
            {
                if (value)
                {
                    SequenceEditor.ViewSettings.EditContext.ActionStack.BeginActionGroup();
                }
                else
                {
                    if (isHolding)
                    {
                        SequenceEditor.ViewSettings.EditContext.ActionStack.EndActionGroup();
                    }
                }
                isHolding = value;
            }
        }
        public int Offset { get; private set; }

        public HoldDragHelper()
        {
            sequenceEventsCopy = new Dictionary<int, SequenceEvent>();
        }

        public void Reset(ISequence selectedSequence, int startColumn, ISong song)
        {
            Song = song;
            Clone = false;
            Sequence = selectedSequence;
            Column = startColumn;

            sequenceEventsCopy = new Dictionary<int, SequenceEvent>();
            foreach (int time in selectedSequence.Events.Dictionary.Keys)
                sequenceEventsCopy.Add(time, selectedSequence.Events.Dictionary[time]);

            draggedPatternInitialTime = -1;

            IsHolding = false;
        }

        internal void SetDraggedSequenceEvent(int snapTime)
        {
            draggedPatternInitialTime = snapTime;
            draggedPatternPreviousTime = snapTime;
        }

        internal void Update(int time, int column)
        {
            int snapTime = BuzzGUI.SequenceEditor.SequenceEditor.ViewSettings.TimeSignatureList.Snap(time - Offset, int.MaxValue);

            if (column != this.Column)
            {
                Do(new SwapSequencesAction(Sequence, Song.Sequences[column]));
                Column = column;
            }

            // First delete
            if (snapTime != draggedPatternInitialTime && Sequence.Events.ContainsKey(draggedPatternInitialTime) && !Clone)
            {
                Do(new ClearAction(Sequence, draggedPatternInitialTime, BuzzGUI.SequenceEditor.SequenceEditor.ViewSettings.TimeSignatureList.GetBarLengthAt(draggedPatternInitialTime)));
            }

            if (snapTime != draggedPatternPreviousTime)
            {
                // "Draw" Pattern 
                Do(new SetEventAction(Sequence, snapTime, sequenceEventsCopy[draggedPatternInitialTime]));

                // Delete the old position
                // Do(new ClearAction(Sequence, draggedPatternPreviousTime, SequenceEditor.ViewSettings.TimeSignatureList.GetBarLengthAt(draggedPatternPreviousTime)));

                // 
                if (!sequenceEventsCopy.ContainsKey(draggedPatternPreviousTime))
                {
                    Do(new ClearAction(Sequence, draggedPatternPreviousTime, BuzzGUI.SequenceEditor.SequenceEditor.ViewSettings.TimeSignatureList.GetBarLengthAt(draggedPatternInitialTime)));
                }

                else if (draggedPatternPreviousTime != draggedPatternInitialTime)
                {
                    Do(new SetEventAction(Sequence, draggedPatternPreviousTime, sequenceEventsCopy[draggedPatternPreviousTime]));
                }
            }

            draggedPatternPreviousTime = snapTime;
        }

        void Do(IAction a)
        {
            SequenceEditor.ViewSettings.EditContext.ActionStack.Do(a);
        }

        internal void SetDragOffset(int offset)
        {
            Offset = offset;
        }
    }
}
