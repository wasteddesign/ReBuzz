using BuzzGUI.Interfaces;
using ReBuzz.Core;
using ReBuzz.FileOps;
using System;
using System.Collections.Generic;
using System.Dynamic;

namespace ReBuzz.MachineManagement
{
    internal class InstrumentManager
    {
        readonly List<Instrument> list;
        public InstrumentManager()
        {
            list = new List<Instrument>();

        }

        internal static Instrument CreateFromMoreMachines(MachineDLL machineDll)
        {
            Instrument instrument = new Instrument();

            instrument.Name = "";
            instrument.Path = "";

            if (machineDll.Info.Type == MachineType.Effect)
            {
                instrument.Type = InstrumentType.Effect;
            }
            else if (machineDll.Info.Type == MachineType.Generator && machineDll.Info.Flags.HasFlag(MachineInfoFlags.CONTROL_MACHINE))
            {
                instrument.Type = InstrumentType.Control;
            }
            else if (machineDll.Info.Type == MachineType.Generator)
            {
                instrument.Type = InstrumentType.Generator;
            }
            else
            {
                instrument.Type = InstrumentType.Unknown;
            }

            instrument.MachineDLL = machineDll;
            return instrument;
        }

        public List<Instrument> CreateInstrumentsList(IBuzz buzz, IMachineDatabase mdb)
        {
            list.Clear();

            foreach (var info in mdb.DictLibRef.Values)
            {
                if (buzz.MachineDLLs.ContainsKey(info.libName))
                {
                    var machineDll = buzz.MachineDLLs[info.libName];
                    Instrument instrument = new Instrument();

                    if (info.IsLoaderInstrument)
                    {
                        instrument.Name = info.InstrumentFullName;
                        instrument.Path = info.InstrumentPath;
                    }
                    else
                    {
                        instrument.Name = "";
                        instrument.Path = "";
                    }

                    if (machineDll.Info.Type == MachineType.Effect)
                    {
                        instrument.Type = InstrumentType.Effect;
                    }
                    else if (machineDll.Info.Type == MachineType.Generator && machineDll.Info.Flags.HasFlag(MachineInfoFlags.CONTROL_MACHINE))
                    {
                        instrument.Type = InstrumentType.Control;
                    }
                    else if (machineDll.Info.Type == MachineType.Generator)
                    {
                        instrument.Type = InstrumentType.Generator;
                    }
                    else
                    {
                        instrument.Type = InstrumentType.Unknown;
                    }

                    instrument.MachineDLL = machineDll;

                    list.Add(instrument);
                }
            }
            return list;
        }
    }
}
