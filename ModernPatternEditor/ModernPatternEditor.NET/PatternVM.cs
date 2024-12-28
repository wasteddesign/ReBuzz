using BuzzGUI.Common;
using BuzzGUI.Common.Actions.PatternActions;
using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using WDE.ModernPatternEditor.MPEStructures;
using static WDE.ModernPatternEditor.MPEStructures.MPEPattern;

namespace WDE.ModernPatternEditor
{
    public class PatternVM : INotifyPropertyChanged
    {
        IPattern pattern;
        public IPattern Pattern
        {
            get { return pattern; }
            set
            {
                if (pattern != null)
                {
                    pattern.PropertyChanged -= pattern_PropertyChanged;
                    pattern.Machine.PropertyChanged -= Machine_PropertyChanged;

                    ReleaseColumnSets();
                }

                pattern = value;

                if (pattern != null)
                {
                    pattern.PropertyChanged += pattern_PropertyChanged;
                    pattern.Machine.PropertyChanged += Machine_PropertyChanged;

                    DefaultRPB = Editor.MPEPatternsDB.GetMPEPattern(pattern).RowsPerBeat;
                    rowNumberColumnSet = new RowNumberColumnSet(DefaultRPB);
                    RowNumberColumnSet.BeatCount = BeatCount;
                    PatternData pd = new PatternData();
                    CreateColumnSets();
                }

                PropertyChanged.Raise(this, "Pattern");
            }
        }

        void Machine_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "TrackCount":
                    CreateColumnSets();
                    break;

                case "Name":
                    CreateColumnSets();
                    break;
            }
        }

        void pattern_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "Length":
                    RowNumberColumnSet.BeatCount = BeatCount;
                    foreach (var cs in columnSets) cs.BeatCount = BeatCount;
                    CursorPosition = CursorPosition.Constrained;
                    Selection = Selection.Constrained;
                    Editor.MPEPatternsDB.GetMPEPattern(Pattern).SetBeatCount(BeatCount);
                    PropertyChanged.Raise(this, "BeatCount");
                    PropertyChanged.Raise(this, "UndoableBeatCount");
                    break;

                case "Name":
                    PropertyChanged.Raise(this, e.PropertyName);
                    break;

            }


        }

        public PatternEditor Editor { get; }
        public MachineVM MachineVM { get; private set; }

        public PatternVM(MachineVM mvm, PatternEditor pe)
        {
            DefaultRPB = 4;

            this.Editor = pe;
            this.MachineVM = mvm;
            cursorPosition = new Digit(this);
            selection = new Selection(this);
        }

        void CreateColumnSets()
        {
            ReleaseColumnSets();
            columnSets = new List<ParameterColumnSet>();

            foreach (var pSet in Editor.MPEPatternsDB.GetMPEPattern(pattern).GetParameterSets())
            {
                //CreateColumnSet(pSet.parameters.ToArray(), pSet.track, pSet.name);

                CreateColumnSet(pSet);
            }

            /*
			CreateColumnSet(pattern.Machine.ParameterGroups[1].Parameters, 0, pattern.Machine.Name + "\nGlobal");
			for (int track = 0; track < pattern.Machine.ParameterGroups[2].TrackCount; track++)
			{
				CreateColumnSet(pattern.Machine.ParameterGroups[2].Parameters, track, pattern.Machine.Name + string.Format("\nTrack {0}", track));
			}
			*/

            CursorPosition = CursorPosition.Constrained;
            Selection = Selection.Constrained;
            PropertyChanged.Raise(this, "ColumnSets");
        }

        void CreateColumnSet(MPEParameterSet pSet)
        {
            var parameters = pSet.parameters;
            if (parameters.Count == 0) return;

            var mpePattern = Editor.MPEPatternsDB.GetMPEPattern(pattern);

            var cl = new List<IPatternColumn>();

            foreach (var p in parameters)
            {
                var ic = mpePattern.Columns.FirstOrDefault(c => c.Parameter == p && c.Track == pSet.track && c.Machine == p.Group.Machine);
                if (ic == null)
                {
                    if (p is MPEInternalParameter)
                    {
                        ic = new MPEPatternColumnBuzz(PatternColumnType.MIDI, Pattern, ((MPEInternalParameter)p).Machine, p, pSet.track);
                    }
                    else
                    {
                        mpePattern.InsertColumn(mpePattern.Columns.Count, p, pSet.track);
                        ic = mpePattern.Columns.Last();
                    }
                }

                cl.Add(ic);
            }

            columnSets.Add(new ParameterColumnSet(this, cl, pSet.track) { BeatCount = this.BeatCount, Label = pSet.name });
        }

        void CreateColumnSet(IEnumerable<IParameter> parameters, int track, string label)
        {
            if (parameters.Count() == 0) return;

            var cl = new List<IPatternColumn>();

            foreach (var p in parameters)
            {
                var ic = pattern.Columns.FirstOrDefault(c => c.Parameter == p && c.Track == track && c.Machine == p.Group.Machine);
                if (ic == null)
                {
                    if (p is MPEInternalParameter)
                    {
                        ic = new MPEPatternColumnBuzz(PatternColumnType.MIDI, Pattern, ((MPEInternalParameter)p).Machine, p, track);
                    }
                    else
                    {
                        pattern.InsertColumn(pattern.Columns.Count, p, track);
                        ic = pattern.Columns.Last();
                    }
                }

                cl.Add(ic);
            }

            columnSets.Add(new ParameterColumnSet(this, cl, track) { BeatCount = this.BeatCount, Label = label });
        }

        void ReleaseColumnSets()
        {
            if (columnSets == null) return;
            foreach (var cs in columnSets) cs.Release();
            columnSets = null;
        }

        public int BeatCount
        {
            get { return (pattern.Length + PatternControl.BUZZ_TICKS_PER_BEAT - 1) / PatternControl.BUZZ_TICKS_PER_BEAT; }
            internal set
            {
                if (pattern.Length != value * PatternControl.BUZZ_TICKS_PER_BEAT)
                {
                    pattern.Length = value * PatternControl.BUZZ_TICKS_PER_BEAT;
                    PropertyChanged.Raise(this, "BeatCount");
                }
            }
        }

        public int UndoableBeatCount
        {
            get { return BeatCount; }
            set
            {
                if (value != BeatCount)
                {
                    using (new ActionGroup(Editor.EditContext.ActionStack))
                    {
                        MachineVM.Editor.DoAction(new MPESetPatternLengthAction(Editor.MPEPatternsDB.GetMPEPattern(Pattern), BeatCount, value));
                        // Editor.MPEPatternsDB.GetMPEPattern(Pattern).SetBeatCount(value); // Undoable set length?
                        //MachineVM.Editor.DoAction(new SetLengthAction(pattern, value * Global.Buzz.TPB));

                    }
                    PropertyChanged.Raise(this, "UndoableBeatCount");
                }
            }
        }

        public string Name { get { return Pattern.Name; } }

        RowNumberColumnSet rowNumberColumnSet;// = new RowNumberColumnSet();
        public RowNumberColumnSet RowNumberColumnSet { get { return rowNumberColumnSet; } }

        List<ParameterColumnSet> columnSets;
        public IList<ParameterColumnSet> ColumnSets { get { return columnSets; } }

        public ColumnRenderer.IColumnSet GetColumnSet(Digit p) { return ColumnSets[p.ColumnSet]; }
        public ColumnRenderer.IColumn GetColumn(Digit p) { return ColumnSets[p.ColumnSet].Columns[p.Column]; }
        public ColumnRenderer.IBeat GetBeat(Digit p) { return GetColumn(p).FetchBeat(p.Beat); }
        public ColumnRenderer.BeatValueType GetValueType(Digit p) { return GetBeat(p).Rows[p.RowInBeat].Type; }
        public int GetValue(Digit p) { return GetBeat(p).Rows[p.RowInBeat].Value; }
        public string GetValueString(Digit p) { return GetBeat(p).Rows[p.RowInBeat].ValueString; }

        public Digit GetColumnDigit(ColumnRenderer.IColumn c)
        {
            var d = new Digit(this, columnSets.FindIndex(cs => cs.Columns.Contains(c)), 0, 0, 0, 0);
            d = d.SetColumn(columnSets[d.ColumnSet].Columns.IndexOf(c));
            return d;
        }

        public Digit GetColumnSetDigit(ColumnRenderer.IColumnSet cs)
        {
            return new Digit(this, columnSets.IndexOf((ParameterColumnSet)cs), 0, 0, 0, 0);
        }


        Digit cursorPosition;
        public Digit CursorPosition
        {
            get { return cursorPosition; }
            set
            {
                //if (value != cursorPosition)
                {
                    cursorPosition = value;
                    PropertyChanged.Raise(this, "CursorPosition");
                }
            }
        }

        Selection selection;
        public Selection Selection
        {
            get { return selection; }
            set
            {
                //if (value != cursorPosition)
                {
                    selection = value;
                    PropertyChanged.Raise(this, "Selection");
                }
            }
        }

        public double ScrollPosition = 0;

        public PatternEditorSettings BindableSettings { get { return PatternEditor.Settings; } }

        public int DefaultRPB { get; internal set; }

        public event PropertyChangedEventHandler PropertyChanged;


    }
}
