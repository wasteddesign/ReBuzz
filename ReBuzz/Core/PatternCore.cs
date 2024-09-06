using BuzzGUI.Common;
using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using static ReBuzz.Core.ParameterCore;

namespace ReBuzz.Core
{
    internal class PatternCore : IPattern
    {
        private static int patternHandleCounter = 100;
        public IMachine Machine { get; set; }

        string name;
        public string Name { get => name; set { name = value; PropertyChanged.Raise(this, "Name"); } }

        int length;
        public int Length
        {
            get => length;
            set
            {
                length = value; PropertyChanged.Raise(this, "Length");
                foreach (var seq in Machine.Graph.Buzz.Song.Sequences)
                {
                    foreach (var seqEventKeyValue in seq.Events.Where(e => e.Value.Pattern == this))
                    {
                        var seqCore = seq as SequenceCore;
                        seqEventKeyValue.Value.Span = length;
                        seqCore.SequenceEventChanged(seqEventKeyValue.Key, seqEventKeyValue.Value);
                    }
                }
            }
        }

        readonly List<IPatternColumn> columns;
        public ReadOnlyCollection<IPatternColumn> Columns { get => columns.AsReadOnly(); }

        int playPosition;
        public int PlayPosition
        {
            get => playPosition;
            set
            {
                playPosition = value;
            }
        }

        bool isPlayingSolo;
        public bool IsPlayingSolo
        {
            get => isPlayingSolo;
            set
            {
                var bc = Global.Buzz as ReBuzzCore;
                isPlayingSolo = value;
                if (isPlayingSolo)
                {
                    bc.SoloPattern = this;
                    bc.Playing = true;
                    nextPositionInSamples = 0;
                }
                else
                {
                    nextPositionInSamples = 0;
                    bc.Playing = false;
                }

                PropertyChanged.Raise(this, "IsPlayingSolo");
            }
        }

        public int[] PatternEditorMachineMIDIEvents
        {
            get
            {
                var buzz = Machine.Graph.Buzz as ReBuzzCore;
                return buzz.MachineManager.GetPatternEditorMachineMIDIEvents(Machine as MachineCore, this);
            }
            set
            {
                var buzz = Machine.Graph.Buzz as ReBuzzCore;
                buzz.MachineManager.SetPatternEditorMachineMIDIEvents(Machine as MachineCore, this, value);
            }
        }

        public IntPtr CPattern { get; set; }
        public byte[] Data { get; internal set; }

        public IEnumerable<IPatternEditorColumn> PatternEditorMachineEvents
        {
            get
            {
                var buzz = Machine.Graph.Buzz as ReBuzzCore;
                var peme = buzz.MachineManager.PatternEditorMachineEvents(Machine as MachineCore, this);
                return peme;
            }
        }

        public PatternCore(MachineCore machineCore, string name, int length)
        {
            Machine = machineCore;
            Name = name;
            Length = length;

            CPattern = new IntPtr(patternHandleCounter++);
            columns = new List<IPatternColumn>();

            /*
            foreach (var parTrackTuple in Machine.AllParametersAndTracks())
            {
                PatternColumnCore pc = new PatternColumnCore(PatternColumnType.Parameter, this, Machine, parTrackTuple.Item1, parTrackTuple.Item2, null, null);
                columns.Add(pc);
            }
            */
        }

        public event Action<IPatternColumn> ColumnAdded;
        public event Action<IPatternColumn> ColumnRemoved;
        public event PropertyChangedEventHandler PropertyChanged;
        public event Action<IPatternColumn> PatternChanged;

        public void DeleteColumn(int index)
        {
            if (index < columns.Count)
            {
                var pcc = columns[index];
                columns.RemoveAt(index);
                ColumnRemoved?.Invoke(pcc);
            }
        }

        public void InsertColumn(int index, IParameter p, int track)
        {
            PatternColumnType type = p.IndexInGroup == (int)InternalParameter.MidiNote || p.IndexInGroup == -1 ? PatternColumnType.MIDI : PatternColumnType.Parameter;
            PatternColumnCore pcc = new PatternColumnCore(type, this, (p as ParameterCore).Machine, p, track, null, null);
            //int newIndex = Math.Min(index, columns.Count);
            //columns.Insert(newIndex, pcc);
            columns.Insert(index, pcc);
            ColumnAdded?.Invoke(pcc);
        }

        public void InsertColumn(int index, IMachine m)
        {
            if (m != null)
            {
                if (index < m.AllNonInputParameters().Count())
                {
                    var p = m.AllNonInputParameters().ElementAt(index);
                    InsertColumn(columns.Count(), p, 0);
                }
            }
            else
            {
                ParameterCore parameter = ParameterCore.GetMidiParameter(Machine as MachineCore);

                PatternColumnCore pcc = new PatternColumnCore(PatternColumnType.MIDI, this, Machine, parameter, 0, null, null);
                columns.Insert(index, pcc);
                ColumnAdded?.Invoke(pcc);
            }
        }

        public void UpdatePEMachineWaveReferences(IDictionary<int, int> map)
        {
        }

        double nextPositionInSamples = 0;
        internal void UpdateSoloPlayPosition(int sampleCount)
        {
            var masterInfo = ReBuzzCore.masterInfo;

            double tick = nextPositionInSamples / masterInfo.SamplesPerTick;

            if (tick >= Length)
            {
                nextPositionInSamples = 0;
                tick = 0;
            }
            playPosition = (int)(PatternEvent.TimeBase * tick);

            nextPositionInSamples += sampleCount;
        }

        public void NotifyPatternChanged()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                PatternChanged?.Invoke(null);
            }));
        }

        internal void ClearEvents()
        {
            PatternChanged = null;
            ColumnAdded = null;
            ColumnRemoved = null;
            PropertyChanged = null;
        }
    }
}
