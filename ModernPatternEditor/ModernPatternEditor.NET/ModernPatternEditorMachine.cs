using Accessibility;
using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Common.Templates;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Xml.Linq;
using WDE.ModernPatternEditor.MPEStructures;

namespace WDE.ModernPatternEditor
{
    [MachineDecl(Name = "Modern Pattern Editor", ShortName = "MPE", Author = "WDE", MaxTracks = 8, InputCount = 0, OutputCount = 0)]
    public class ModernPatternEditorMachine : IBuzzMachine, INotifyPropertyChanged, IGUICallbacks
    {
        public event PropertyChangedEventHandler PropertyChanged;

        IBuzzMachineHost host;
        PatternEditor ModernPatternEditor { get; set; }
        public ModernPatternEditorMachine(IBuzzMachineHost host)
        {
            ModernPatternEditor = new PatternEditor(this);
            ModernPatternEditor.Song = Global.Buzz.Song;
            this.host = host;
        }

        [ParameterDecl()]
        public bool Dummy { get; set; }

        public void Work()
        {
            SongTime songTime = new SongTime();
            songTime.PosInSubTick = host.SubTickInfo.PosInSubTick;

            ModernPatternEditor.Work(songTime);
        }

        public void ImportFinished(IDictionary<string, string> machineNameMap)
        {
            PatternEditorUtils.MachineNameMap = machineNameMap;

            // Redo everyting for this machine in case machine names have been changed after import
            
            var currentMachine = ModernPatternEditor.TargetMachine;
            if (currentMachine != null)
            {
                ModernPatternEditor.TargetMachine = null;

                SetTargetMachine(currentMachine);
            }
        }

        // Add this if Pattern Editor Machine
        public UserControl PatternEditorControl()
        {
            return ModernPatternEditor;
        }

        public void SetEditorPattern(IPattern pattern)
        {
            SetTargetMachine(pattern.Machine);
            ModernPatternEditor.SetEditorPattern(pattern);
        }

        public void RecordControlChange(IParameter parameter, int track, int value)
        {
            if (ModernPatternEditor != null)
            {
                IMachine machine = parameter.Group.Machine;
                ModernPatternEditor.RecordControlChange(parameter, track, value);
            }
        }

        public void SetTargetMachine(IMachine machine)
        {
            ModernPatternEditor.SetTargetMachine(machine);
        }

        public string GetEditorMachine()
        {
            return "Modern Pattern Editor";
        }

        public void SetPatternEditorMachine(IMachineDLL editorMachine)
        {
            
        }

        public void SetPatternName(string machine, string oldName, string newName)
        {
            var mac = Global.Buzz.Song.Machines.FirstOrDefault(m => m.Name == machine);
            if (mac != null)
            {
                mac.RenamePattern(mac.Patterns.FirstOrDefault(p => p.Name == oldName), newName);
            }
        }

        public int GetTicksPerBeatDelegate(IPattern pattern, int playPosition)
        {
            // We need column to get the specific RPB. Return the defaul for the pattern
            var p = ModernPatternEditor.MPEPatternsDB.GetMPEPattern(pattern);
            if (p != null)
            {
                return ModernPatternEditor.MPEPatternsDB.GetMPEPattern(pattern).RowsPerBeat;
            }
            else
            {
                return MPEPatternColumn.BUZZ_TICKS_PER_BEAT;
            }
        }

        public void ControlChange(IMachine machine, int group, int track, int param, int value)
        {
            group &= ~(16);
            machine.ParameterGroups[group].Parameters[param].SetValue(track | 1 << 16, value);
        }

        public void SetModifiedFlag()
        {
            Global.Buzz.SetModifiedFlag();
        }

        public class State : INotifyPropertyChanged
        {

            public event PropertyChangedEventHandler PropertyChanged;
        }

        public void SetPatternEditorData(byte[] data)
        {
            ModernPatternEditor.SetPatternEditorData(data);
        }

        public byte[] GetPatternEditorData()
        {
            return ModernPatternEditor.GetPatternEditorData();
        }

        State machineState = new State();
        public State MachineState           // a property called 'MachineState' gets automatically saved in songs and presets
        {
            get { return machineState; }
            set
            {
                machineState = value;
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("MachineState"));
            }
        }

        public bool CanExecuteCommand(BuzzCommand cmd)
        {
            return ModernPatternEditor.CanExecuteCommand(cmd);
        }

        public void ExecuteCommand(BuzzCommand cmd)
        {
            ModernPatternEditor.ExecuteCommand(cmd);
        }

        public void MidiNote(int channel, int value, int velocity)
        {
            ModernPatternEditor.MidiNote(channel, value, velocity);
        }

        public void MidiControlChange(int ctrl, int channel, int value)
        {
            ModernPatternEditor.MidiControlChange(ctrl, channel, value);
        }

        public int[] GetPatternEditorMachineMIDIEvents(IPattern pattern)
        {
            return ModernPatternEditor.ExportMidiEvents(pattern);
        }

        public void SetPatternEditorMachineMIDIEvents(IPattern pattern, int[] data)
        {
            ModernPatternEditor.ImportMidiEvents(pattern, data);
        }

        public IEnumerable<IPatternEditorColumn> GetPatternEditorEvents(IPattern pattern, int tbegin, int tend)
        {
            return ModernPatternEditor.GetPatternColumnEvents(pattern, tbegin, tend);
        }

        public void Activate()
        {
            ModernPatternEditor.Activate();
        }

        public void Release()
        {
            ModernPatternEditor.Release();
        }

        public void CreatePatternCopy(IPattern pnew, IPattern p)
        {
            ModernPatternEditor.CreatePatternCopy(pnew.Name, p.Name);
        }

        // Update wave references if song/template was imported
        public void UpdateWaveReferences(IPattern pattern, IDictionary<int, int> remap)
        {
            var mpePattern = ModernPatternEditor.MPEPatternsDB.GetMPEPattern(pattern);
            foreach (var column in mpePattern.MPEPatternColumns)
            {
                if (column.Parameter.Flags.HasFlag(ParameterFlags.Wave))
                {
                    var events = column.GetEvents(0, int.MaxValue).ToArray();
                    List<PatternEvent> newEvents = new List<PatternEvent>();
                    for (int i = 0; i < events.Count(); i++)
                    {
                        var e = events[i];
                        int wave = e.Value - 1;
                        if (remap.ContainsKey(wave))
                        {   
                            e.Value = remap[wave] + 1;
                            newEvents.Add(e);
                        }
                    }

                    column.SetEvents(newEvents.ToArray(), false, false);
                    column.SetEvents(newEvents.ToArray(), true, false);
                }
            }
        }
    }
}
