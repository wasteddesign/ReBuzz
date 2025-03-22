using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.ComponentModel;
using System.Linq;
using Wintellect.PowerCollections;

namespace ReBuzz.Core
{
    internal class SequenceCore : ISequence
    {
        private static IntPtr sequenceHandleCounter = 100;

        internal MachineCore MachineCore { get; private set; }
        public IMachine Machine { get => MachineCore; }

        public OrderedDictionary<int, SequenceEvent> EventsList { get; set; }
        public ReadOnlyDictionary<int, SequenceEvent> Events { get => new ReadOnlyDictionary<int, SequenceEvent>(EventsList); }

        public IPattern PlayingPattern
        {
            get
            {
                var buzz = MachineCore.Graph.Buzz as ReBuzzCore;

                if (buzz.SoloPattern != null && buzz.SoloPattern.Machine == Machine)
                    return buzz.SoloPattern;

                foreach (var pe in EventsList.Values)
                {
                    if (pe.Pattern != null && pe.Pattern.PlayPosition != int.MinValue)
                        return pe.Pattern;
                }

                if (TriggerEventInfo != null && TriggerEventInfo.se.Pattern.PlayPosition != int.MinValue)
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

        public void Delete(int time, int span)
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

        public void Insert(int time, int span)
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

        public void SetEvent(int time, SequenceEvent e)
        {
            if (e == null)
            {
                if (Events.ContainsKey(time))
                {
                    EventsList.Remove(time);
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
                UpdateSpans();
                PropertyChanged.Raise(this, "Events");
                EventChanged?.Invoke(time);
            }
            (Global.Buzz as ReBuzzCore).SetModifiedFlag();
        }

        void UpdateSpans()
        {
            var events = EventsList.ToArray();
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
            if (time != 0 && e != null)
            {
                TriggerEventInfo = new TriggerEventData(time, e, loop, false);
            }
            else
            {
                TriggerEventInfo = null;
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
