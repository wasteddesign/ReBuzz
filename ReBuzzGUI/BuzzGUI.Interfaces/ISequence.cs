using System;
using System.ComponentModel;

namespace BuzzGUI.Interfaces
{
    public enum SequenceEventType { PlayPattern, Break, Mute, Thru };

    public class SequenceEvent
    {
        public SequenceEvent(SequenceEvent e) { Type = e.Type; Pattern = e.Pattern; Span = e.Span; }
        public SequenceEvent(SequenceEventType type) { Type = type; }
        public SequenceEvent(SequenceEventType type, IPattern pattern) { Type = type; Pattern = pattern; }
        public SequenceEvent(SequenceEventType type, IPattern pattern, int span) { Type = type; Pattern = pattern; Span = span; }

        public SequenceEventType Type { get; private set; }
        public IPattern Pattern { get; private set; }
        public int Span { get; set; }       // = Pattern.Length unless clipped by another event
    }

    public interface ISequence : INotifyPropertyChanged
    {
        IMachine Machine { get; }
        ReadOnlyDictionary<int, SequenceEvent> Events { get; }

        bool IsDisabled { get; set;  }

        IPattern PlayingPattern { get; }
        int PlayingPatternPosition { get; }

        event Action<int> EventChanged;

        // fired after Insert/Delete/Clear operations instead of invalidating Events
        // needed for performance reasons
        event Action<int, int> SpanInserted;
        event Action<int, int> SpanDeleted;
        event Action<int, int> SpanCleared;

        void SetEvent(int time, SequenceEvent e);       // e == null deletes event
        void Insert(int time, int span);
        void Delete(int time, int span);
        void Clear(int time, int span);

        // trigger an event at time (ticks) overriding possible sequence event and previous call to TriggerEvent, e == null cancels (time is ignored)
        // used for live pattern triggering
        void TriggerEvent(int time, SequenceEvent e, bool loop);

    }
}
