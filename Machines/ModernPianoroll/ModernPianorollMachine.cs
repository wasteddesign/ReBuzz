using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using Pianoroll.GUI;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using WDE.ModernPatternEditor;
using WDE.ModernPatternEditor.MPEStructures;

namespace WDE.Pianoroll
{
    [MachineDecl(Name = "Modern Pianoroll", ShortName = "Pianoroll", Author = "WDE", MaxTracks = 1)]
    public class ModernPianorollMachine : IBuzzMachine, INotifyPropertyChanged
    {
        IBuzzMachineHost host;
        Editor Pianoroll { get; set; }
        public ModernPianorollMachine(IBuzzMachineHost host)
        {
            this.host = host;
            Pianoroll = new Editor(this);
        }

        [ParameterDecl()]
        public bool Dummy { get; set; }

        public void Work()
        {
            SongTime songTime = new SongTime();
            songTime.PosInSubTick = host.SubTickInfo.PosInSubTick;

            Pianoroll.Work(songTime);
        }

        public void ImportFinished(IDictionary<string, string> machineNameMap)
        {
            PatternEditorUtils.MachineNameMap = machineNameMap;
        }

        // Add this if Pattern Editor Machine
        public UserControl PatternEditorControl()
        {
            return Pianoroll;
        }

        public void SetEditorPattern(IPattern pattern)
        {
            SetTargetMachine(pattern.Machine);
            Pianoroll.SetEditorPattern(pattern);
        }

        public void RecordControlChange(IParameter parameter, int track, int value)
        {
            if (Pianoroll != null)
            {
                IMachine machine = parameter.Group.Machine;
                Pianoroll.RecordControlChange(parameter.Group.Machine, machine.ParameterGroups.IndexOf(parameter.Group),
                    track, parameter.IndexInGroup, value);
            }
        }

        public void SetTargetMachine(IMachine machine)
        {
            if (this.Machine == null)
            {
                this.Machine = machine;
                Pianoroll.TargetMachine = machine;
                Pianoroll.MPEPatternsDB.Machine = machine;
                Pianoroll.MPEPatternsDB.SetPatterns(mpePatterns);
                Pianoroll.TargetMachineChanged();
            }
        }

        public string GetEditorMachine()
        {
            return "Modern Pianoroll";
        }

        public void SetPatternName(string machine, string oldName, string newName)
        {

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
            if (data != null)
            {
                mpePatterns = PatternEditorUtils.ProcessEditorData(Pianoroll, data);
            }
        }

        public byte[] GetPatternEditorData()
        {
            return PatternEditorUtils.CreatePatternXPPatternData(Pianoroll.MPEPatternsDB.GetPatterns());
        }

        State machineState = new State();
        private List<MPEPattern> mpePatterns;

        public event PropertyChangedEventHandler PropertyChanged;

        public State MachineState           // a property called 'MachineState' gets automatically saved in songs and presets
        {
            get { return machineState; }
            set
            {
                machineState = value;
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("MachineState"));
            }
        }

        public IMachine Machine { get; internal set; }

        public bool CanExecuteCommand(BuzzCommand cmd)
        {
            return Pianoroll.CanExecuteCommand(cmd);
        }

        public void ExecuteCommand(BuzzCommand cmd)
        {
            Pianoroll.ExecuteCommand(cmd);
        }

        public void MidiNote(int channel, int value, int velocity)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Pianoroll.MidiNote(value, velocity, true);
            });
        }

        public void MidiControlChange(int ctrl, int channel, int value)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Pianoroll.MidiControlChange(ctrl, channel, value);
            });
        }

        public int[] GetPatternEditorMachineMIDIEvents(IPattern pattern)
        {
            return Pianoroll.ExportMidiEvents(pattern.Name);
        }

        public IList<PatternEvent> GetPatternCloumnEvents(IPatternColumn column, int tbegin, int tend)
        {
            return Pianoroll.GetPatternColumnEvents(column, tbegin, tend);
        }

        public void WriteDC(string text)
        {
            Global.Buzz.DCWriteLine(text);
        }

        public int GetPlayPosition()
        {
            return Global.Buzz.Song.PlayPosition * 960;
        }

        public bool GetPlayNotesState()
        {
            return false;
        }

        public void MidiNotePR(int note, int velocity)
        {
            Machine.SendMIDINote(0, note, velocity);
        }

        public int GetBaseOctave()
        {
            return Machine != null ? Machine.BaseOctave : 0;
        }

        public bool IsMidiNoteImplemented()
        {
            return true;
        }

        public void GetGUINotes()
        {

        }

        public void GetRecordedNotes()
        {

        }

        public int GetStateFlags()
        {
            return 0;
        }

        public bool TargetSet()
        {
            return (Machine != null);
        }

        public void SetStatusBarText(int pane, string text)
        {

        }

        public void PlayNoteEvents(IEnumerable<NoteEvent> notes, bool play)
        {
            foreach (NoteEvent note in notes)
            {
                int velocity = play ? note.Velocity : 0;

                Pianoroll.TargetMachine.SendMIDINote(0, note.Note, velocity);
            }
        }

        public bool IsEditorWindowVisible()
        {
            return Global.Buzz.ActiveView == BuzzView.PatternView && Global.Buzz.Playing;
        }

        public string GetThemePath()
        {
            string path = System.IO.Path.Combine(Global.BuzzPath, "Themes");
            return System.IO.Path.Combine(path, Global.Buzz.SelectedTheme);
        }

        public string GetTargetMachine()
        {
            return Machine != null ? Machine.Name : "";
        }
    }
}
