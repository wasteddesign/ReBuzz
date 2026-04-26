using BuzzGUI.Common;
using BuzzGUI.Common.Actions.MachineActions;
using BuzzGUI.Common.Settings;
using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Markup;

namespace WDE.ModernPatternEditor
{
    public class MachineVM : INotifyPropertyChanged
    {
        IMachine machine;
        public IMachine Machine
        {
            get { return machine; }
            set
            {
                if (machine != null)
                {
                    machine.PropertyChanged -= machine_PropertyChanged;
                    machine.PatternAdded -= machine_PatternAdded;
                    machine.PatternRemoved -= machine_PatternRemoved;
                    RemoveAllPatterns();
                }

                machine = value;

                if (machine != null)
                {
                    machine.PropertyChanged += machine_PropertyChanged;
                    machine.PatternAdded += machine_PatternAdded;
                    machine.PatternRemoved += machine_PatternRemoved;

                    //BaseOctave = PatternEditor.Settings.DefaultBaseOctave ? 3 : machine.BaseOctave;

                    AddAllPatterns();
                }

                PropertyChanged.Raise(this, "Machine");
            }
        }

        //int baseOctave;
        //public int BaseOctave { get => baseOctave; set { baseOctave = value; PropertyChanged.Raise(this, "BaseOctave"); } }
        //public int BaseOctave { get => machine.BaseOctave; set { machine.BaseOctave = value; PropertyChanged.Raise(this, "BaseOctave"); } }

        void machine_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "TrackCount")
            {
                PropertyChanged.Raise(this, "UndoableTrackCount");
            }
            else if (e.PropertyName == "Patterns")
            {
                SortAllPatterns();
                PropertyChanged.Raise(this, "Machine");
            }
            //else if (e.PropertyName == "BaseOctave")
            //{
            //    PropertyChanged.Raise(this, "BaseOctave");
            //}
        }

        void machine_PatternRemoved(IPattern p)
        {
            Editor.TargetMachine_PatternRemoved(p);
            var vm = Patterns.First(x => x.Pattern == p);
            vm.Pattern = null;
            bool wasselected = SelectedPattern == vm;
            patterns.Remove(vm);
            if (wasselected) SelectedPattern = patterns.LastOrDefault();
        }

        void machine_PatternAdded(IPattern p)
        {
            Editor.TargetMachine_PatternAdded(p);
            var vm = new PatternVM(this, Editor) { Pattern = p };
            patterns.Add(vm);
            SelectedPattern = vm;
        }
        void RemoveAllPatterns()
        {
            foreach (var p in patterns)
            {
                p.Pattern = null;
            }

            patterns.Clear();
            SelectedPattern = null;
        }

        void AddAllPatterns()
        {
            foreach (var p in machine.Patterns.OrderBy(x => x.Name))
            {
                var vm = new PatternVM(this, Editor) { Pattern = p };
                patterns.Add(vm);
            }

            SelectedPattern = patterns.FirstOrDefault();
        }

        void SortAllPatterns()
        {
            if (SelectedPattern != null)
            {
                var pattern = SelectedPattern.Pattern;
                patterns.Clear();
                foreach (var p in machine.Patterns.OrderBy(x => x.Name))
                {
                    var vm = new PatternVM(this, Editor) { Pattern = p };
                    patterns.Add(vm);
                }
                SelectedPattern = Patterns.FirstOrDefault(x => x.Pattern == pattern);
            }
        }

        public PatternEditor Editor { get; private set; }

        public MachineVM(PatternEditor editor)
        {
            this.Editor = editor;
        }

        public ObservableCollection<PatternVM> patterns = new ObservableCollection<PatternVM>();
        public ObservableCollection<PatternVM> Patterns { get { return patterns; } }

        PatternVM selectedPattern;
        public PatternVM SelectedPattern
        {
            get { return selectedPattern; }
            set
            {   
                selectedPattern = value;
                PropertyChanged.Raise(this, "SelectedPattern");
                PropertyChanged.Raise(this, "HasSelectedPattern");
            }
        }

        public bool HasSelectedPattern { get { return SelectedPattern != null; } }

        public string Name { get { return Machine.Name; } }
        public IEnumerable<int> BaseOctaves { get { return Enumerable.Range(0, 10); } }

        public int MinTracks { get { return Machine.DLL.Info.MinTracks; } }
        public int MaxTracks { get { return Machine.DLL.Info.MaxTracks; } }
        public bool CanChangeTrackCount { get { return MinTracks != MaxTracks; } }

        public int UndoableTrackCount
        {
            get { return Machine.TrackCount; }
            set
            {
                if (value != Machine.TrackCount)
                {
                    //lock (Editor.syncLock)
                    {   
                        Editor.DoAction(new SetTrackCountAction(Machine, value));
                    }
                }
            }
        }


        #region INotifyPropertyChanged Members
#pragma warning disable 0067
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore 0067
        #endregion

    }
}
