using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System.Collections.ObjectModel;
using System.ComponentModel;
using static WDE.ModernPatternEditor.PatternEditorUtils;

namespace WDE.ModernPatternEditor.MPEStructures
{
    public class MPEParameterGroup : IParameterGroup
    {
        public IMachine Machine { get; private set; }

        public ParameterGroupType Type { get; private set; }

        public ReadOnlyCollection<IParameter> Parameters { get; private set; }

        public int TrackCount { get; set; }
    }

    public class MPEInternalParameter : IParameter
    {
        internal static readonly int DefaultMidiVolume = 100;
        private static Dictionary<Tuple<IMachine, InternalParameter>, MPEInternalParameter> internalParameters = new Dictionary<Tuple<IMachine, InternalParameter>, MPEInternalParameter>();

        public static MPEInternalParameter GetInternalParameter(IMachine mac, InternalParameter internalType)
        {
            MPEInternalParameter par = null;
            var key = new Tuple<IMachine, InternalParameter>(mac, internalType);
            if (internalParameters.ContainsKey(key))
                par = internalParameters[key];
            else
            {
                par = new MPEInternalParameter(mac, mac.ParameterGroups[2], internalType);
                internalParameters.Add(key, par);
            }

            return par;
        }

        private MPEInternalParameter(IMachine machine, IParameterGroup group, InternalParameter internalType)
        {
            this.Machine = machine;
            InternalType = internalType;
            Group = group;
            paramValues = new Dictionary<int, int>();
            IndexInGroup = (int)internalType;

            switch (internalType)
            {
                case InternalParameter.MidiNote:
                    {
                        MinValue = BuzzNote.Min;
                        MaxValue = BuzzNote.Max;
                        NoValue = 0;
                        DefValue = BuzzNote.Parse("C-4");
                        //IndexInGroup = -6;
                        Name = "MIDI Note";
                        Description = "MIDI Note";
                        Flags = ParameterFlags.TickOnEdit;
                        Type = ParameterType.Note;
                    }
                    break;
                case InternalParameter.MidiVelocity:
                    {
                        MinValue = 0;
                        MaxValue = 0x7f;
                        NoValue = 0;
                        DefValue = DefaultMidiVolume;
                        //IndexInGroup = -5;
                        Name = "MIDI Velocity";
                        Description = "MIDI Velocity";
                        Type = ParameterType.Byte;
                    }
                    break;
                case InternalParameter.MidiNoteDelay:
                    {
                        MinValue = 0;
                        MaxValue = 0x5f;
                        NoValue = 0;
                        DefValue = 0;
                        //IndexInGroup = -4;
                        Name = "MIDI Note Delay";
                        Description = "MIDI Note Delay";
                        Type = ParameterType.Byte;
                    }
                    break;
                case InternalParameter.MidiNoteCut:
                    {
                        MinValue = 0;
                        MaxValue = 0x5f;
                        NoValue = 0;
                        DefValue = 0;
                        //IndexInGroup = -3;
                        Name = "MIDI Note Cut";
                        Description = "MIDI Note Cut";
                        Type = ParameterType.Byte;
                    }
                    break;
                case InternalParameter.MidiPitchWheel:
                    {
                        MinValue = 0;
                        MaxValue = 0x3fff;
                        NoValue = 0;
                        DefValue = 0;
                        //IndexInGroup = -2;
                        Name = "MIDI Pitch Wheel";
                        Description = "MIDI Pitch Wheel";
                        Type = ParameterType.Word;
                    }
                    break;
                case InternalParameter.MidiCC:
                    {
                        MinValue = 0;
                        MaxValue = 0x7fff;
                        NoValue = 0;
                        DefValue = 0;
                        //IndexInGroup = -1;
                        Name = "MIDI CC";
                        Description = "MIDI CC";
                        Type = ParameterType.Word;
                    }
                    break;
            }

        }

        private Dictionary<int, int> paramValues;

        public InternalParameter InternalType { get; private set; }

        public IParameterGroup Group { get; private set; }

        public int IndexInGroup { get; private set; }

        public ParameterType Type { get; private set; }

        public string Name { get; private set; }

        public string Description { get; private set; }

        public int MinValue { get; private set; }

        public int MaxValue { get; private set; }

        public int NoValue { get; private set; }

        public ParameterFlags Flags { get; private set; }

        public int DefValue { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public IMachine Machine { get; }

        public void BindToMIDIController(int track, int mcindex)
        {

        }

        public string DescribeValue(int value)
        {
            return BuzzNote.TryToString(value);
        }

        public string GetDisplayName(int track)
        {
            return Name;
        }

        public int GetValue(int track)
        {
            return paramValues.ContainsKey(track) ? paramValues[track] : DefValue;
        }

        public bool IsValidAsciiChar(int ch)
        {
            return false;
        }

        public void SetValue(int track, int value)
        {
            value = Math.Min(MaxValue, Math.Max(value, MinValue));
            paramValues[track] = value;
        }

        public void SubscribeEvents(int track, Action<IParameter, int> valueChanged, Action<IParameter, int> valueDescriptionChanged)
        {
        }

        public void UnsubscribeEvents(int track, Action<IParameter, int> valueChanged, Action<IParameter, int> valueDescriptionChanged)
        {
        }
    }

    // Temporary until cleaned up. Merge with MPEPatternColumn?
    public class MPEPatternColumnBuzz : IPatternColumn
    {
        public MPEPatternColumnBuzz(PatternColumnType Type, IPattern Pattern, IMachine Machine, IParameter Parameter, int Track)
        {
            this.Type = Type;
            this.Pattern = Pattern;
            this.Machine = Machine;
            this.Parameter = Parameter;
            this.Track = Track;

            mBeatSubdivision = new Dictionary<int, int>();
            BeatSubdivision = new BuzzGUI.Interfaces.ReadOnlyDictionary<int, int>(mBeatSubdivision);
        }

        public PatternColumnType Type { get; }

        public IPattern Pattern { get; }

        public IMachine Machine { get; set; }

        public IParameter Parameter { get; }

        public int Track { get; }
        public BuzzGUI.Interfaces.ReadOnlyDictionary<int, int> BeatSubdivision { get; }

        public IDictionary<string, string> Metadata { get; }

        public event Action<IEnumerable<PatternEvent>, bool> EventsChanged;
        public event Action<int> BeatSubdivisionChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public Dictionary<int, int> mBeatSubdivision;


        public IEnumerable<PatternEvent> GetEvents(int tbegin, int tend)
        {
            return null;
        }

        public void SetBeatSubdivision(int beatindex, int subdiv)
        {
            mBeatSubdivision[beatindex] = subdiv;
        }

        public void SetEvents(IEnumerable<PatternEvent> e, bool set)
        {
        }

        public IEnumerable<PatternEvent> GetPEEvents(int tbegin, int tend)
        {
            return null;
        }
    }
}
