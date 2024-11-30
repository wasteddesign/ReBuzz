using BuzzGUI.Common;
using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace ReBuzz.Core
{
    internal class ParameterCore : IParameter
    {
        public enum InternalParameter
        {
            SPGlobalTrigger = -10,
            SPGlobalEffect1 = -11,
            SPGlobalEffect1Data = -12,

            SPTrackTrigger = -110,
            SPTrackEffect1 = -111,
            SPTrackEffect1Data = -112,

            MidiNote = -128,
            MidiVelocity = -129,
            MidiNoteDelay = -130,
            MidiNoteCut = -131,
            MidiPitchWheel = -132,
            MidiCC = -133
        };

        public IParameterGroup Group { get; set; }

        public int IndexInGroup { get; set; }
        public MachineCore Machine { get; private set; }
        public ParameterType Type { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public int MinValue { get; set; }

        public int MaxValue { get; set; }

        public int NoValue { get; set; }

        //readonly Lock paramLock = new();

        public ParameterFlags Flags { get; set; }

        public int DefValue { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public void BindToMIDIController(int track, int mcindex)
        {
            var buzz = Group.Machine.Graph.Buzz as ReBuzzCore;
            buzz.MidiControllerAssignments.BindParameter(this, track, mcindex);
        }

        int describeValuePrev = -1;
        string descriveValuePrevString = null;
        public string DescribeValue(int value)
        {
            string ret = "";
            // Avoid calling native machines too often
            if (describeValuePrev == value)
            {
                return descriveValuePrevString;
            }

            if (Group.Type == ParameterGroupType.Input)
            {
                if (IndexInGroup == 0)
                {
                    ret = value > 0 ? string.Format("{0:F1}dB", Decibel.FromAmplitude(value * (1.0 / 0x4000))) : "-inf.dB";
                }
                else if (IndexInGroup == 1)
                {
                    string pan = value.ToString();
                    if (value == 0)
                        pan = "L";
                    else if (value == 0x4000)
                        pan = "C";
                    else if (value == 0x8000)
                        pan = "R";

                    ret = pan;
                }
            }
            else
            {
                var machine = Group.Machine as MachineCore;
                var buzz = machine.Graph.Buzz as ReBuzzCore;
                int index = machine.AllNonInputParameters().FindIndex(p => p == this);

                ret = buzz.MachineManager.DescribeValue(machine, index, value);

                if (ret == null)
                {
                    ret = value.ToString();
                }
            }
            describeValuePrev = value;
            descriveValuePrevString = ret;
            return ret;
        }

        public void SetDisplayName(int track, string name)
        {
            displayNames[track] = name;
        }

        public string GetDisplayName(int track)
        {
            if (Group.Type == ParameterGroupType.Input)
            {
                return displayNames.ContainsKey(track) ? displayNames[track] : "";
            }
            else
            {
                return Name;
            }
        }

        public int GetValue(int track)
        {
            if (!values.ContainsKey(track))
                values[track] = DefValue;

            return values[track];
        }

        public int GetPValue(int track)
        {
            if (!pvalues.ContainsKey(track))
                return DefValue;

            return pvalues[track];
        }

        public void ClearPVal()
        {
            pvalues.Clear();
        }

        public bool IsValidAsciiChar(int ch)
        {
            return true;
        }

        ConcurrentDictionary<int, int> values = new ConcurrentDictionary<int, int>();
        readonly ConcurrentDictionary<int, int> pvalues = new ConcurrentDictionary<int, int>();
        readonly Dictionary<int, string> displayNames = new Dictionary<int, string>();

        // Is ConcurrentDictionary needed. Adds locks and latency?
        private readonly ConcurrentDictionary<int, EventManager> valueChangedEvent = new ConcurrentDictionary<int, EventManager>();
        private readonly ConcurrentDictionary<int, EventManager> valueDescrtiptionChangedEvent = new ConcurrentDictionary<int, EventManager>();

        //internal Dictionary<int, int> Values { get => values; set => values = value; }
        public ParameterCore()
        {
            dtDescribeEvent = new DispatcherTimer();
            dtDescribeEvent.Interval = TimeSpan.FromMilliseconds(1000 / 30.0);
        }
        internal class EventManager
        {
            public event Action<IParameter, int> Event;
            public void CallEvent(IBuzz buzz, IParameter parameter, int track)
            {
                if (Event != null)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (Event != null)
                                Event(parameter, track);
                        }
                        catch (Exception e)
                        {
                            buzz.DCWriteLine(e.Message);
                        }
                    }), DispatcherPriority.Normal);
                }
            }

            public void CallEventDesc(IParameter parameter, int track)
            {
                if (Event != null)
                {
                    //Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    //{
                    try
                    {
                        if (Event != null)
                            Event(parameter, track);
                    }
                    catch (Exception e)
                    {
                        Global.Buzz.DCWriteLine(e.Message);
                    }
                    finally
                    {
                        describeValueEventPending = false;
                    }

                    //}), DispatcherPriority.Normal);
                }
            }

            internal void ClearEvents()
            {
                Event = null;
            }
        }

        static bool describeValueEventPending = false;
        readonly DispatcherTimer dtDescribeEvent;
        internal void InvokeEvents(IBuzz buzz, int track)
        {
            if (valueChangedEvent.TryGetValue(track, out EventManager em))
            {
                em.CallEvent(buzz, this, track);
            }

            // Aviod invoking valueDescrtiptionChangedEvent to minimize calls to native machines
            if (!describeValueEventPending)
            {
                if (valueDescrtiptionChangedEvent.TryGetValue(track, out EventManager emd))
                {
                    describeValueEventPending = true;

                    dtDescribeEvent.Tick += (s, e) =>
                    {
                        emd.CallEventDesc(this, track);
                        dtDescribeEvent.Stop();
                    };
                    dtDescribeEvent.Start();
                }
            }
            else
            {
                // Reset timer
                dtDescribeEvent.Start();
            }
        }

        internal void DirectSetValue(int track, int value)
        {
            values[track] = value;
        }

        public void SetValue(int track, int value)
        {
            if (Group == null)
            {
                // ParameterCore is used as a temporary storage in BMXFile. FIXME?
                return;
            }

            var machine = Group.Machine as MachineCore;

            // Lock machine here and MachineWorkInstance.cs Tick to avoid potential collision
            lock (machine.workLock)
            {
                track = track == -1 ? 0 : track;

                int do_not_record_flag = 1 << 16;
                bool record = true;
                if ((track & do_not_record_flag) != 0)
                {
                    record = false;
                    track &= ~do_not_record_flag;
                }

                // Check ranges
                if (value != NoValue)
                    if (Type == ParameterType.Note && value != BuzzNote.Off)
                        value = Math.Max(MinValue, Math.Min(MaxValue, value));

                // Save changes to be sent to managed machines and as events to listeners.
                machine.parametersChanged[this] = track;

                // Do we need this?
                if (values.ContainsKey(track) && values[track] == value)
                    return;

                values[track] = value;
                if (value != NoValue)
                    pvalues[track] = value;

                // Update inputs. Should this be moved to tick?
                if (Group.Type == ParameterGroupType.Input && machine.ParameterGroups.Count != 0)
                {
                    if (IndexInGroup < machine.ParameterGroups[0].Parameters.Count
                        && track < machine.Inputs.Count)
                    {
                        var input = machine.Inputs[track];
                        if (IndexInGroup == 0)
                        {
                            input.Amp = value;
                        }
                        else
                        {
                            input.Pan = value;
                        }
                    }
                }
                else if (record && Group != null && Group.Machine.Graph != null)
                {
                    var bc = Group.Machine.Graph.Buzz as ReBuzzCore;
                    //bc.RecordParametersDictionary.TryAdd(new Tuple<ParameterCore,int>(this, track), value);
                    bc.RecordControlChange(this, track, value);
                }
            }
        }

        public void SubscribeEvents(int track, Action<IParameter, int> valueChanged, Action<IParameter, int> valueDescriptionChanged)
        {
            if (valueChanged != null)
            {
                if (!valueChangedEvent.ContainsKey(track))
                    valueChangedEvent.TryAdd(track, new EventManager());

                var em = valueChangedEvent[track];
                em.Event += valueChanged;
            }
            if (valueDescriptionChanged != null)
            {
                if (!valueDescrtiptionChangedEvent.ContainsKey(track))
                    valueDescrtiptionChangedEvent.TryAdd(track, new EventManager());

                var em = valueDescrtiptionChangedEvent[track];
                em.Event += valueChanged;
            }
        }

        public void UnsubscribeEvents(int track, Action<IParameter, int> valueChanged, Action<IParameter, int> valueDescriptionChanged)
        {
            if (valueChanged != null)
            {
                if (valueChangedEvent.ContainsKey(track))
                {
                    var em = valueChangedEvent[track];
                    em.Event -= valueChanged;
                }
            }
            if (valueDescriptionChanged != null)
            {
                if (valueDescrtiptionChangedEvent.ContainsKey(track))
                {
                    var em = valueDescrtiptionChangedEvent[track];
                    em.Event -= valueChanged;
                }
            }
        }

        internal int GetTypeSize()
        {
            if (Type == ParameterType.Note ||
                Type == ParameterType.Switch ||
                Type == ParameterType.Byte)
            {
                return 1;
            }
            else if (Type == ParameterType.Word)
            {
                return 2;
            }
            return 0;
        }

        internal ParameterCore Clone()
        {
            ParameterCore p = new ParameterCore();
            p.Type = Type;
            p.Name = Name;
            p.MinValue = MinValue;
            p.MaxValue = MaxValue;
            p.DefValue = DefValue;
            p.NoValue = NoValue;
            p.Flags = Flags;
            p.Description = Description;
            p.Group = Group;
            p.IndexInGroup = IndexInGroup;
            p.values = values;

            return p;
        }

        internal void ClearEvents()
        {
            //PropertyChanged = null;
            foreach (var em in valueChangedEvent.Values)
            {
                em.ClearEvents();
            }
            foreach (var em in valueDescrtiptionChangedEvent.Values)
            {
                em.ClearEvents();
            }
            valueChangedEvent.Clear();
            valueDescrtiptionChangedEvent.Clear();
        }

        internal static ParameterCore GetMidiParameter(MachineCore machine)
        {
            ParameterCore parameter = new ParameterCore();
            parameter.Name = "MIDI Note";
            parameter.Description = "MIDI Note";
            parameter.MinValue = BuzzNote.Min;
            parameter.MaxValue = BuzzNote.Max;
            parameter.DefValue = BuzzNote.Parse("C-4");
            parameter.NoValue = 0;
            parameter.Type = ParameterType.Note;
            parameter.Flags = ParameterFlags.State | ParameterFlags.TickOnEdit;
            parameter.SetValue(0, parameter.NoValue);
            parameter.IndexInGroup = -1;// (int)InternalParameter.MidiNote;
            parameter.Machine = machine;
            //parameter.Group = machine.ParameterGroups[2]; // Put Midi parameters to to Track Group

            return parameter;
        }
    }
}
