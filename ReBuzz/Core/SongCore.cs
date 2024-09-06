using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using ReBuzz.Core.Actions.GraphActions;
using ReBuzz.MachineManagement;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace ReBuzz.Core
{
    public class SongCore : ISong, INotifyPropertyChanged
    {
        #region ISong_Interface

        int playPosition = 0;
        internal int PlayPositionSetNextTick = -1; // Update this in the next tick
        internal bool AdjustPositionOnTick = false;
        public int PlayPosition
        {
            get { return playPosition; }
            set
            {
                if (value < LoopStart)
                    value = LoopStart;

                AdjustPositionOnTick = true;

                if (BuzzCore.Playing && BuzzCore.SoloPattern == null)
                {
                    PlayPositionSetNextTick = value;
                }
                else
                {
                    playPosition = value;
                    ReBuzzCore.GlobalState.SongPosition = playPosition;

                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        PropertyChanged.Raise(this, "PlayPosition");
                    });
                }
            }
        }

        // Move play position 
        internal void UpdatePlayPosition(int ticks)
        {
            if (ReBuzzCore.masterInfo.PosInTick == 0 && PlayPositionSetNextTick != -1)
            {
                playPosition = PlayPositionSetNextTick;
                PlayPositionSetNextTick = -1;
            }
            else
            {
                playPosition += ticks;
            }

            if (playPosition >= LoopEnd)
            {
                playPosition = LoopStart;
            }

            UpdateSpecialSequeceEvents();

            ReBuzzCore.GlobalState.SongPosition = playPosition;

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                PropertyChanged.Raise(this, "PlayPosition");
            });
        }

        void UpdateSpecialSequeceEvents()
        {
            foreach (var seq in sequences)
            {
                if (seq.Events.TryGetValue(playPosition, out var se))
                {
                    if (se.Type == SequenceEventType.Mute)
                    {
                        seq.MachineCore.IsSeqMute = true;
                    }
                    else if (se.Type == SequenceEventType.Thru && seq.Machine.DLL.Info.Type == MachineType.Effect)
                    {
                        seq.MachineCore.IsSeqThru = true;
                    }
                    else
                    {
                        seq.MachineCore.IsSeqMute = seq.MachineCore.IsSeqThru = false;
                    }
                }
            }
        }

        int loopStart = 0;
        public int LoopStart
        {
            get { return loopStart; }
            set
            {
                loopStart = value;
                ReBuzzCore.GlobalState.LoopStart = value;
                if (PlayPosition < loopStart)
                {
                    PlayPosition = loopStart;
                }
                PropertyChanged.Raise(this, "LoopStart");
            }
        }

        int loopEnd = 16;
        public int LoopEnd
        {
            get { return loopEnd; }
            set
            {
                loopEnd = value;
                ReBuzzCore.GlobalState.LoopEnd = value;
                if (PlayPosition >= loopEnd)
                {
                    PlayPosition = LoopStart;
                }
                if (SongEnd < loopEnd)
                {
                    SongEnd = loopEnd;
                }
                PropertyChanged.Raise(this, "LoopEnd");
            }
        }

        int songEnd = 16;
        public int SongEnd
        {
            get { return songEnd; }
            set
            {
                songEnd = value;
                ReBuzzCore.GlobalState.SongEnd = value;
                if (PlayPosition >= songEnd)
                {
                    PlayPosition = LoopStart;
                }
                PropertyChanged.Raise(this, "SongEnd");
            }
        }

        List<SequenceCore> sequences = new List<SequenceCore>();

        public ReadOnlyCollection<ISequence> Sequences { get => sequences.Cast<ISequence>().ToReadOnlyCollection(); }

        internal List<SequenceCore> SequencesList { get => sequences; set => sequences = value; }
        internal WavetableCore WavetableCore { get; set; }
        public IWavetable Wavetable { get => WavetableCore; }

        readonly IDictionary<string, object> associations = new Dictionary<string, object>();
        public IDictionary<string, object> Associations { get => associations; }

        string name;
        public string SongName { get => name; internal set { name = value; PropertyChanged.Raise(this, "Name"); } }

        public event Action<int> SequenceAdded;
        public event Action<int> SequenceRemoved;
        public event Action<int> SequenceChanged;

        public SongCore()
        {
        }

        public void AddSequence(IMachine m, int index)
        {
            lock (ReBuzzCore.AudioLock)
            {
                SequenceCore sequenceCore = new SequenceCore(m as MachineCore);
                sequences.Insert(index, sequenceCore);
                PropertyChanged.Raise(this, "Sequences");
                SequenceAdded?.Invoke(index);
            }

            Buzz.SetModifiedFlag();
        }

        public void RemoveSequence(ISequence s)
        {
            lock (ReBuzzCore.AudioLock)
            {
                var sc = s as SequenceCore;
                int index = sequences.IndexOf(sc);
                sequences.Remove(sc);
                PropertyChanged.Raise(this, "Sequences");
                SequenceRemoved?.Invoke(index);
                sc.Release();
            }
            Buzz.SetModifiedFlag();
        }

        public void SwapSequences(ISequence s, ISequence t)
        {
            lock (ReBuzzCore.AudioLock)
            {
                int indexs = sequences.IndexOf((SequenceCore)s);
                int indext = sequences.IndexOf((SequenceCore)t);
                sequences.RemoveAt(indexs);
                sequences.Insert(indexs, (SequenceCore)t);
                SequenceChanged?.Invoke(indexs);
                sequences.RemoveAt(indext);
                sequences.Insert(indext, (SequenceCore)s);
                SequenceChanged?.Invoke(indext);
            }

            Buzz.SetModifiedFlag();
        }

        #endregion

        #region MachineGraph

        ReBuzzCore reBuzzCore;
        public ReBuzzCore BuzzCore
        {
            get => reBuzzCore; set
            {
                reBuzzCore = value;
                WavetableCore = new WavetableCore(BuzzCore);
            }
        }


        public IBuzz Buzz { get => reBuzzCore; }

        List<MachineCore> machinesList = new List<MachineCore>();
        public List<MachineCore> MachinesList { get => machinesList; set => machinesList = value; }
        public ReadOnlyCollection<IMachine> Machines { get => machinesList.Where(m => !m.Hidden).Cast<IMachine>().ToReadOnlyCollection(); }

        public bool CanUndo { get => ActionStack.CanUndo; }

        public bool CanRedo { get => ActionStack.CanRedo; }
        ManagedActionStack actionStack = new ManagedActionStack();
        private IDictionary<string, string> importDictionary;
        internal Dictionary<MachineCore, MachineInitData> DictInitData = new Dictionary<MachineCore, MachineInitData>();
        private bool initImportDone;

        internal bool Importing { get; set; }

        public ManagedActionStack ActionStack { get => actionStack; set => actionStack = value; }
        public bool SoloMode { get; private set; }

        public event Action<IMachine> MachineAdded;
        public event Action<IMachine> MachineRemoved;
        public event Action<IMachineConnection> ConnectionAdded;
        public event Action<IMachineConnection> ConnectionRemoved;
        public event PropertyChangedEventHandler PropertyChanged;

        public void BeginActionGroup()
        {
            ActionStack.BeginActionGroup();
        }

        public bool CanConnectMachines(IMachine src, IMachine dst)
        {
            if (src.DLL.Info.Type != MachineType.Master &&
                !src.DLL.Info.Flags.HasFlag(MachineInfoFlags.CONTROL_MACHINE) &&
                !dst.DLL.Info.Flags.HasFlag(MachineInfoFlags.CONTROL_MACHINE) &&
                dst.DLL.Info.Type != MachineType.Generator &&
                dst.Outputs.FirstOrDefault(o => o.Destination == src) == null &&
                src.Outputs.FirstOrDefault(o => o.Destination == dst) == null &&
                !FindDestinationFromSourceConnetions(src, dst))
                return true;
            else
                return false;
        }

        private bool FindDestinationFromSourceConnetions(IMachine src, IMachine dst)
        {
            foreach (var mc in src.Inputs)
            {
                if (FindDestinationFromSourceConnetions(mc.Source, dst))
                    return true;
            }

            if (src == dst)
                return true;
            else
                return false;
        }

        public void CloneMachine(IMachine m, float x, float y)
        {
            Do(new CloneMachineAction(reBuzzCore, m, x, y));
        }

        public void ConnectMachines(IMachine src, IMachine dst, int srcchn, int dstchn, int amp, int pan)
        {
            if (Importing)
            {
                // Call native machine init before connecting
                InitImport();
            }
            Do(new ConnectMachinesAction(reBuzzCore, src, dst, srcchn, dstchn, amp, pan));
        }

        internal void ConnectMachines(MachineConnectionCore mcc)
        {
            Do(new ConnectMachinesAction(reBuzzCore, mcc.Source, mcc.Destination, mcc.SourceChannel, mcc.DestinationChannel, mcc.Amp, mcc.Pan));
        }

        public void CreateMachine(int id, float x, float y)
        {
            if (id >= 0)
            {
                Do(new CreateMachineAction(reBuzzCore, id, x, y));
            }
        }

        public void CreateMachine(string machine, string instrument, string name, byte[] data, string patterneditor, byte[] patterneditordata, int trackcount, float x, float y)
        {
            Do(new CreateMachineAction(reBuzzCore, machine, instrument, name, data, patterneditor, patterneditordata, trackcount, x, y));
        }

        public void DeleteMachines(IEnumerable<IMachine> m)
        {
            var dm = m.Where(machine => machine.DLL.Info.Type != MachineType.Master);
            if (dm.Count() > 0)
            {
                Do(new DeleteMachinesAction(reBuzzCore, dm));
            }
        }

        public void DisconnectMachines(IMachineConnection mc)
        {
            Do(new DisconnectMachinesAction(reBuzzCore, mc));
        }

        public void Do(IAction a)
        {
            ActionStack.Do(a);
        }

        public void DoubleClick(int x, int y)
        {

        }

        public void EndActionGroup()
        {
            ActionStack.EndActionGroup();
        }

        public void BeginImport(IDictionary<string, string> machinerename)
        {
            initImportDone = false;
            Importing = true;
            DictInitData.Clear();
            this.importDictionary = machinerename;
        }

        public void EndImport()
        {
            foreach (var machine in MachinesList.Where(m => !m.Hidden))
            {
                reBuzzCore.MachineManager.ImportFinished(machine, importDictionary);

                InitImport();
            }
            Importing = false;
            DictInitData.Clear();
        }

        private void InitImport()
        {
            if (!initImportDone)
            {
                foreach (var machine in DictInitData.Keys.Where(m => !m.Hidden))
                {
                    if (!machine.DLL.IsManaged && !machine.DLL.IsMissing)
                    {
                        var idata = DictInitData[machine];
                        BuzzCore.MachineManager.CallInit(machine, idata.data, idata.tracks);
                    }
                }
                initImportDone = true;
            }
        }

        public void ImportSong(float x, float y)
        {
            BuzzCore.ImportSong(x, y);
        }

        public void InsertMachine(IMachineConnection m, int id, float x, float y)
        {
            Do(new InsertMachineAction(BuzzCore, m, id, x, y));
        }

        public void InsertMachine(IMachineConnection m, string machineName, string instrument, float x, float y)
        {
            Do(new InsertMachineAction(BuzzCore, m, machineName, instrument, x, y));
        }

        public void MoveMachines(IEnumerable<Tuple<IMachine, Tuple<float, float>>> mm)
        {
            Do(new MoveMachinesAction(reBuzzCore, mm));
        }

        public void QuickNewMachine(char firstch)
        {

        }

        public void Redo()
        {
            if (CanRedo)
                ActionStack.Redo();
        }

        public void ReplaceMachine(IMachine m, int id, float x, float y)
        {
            Do(new ReplaceMachineAction(reBuzzCore, m, id, x, y));
        }

        public void ReplaceMachine(IMachine m, string machine, string instrument, float x, float y)
        {
            Do(new ReplaceMachineAction(reBuzzCore, m, machine, instrument, x, y));
        }

        public void SetConnectionChannel(IMachineConnection mc, bool destination, int channel)
        {
            lock (ReBuzzCore.AudioLock)
            {
                var conn = (mc as MachineConnectionCore);
                if (destination)
                {
                    conn.DestinationChannel = channel;
                }
                else
                {
                    conn.SourceChannel = channel;
                }
            }
        }

        public void SetConnectionParameter(IMachineConnection mc, int index, int oldvalue, int newvalue)
        {
            lock (ReBuzzCore.AudioLock)
            {
                var conn = (mc as MachineConnectionCore);
                if (index == 0) // amp
                {
                    conn.Amp = newvalue;
                }
                else if (index == 1 && conn.HasPan)
                {
                    conn.Pan = newvalue;
                }
            }
        }

        public event Action<int, int> ShowMachineViewContextMenu;
        public void ShowContextMenu(int x, int y)
        {
            // ToDo: pos
            if (ShowMachineViewContextMenu != null)
            {
                ShowMachineViewContextMenu.Invoke(x, y);
            }
            //reBuzzCore.MachineView.ContextMenu.IsOpen = true;
        }

        public void Undo()
        {
            if (ActionStack.CanUndo)
            {
                ActionStack.Undo();
            }
        }

        internal void InvokeMachineAdded(MachineCore machine)
        {
            MachineAdded?.Invoke(machine);
            Buzz.SetModifiedFlag();
        }

        internal void InvokeConnectionAdded(MachineConnectionCore mc)
        {
            ConnectionAdded?.Invoke(mc);
            (mc.Destination as MachineCore).RaiseTrackCount();

            Buzz.SetModifiedFlag();
        }

        internal void InvokeConnectionRemoved(MachineConnectionCore mc)
        {
            ConnectionRemoved?.Invoke(mc);
            (mc.Destination as MachineCore).RaiseTrackCount();
            Buzz.SetModifiedFlag();
        }

        internal void RemoveMachine(MachineCore machine)
        {
            if (machine == null)
                return;

            if (!machine.Hidden)
            {
                try
                {
                    MachineRemoved?.Invoke(machine);
                }
                catch (Exception e)
                {
                    Buzz.DCWriteLine(e.Message);
                }

            }
            lock (ReBuzzCore.AudioLock)
            {
                MachinesList.Remove(machine);
            }
        }

        internal void UpdateSoloMode()
        {
            bool solo = false;
            foreach (MachineCore machine in MachinesList)
            {
                if (machine.IsSoloed)
                {
                    solo = true;
                    break;
                }
            }

            this.SoloMode = solo;
        }

        #endregion

    }
}
