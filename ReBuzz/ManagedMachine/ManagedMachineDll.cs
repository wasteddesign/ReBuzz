using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using ReBuzz.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ReBuzz.ManagedMachine
{
    internal class ManagedMachineDLL
    {
        internal enum WorkFunctionTypes
        {
            None,
            Generator,
            Effect,
            GeneratorBlock,
            EffectBlock,
            Control,
            EffectBlockMulti,
            GeneratorBlockMulti
        }

        public Assembly Assembly { get; set; }
        public WorkFunctionTypes WorkFunctionType { get; set; }
        public Type machineType { get; set; }
        public MachineInfo MachineInfo { get; private set; }

        public MachineParameter[] globalParameters;
        public MachineParameter[] trackParameters;
        internal MachineDecl machineInfo;
        internal ConstructorInfo constructor;

        public void LoadManagedMachine(string path)
        {
            Assembly = Assembly.LoadFile(path);
            //Assembly = Assembly.UnsafeLoadFrom(path);

            machineType = Assembly.GetTypes().FirstOrDefault((Type t) => t.GetInterface("IBuzzMachine") != null);
            if (machineType == null)
            {
                throw new Exception("IBuzzMachine implementation missing");
            }
            if (!machineType.IsPublic || !machineType.IsClass || machineType.IsAbstract || machineType.IsInterface)
            {
                throw new Exception($"class {machineType.Name} must be declared public");
            }
            machineInfo = machineType.GetCustomAttributes(inherit: false).OfType<MachineDecl>().FirstOrDefault();
            if (machineInfo == null)
            {
                throw new Exception("MachineDecl attribute missing");
            }
            constructor = machineType.GetConstructor(new Type[1] { typeof(IBuzzMachineHost) });
            if (constructor == null || !constructor.IsPublic)
            {
                throw new Exception($"class {machineType.Name} must have a public constructor that takes an IBuzzMachineHost parameter");
            }
            MachineInfo nmi = new MachineInfo();
            nmi.Version = Global.Buzz.HostVersion;
            nmi.Flags = MachineInfoFlags.LOAD_DATA_RUNTIME;
            nmi.MinTracks = 0;
            nmi.MaxTracks = 0;
            nmi.Name = machineInfo.Name;
            nmi.ShortName = machineInfo.ShortName;
            nmi.Author = machineInfo.Author;

            WorkFunctionType = GetWorkFunctionType();
            if (WorkFunctionType == WorkFunctionTypes.None || WorkFunctionType == WorkFunctionTypes.Control)
            {
                nmi.Type = MachineType.Generator;
                nmi.Flags |= MachineInfoFlags.NO_OUTPUT | MachineInfoFlags.CONTROL_MACHINE;
            }
            else if (WorkFunctionType == WorkFunctionTypes.GeneratorBlockMulti)
            {
                nmi.Type = MachineType.Generator;
                nmi.Flags |= MachineInfoFlags.MULTI_IO;
            }
            else if (WorkFunctionType == WorkFunctionTypes.EffectBlockMulti)
            {
                nmi.Type = MachineType.Effect;
                nmi.Flags |= MachineInfoFlags.MULTI_IO;
            }
            else
            {
                nmi.Type = ((WorkFunctionType == WorkFunctionTypes.Generator || WorkFunctionType == WorkFunctionTypes.GeneratorBlock) ? MachineType.Generator : MachineType.Effect);
            }

            MethodInfo method = machineType.GetMethod("PatternEditorControl");
            if (method != null)
            {
                nmi.Flags |= MachineInfoFlags.PATTERN_EDITOR;
            }

            globalParameters = (from p in machineType.GetProperties()
                                where p.GetCustomAttributes(inherit: false).OfType<ParameterDecl>().Any()
                                orderby p.MetadataToken
                                select p into pp
                                select new MachineParameter(pp)).ToArray();
            trackParameters = (from p in machineType.GetMethods()
                               where p.GetCustomAttributes(inherit: false).OfType<ParameterDecl>().Any()
                               orderby p.MetadataToken
                               select p into pp
                               select new MachineParameter(pp)).ToArray();
            if (globalParameters.Length == 0 && trackParameters.Length == 0)
            {
                throw new Exception("at least one parameter is required");
            }
            //nmi.numGlobalParameters = globalParameters.Length;
            //nmi.numTrackParameters = trackParameters.Length;
            if (trackParameters.Length > 0)
            {
                nmi.MinTracks = 1;
                nmi.MaxTracks = Math.Max(1, machineInfo.MaxTracks);
            }

            this.MachineInfo = nmi;
        }

        private WorkFunctionTypes GetWorkFunctionType()
        {
            MethodInfo method = machineType.GetMethod("Work");
            if (method == null)
            {
                return WorkFunctionTypes.None;
            }
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 0 && method.ReturnType == typeof(void))
            {
                return WorkFunctionTypes.Control;
            }
            if (parameters.Length == 0 && method.ReturnType == typeof(Sample))
            {
                return WorkFunctionTypes.Generator;

            }
            if (parameters.Length == 1 && method.ReturnType == typeof(Sample) && parameters[0].ParameterType == typeof(Sample))
            {
                return WorkFunctionTypes.Effect;
            }
            if (parameters.Length == 3 && method.ReturnType == typeof(bool) && parameters[0].ParameterType == typeof(Sample[]) && parameters[1].ParameterType == typeof(int) && parameters[2].ParameterType == typeof(WorkModes))
            {
                return WorkFunctionTypes.GeneratorBlock;
            }
            if (parameters.Length == 3 && method.ReturnType == typeof(bool) && parameters[0].ParameterType == typeof(IList<Sample[]>) && parameters[1].ParameterType == typeof(int) && parameters[2].ParameterType == typeof(WorkModes))
            {
                return WorkFunctionTypes.GeneratorBlockMulti;
            }
            if (parameters.Length == 4 && method.ReturnType == typeof(bool) && parameters[0].ParameterType == typeof(Sample[]) && parameters[1].ParameterType == typeof(Sample[]) && parameters[2].ParameterType == typeof(int) && parameters[3].ParameterType == typeof(WorkModes))
            {
                return WorkFunctionTypes.EffectBlock;
            }
            if (parameters.Length == 4 && method.ReturnType == typeof(bool) && parameters[0].ParameterType == typeof(IList<Sample[]>) && parameters[1].ParameterType == typeof(IList<Sample[]>) && parameters[2].ParameterType == typeof(int) && parameters[3].ParameterType == typeof(WorkModes))
            {
                return WorkFunctionTypes.EffectBlockMulti;
            }
            throw new Exception("invalid Work function");
        }
        public IBuzzMachine CreateMachine(IBuzzMachineHost h)
        {
            return Activator.CreateInstance(machineType, new object[1] { h }) as IBuzzMachine;
            //return constructor.Invoke(new object[1] { h }) as IBuzzMachine
        }

        internal MachineParameter.Delegates[] CreateParameterDelegates(IBuzzMachine m)
        {
            return globalParameters.Select((MachineParameter p) => p.CreateDelegates(m)).Concat(trackParameters.Select((MachineParameter p) => p.CreateDelegates(m))).ToArray();
        }

        internal void CreateMachineDllParameters(MachineCore mc)
        {
            mc.MachineDLL.MachineInfo = MachineInfo;

            ParameterGroup gpg = new ParameterGroup(mc, ParameterGroupType.Global);
            gpg.TrackCount = 1;

            foreach (var mPar in globalParameters)
            {
                CreateParameter(mPar, gpg);
            }
            mc.ParameterGroupsList.Add(gpg);
            /*
            if (mc.ParameterGroupsList.Count > 0)
            {
                gpg.TrackCount = 1; // One track max for Global
            }
            */

            ParameterGroup tpg = new ParameterGroup(mc, ParameterGroupType.Track);
            tpg.TrackCount = mc.TrackCount;
            foreach (var mPar in trackParameters)
            {
                CreateParameter(mPar, tpg);
            }
            mc.ParameterGroupsList.Add(tpg);
        }

        internal void UpdateMachineDllInfo(MachineCore mc)
        {
            mc.MachineDLL.MachineInfo = MachineInfo;

            if (WorkFunctionType == WorkFunctionTypes.Effect || WorkFunctionType == WorkFunctionTypes.EffectBlock || WorkFunctionType == WorkFunctionTypes.EffectBlockMulti)
            {
                mc.OutputChannelCount = 1;
                mc.InputChannelCount = 1;
                mc.HasStereoInput = true;
                mc.HasStereoOutput = true;
            }
            else if (WorkFunctionType == WorkFunctionTypes.GeneratorBlock || WorkFunctionType == WorkFunctionTypes.Generator)
            {
                mc.OutputChannelCount = 1;
                mc.InputChannelCount = 0;
                mc.HasStereoOutput = true;
            }
            else if (WorkFunctionType == WorkFunctionTypes.Control)
            {
                mc.OutputChannelCount = 0;
                mc.InputChannelCount = 0;
                mc.IsControlMachine = true;
            }
        }

        private void CreateParameter(MachineParameter mPar, ParameterGroup pg)
        {
            ParameterCore p = new ParameterCore();
            p.Name = mPar.Name;
            p.Type = mPar.Type;
            p.Flags = mPar.Flags;
            p.MinValue = mPar.MinValue;
            p.MaxValue = mPar.MaxValue;
            p.NoValue = mPar.NoValue;
            p.DefValue = mPar.DefValue;
            p.Description = mPar.Description;
            p.Group = pg;
            p.IndexInGroup = pg.Parameters.Count;
            pg.AddParameter(p);
        }
    }
}
