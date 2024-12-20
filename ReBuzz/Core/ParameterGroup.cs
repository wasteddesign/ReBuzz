using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ReBuzz.Core
{
    internal class ParameterGroup : IParameterGroup
    {
        IMachine machine;
        public IMachine Machine { get => machine; internal set => machine = value; }

        ParameterGroupType parameterGroupType;
        public ParameterGroupType Type { get => parameterGroupType; set => parameterGroupType = value; }

        List<ParameterCore> parameters = new List<ParameterCore>();

        public ParameterGroup(MachineCore machine, ParameterGroupType type)
        {
            this.machine = machine;
            this.Type = type;
        }

        public ReadOnlyCollection<IParameter> Parameters { get => parameters.Cast<IParameter>().ToReadOnlyCollection(); }
        public List<ParameterCore> ParametersList { get => parameters; set => parameters = value; }

        int trackCount;
        public int TrackCount
        {
            get => trackCount;
            set
            {
                /*
                // Set defaults
                for (int i = trackCount; i < value; i++)
                {
                    foreach (var parameter in Parameters)
                    {
                        if (parameter.Type != ParameterType.Note)
                            parameter.SetValue(i, parameter.DefValue);
                    }
                }

                for (int i = value; i < trackCount; i++)
                {
                    foreach (var parameter in Parameters)
                    {
                        if (parameter.Type != ParameterType.Note)
                            parameter.SetValue(i, parameter.DefValue);
                    }
                }
                */

                if (trackCount != value)
                {
                    trackCount = value;
                    if (Type == ParameterGroupType.Track)
                    {
                        machine.TrackCount = value;
                    }
                }
            }
        }

        internal void AddParameter(ParameterCore parameter)
        {
            parameter.Group = this;
            parameter.IndexInGroup = parameters.Count;
            parameters.Add(parameter);
        }

        public static ParameterGroup CreateInputGroup(MachineCore machine, IUiDispatcher dispatcher)
        {
            var pg = new ParameterGroup(machine, ParameterGroupType.Input);
            var pAmp = new ParameterCore(dispatcher) { IndexInGroup = 0, Name = "Amp", Group = pg, MinValue = 0, MaxValue = 0xfffe, Description = "Amp (0=0%, 4000=100%, FFFE=~400%)", Flags = ParameterFlags.State, Type = ParameterType.Word, DefValue = 0x4000 };
            var pPan = new ParameterCore(dispatcher) { IndexInGroup = 1, Name = "Pan", Group = pg, MinValue = 0, MaxValue = 0x8000, Description = "Pan (0=Left, 4000=Center, 8000=Right)", Flags = ParameterFlags.State, Type = ParameterType.Word, DefValue = 0x4000 };
            pg.AddParameter(pAmp);
            pg.AddParameter(pPan);
            pAmp.SetValue(0, 0x4000);
            pPan.SetValue(0, 0x4000);
            pg.TrackCount = 0;
            return pg;
        }

        public static void AddInputs(MachineConnectionCore mc, ParameterGroup pg)
        {
            var pAmp = pg.ParametersList[0];
            var pPan = pg.ParametersList[1];
            pAmp.SetValue(pg.TrackCount, mc.Amp);
            pPan.SetValue(pg.TrackCount, mc.Pan);

            pAmp.SetDisplayName(pg.TrackCount, mc.Source.Name);
            pPan.SetDisplayName(pg.TrackCount, mc.Source.Name);
            pg.TrackCount++;
        }

        internal ParameterGroup Clone()
        {
            var mac = machine as MachineCore;
            ParameterGroup pg = new ParameterGroup(mac, this.parameterGroupType);
            foreach (var p in this.ParametersList)
            {
                var newParam = p.Clone();
                newParam.Group = this;
                pg.AddParameter(newParam);
            }
            return pg;
        }
    }
}
