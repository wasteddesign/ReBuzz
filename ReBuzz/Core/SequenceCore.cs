using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.ComponentModel;
using System.Linq;
using Wintellect.PowerCollections;
using SequenceEventPair = System.Collections.Generic.KeyValuePair<int, BuzzGUI.Interfaces.SequenceEvent>;

namespace ReBuzz.Core
{
    internal class SequenceCore : ISequence
    {
        private static IntPtr sequenceHandleCounter = 100;

        internal MachineCore MachineCore { get; private set; }
        public IMachine Machine { get => MachineCore; }

        OrderedDictionary<int, SequenceEvent> eventsList;
        public OrderedDictionary<int, SequenceEvent> EventsList
        {
            get => eventsList;
            set
            {
                eventsList = value;
                RefreshEventsSnapshot();
            }
        }
        public ReadOnlyDictionary<int, SequenceEvent> Events { get => new ReadOnlyDictionary<int, SequenceEvent>(EventsList); }

        // Flat, key-ordered, immutable copy of EventsList. Enumerating the PowerCollections
        // red-black tree allocates a recursive yield iterator per node, and the audio
        // thread, the pattern editors (from inside machine Work) and the sequence editor
        // all walk the events of every sequence many times a second.
        //
        // The array is rebuilt eagerly by every EventsList mutation (all of which run under
        // ReBuzzCore.AudioLock) and published as a new array, never modified afterwards, so
        // any thread can read it without a lock and always sees a complete, ordered set.
        // SequenceEvent objects are shared with EventsList, so Span updates made in place
        // by UpdateSpans() are visible without a rebuild.
        SequenceEventPair[] eventsSnapshot = Array.Empty<SequenceEventPair>();

        /// <summary>
        /// Key-ordered snapshot of <see cref="EventsList"/>, safe to read from any thread.
        /// Do not modify the returned array.
        /// </summary>
        internal SequenceEventPair[] EventsSnapshot => System.Threading.Volatile.Read(ref eventsSnapshot);

        /// <summary>
        /// Rebuilds <see cref="EventsSnapshot"/>. Call after any in-place change to the key
        /// set of <see cref="EventsList"/> (Add, Remove, indexer set, Clear); assigning
        /// EventsList does it automatically.
        /// </summary>
        void RefreshEventsSnapshot()
        {
            var source = eventsList;
            System.Threading.Volatile.Write(ref eventsSnapshot,
                source == null || source.Count == 0 ? Array.Empty<SequenceEventPair>() : source.ToArray());
        }

        /// <summary>
        /// Removes the event at <paramref name="time"/> and invalidates the snapshot.
        /// Does not update spans or raise change notifications.
        /// </summary>
        internal bool RemoveEventAt(int time)
        {
            lock (ReBuzzCore.AudioLock)
            {
                bool removed = EventsList.Remove(time);
                if (removed)
                    RefreshEventsSnapshot();
                return removed;
            }
        }

        public IPattern PlayingPattern
        {
            get
            {
                var buzz = MachineCore.Graph.Buzz as ReBuzzCore;

                if (buzz.SoloPattern != null && buzz.SoloPattern.Machine == Machine)
                    return buzz.SoloPattern;

                // Same order as EventsList.Values (key order), without allocating iterators.
                var events = EventsSnapshot;
                for (int i = 0; i < events.Length; i++)
                {
                    var pe = events[i].Value;
                    if (pe.Pattern != null && pe.Pattern.PlayPosition != int.MinValue)
                        return pe.Pattern;
                }

                if (TriggerEventInfo != null && TriggerEventInfo.se.Pattern != null && TriggerEventInfo.se.Pattern.PlayPosition != int.MinValue)
                {
                    return TriggerEventInfo.se.Pattern;
                }

                return null;
            }
        }

        // Returns ticks
        public int PlayingPatternPosition
        {
            get
            {
                var pattern = PlayingPattern;
                if (pattern != null)
                {
                    return pattern.PlayPosition / PatternEvent.TimeBase;
                }
                return -1;
            }
        }

        public IntPtr CSequence { get; private set; }


        bool isDisabled;
        public bool IsDisabled
        {
            get => isDisabled;
            set
            {
                isDisabled = value;
                if (isDisabled)
                {
                    // Maybe setting to send stop after disable?
                    //var buzz = Global.Buzz as ReBuzzCore;
                    //buzz.MachineManager.Stop(Machine as MachineCore);
                }
                PropertyChanged.Raise(this, "IsDisabled");
            }
        }

        public SequenceCore(MachineCore mc)
        {
            MachineCore = mc;
            CSequence = sequenceHandleCounter++;
            EventsList = new OrderedDictionary<int, SequenceEvent>();
        }

        public event Action<int> EventChanged;
        public event Action<int, int> SpanInserted;
        public event Action<int, int> SpanDeleted;
        public event Action<int, int> SpanCleared;
        public event PropertyChangedEventHandler PropertyChanged;

        public void Clear(int time, int span)
        {
            lock (ReBuzzCore.AudioLock)
            {
                OrderedDictionary<int, SequenceEvent> od = new OrderedDictionary<int, SequenceEvent>();
                foreach (var eventTime in Events.Keys.ToArray())
                {
                    if (eventTime < time || eventTime >= time + span)
                    {
                        od.Add(eventTime, Events[eventTime]);
                    }
                }

                EventsList = od;
                UpdateSpans();
                PropertyChanged.Raise(this, "Events");
                SpanCleared.Invoke(time, span);

                (Global.Buzz as ReBuzzCore).SetModifiedFlag();
            }
        }

        public void Delete(int time, int span)
        {
            lock (ReBuzzCore.AudioLock)
            {
                OrderedDictionary<int, SequenceEvent> od = new OrderedDictionary<int, SequenceEvent>();
                foreach (var eventTime in Events.Keys.ToArray())
                {
                    var e = Events[eventTime];
                    if (eventTime >= time && eventTime < time + span)
                    {
                        // Don't add events to updated sequence if between time and span (delete)
                    }
                    else if (eventTime >= time + span)
                    {
                        od.Add(eventTime - span, e);
                    }
                    else
                    {
                        od.Add(eventTime, e);
                    }
                }

                EventsList = od;
                UpdateSpans();
                SpanDeleted?.Invoke(time, span);
                PropertyChanged.Raise(this, "Events");
                (Global.Buzz as ReBuzzCore).SetModifiedFlag();
            }
        }

        public void Insert(int time, int span)
        {
            lock (ReBuzzCore.AudioLock)
            {
                OrderedDictionary<int, SequenceEvent> od = new OrderedDictionary<int, SequenceEvent>();
                foreach (var eventTime in Events.Keys.ToArray())
                {
                    var e = Events[eventTime];
                    if (eventTime >= time)
                    {
                        od.Add(eventTime + span, e);
                    }
                    else
                    {
                        od.Add(eventTime, e);
                    }
                }

                EventsList = od;
                UpdateSpans();
                SpanInserted?.Invoke(time, span);
                PropertyChanged.Raise(this, "Events");
                (Global.Buzz as ReBuzzCore).SetModifiedFlag();
            }
        }

        public void SetEvent(int time, SequenceEvent e)
        {
            lock (ReBuzzCore.AudioLock)
            {
                if (e == null)
                {
                    if (Events.ContainsKey(time))
                    {
                        EventsList.Remove(time);
                        RefreshEventsSnapshot();
                        UpdateSpans();
                        PropertyChanged.Raise(this, "Events");
                        EventChanged?.Invoke(time);
                    }
                }
                else
                {
                    if (e.Type == SequenceEventType.PlayPattern && e.Pattern != null)
                        e.Span = e.Pattern.Length;

                    this.EventsList[time] = e;
                    RefreshEventsSnapshot();
                    UpdateSpans();
                    PropertyChanged.Raise(this, "Events");
                    EventChanged?.Invoke(time);
                }
            (Global.Buzz as ReBuzzCore).SetModifiedFlag();
            }
        }

        // Callers refresh the snapshot first (assigning EventsList or RefreshEventsSnapshot),
        // so it already holds the current events in key order.
        void UpdateSpans()
        {
            var events = EventsSnapshot;
            for (int i = 0; i < events.Length; i++)
            {
                int time = events[i].Key;
                var se = events[i].Value;
                if (se.Type == SequenceEventType.PlayPattern)
                {
                    se.Span = se.Pattern.Length;
                    if ((i < events.Length - 1) && (time + se.Span > events[i + 1].Key))
                    {
                        events[i].Value.Span = events[i + 1].Key - time;
                    }
                }
            }
        }

        internal TriggerEventData TriggerEventInfo { get; set; }
        public void TriggerEvent(int time, SequenceEvent e, bool loop)
        {
            lock (ReBuzzCore.AudioLock)
            {
                if (time != 0 && e != null)
                {
                    TriggerEventInfo = new TriggerEventData(time, e, loop, false);
                }
                else
                {
                    TriggerEventInfo = null;
                }
            }
        }

        internal void SequenceEventChanged(int time, SequenceEvent e)
        {
            PropertyChanged.Raise(this, "Events");
            EventChanged?.Invoke(time);
        }

        internal void Release()
        {
            EventsList.Clear();
            // Also drops the old array, so it does not keep released patterns reachable.
            RefreshEventsSnapshot();
            PropertyChanged.Raise(this, "Events");
            PropertyChanged = null;
            EventChanged = null;
            SpanInserted = null;
            SpanDeleted = null;
            SpanCleared = null;
        }

        internal void InvokeEvents()
        {
            PropertyChanged.Raise(this, "Events");
        }
    }

    internal class TriggerEventData
    {
        internal TriggerEventData(int time, SequenceEvent se, bool loop, bool started)
        {
            this.time = time;
            this.se = se;
            this.loop = loop;
            this.started = started;
        }
        internal int time;
        internal SequenceEvent se;
        internal bool loop;
        internal bool started;

        public int PreviousPosition { get; internal set; }
    }
}
