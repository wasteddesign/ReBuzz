using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.Common.Templates;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using static WDE.ModernPatternEditor.PatternEditorUtils;
using System.Reflection;
using WDE.ModernPatternEditor.ColumnRenderer;

namespace WDE.ModernPatternEditor.MPEStructures
{
    public class MPEPattern
    {
        private int rpb;
        public int RowsPerBeat
        {
            get { return rpb; }
            internal set
            {
                rpb = value;
                foreach (var c in MPEPatternColumns)
                {
                    c.SetRPB(rpb);
                }
            }
        }


        // Key = Visual Index
        public Dictionary<int, MPEPatternColumn> MPEPatternColumnsDict = new Dictionary<int, MPEPatternColumn>();

        public List<MPEPatternColumn> MPEPatternColumns = new List<MPEPatternColumn> { };

        public PatternEditor Editor { get; }
        public string PatternName { get; internal set; }

        private IPattern pattern;
        public IPattern Pattern
        {
            get { return pattern; }
            internal set
            {
                if (pattern != null)
                {
                    pattern.Machine.PropertyChanged -= Machine_PropertyChanged;
                    pattern.PropertyChanged -= Pattern_PropertyChanged;

                    foreach (var c in MPEPatternColumns)
                    {
                        c.Machine = null;
                        c.Parameter = null;
                    }
                    MPEPatternColumns.Clear();
                    MPEPatternColumnsDict.Clear();
                }

                pattern = value;

                if (pattern != null)
                {
                    pattern.Machine.PropertyChanged += Machine_PropertyChanged;
                    pattern.PropertyChanged += Pattern_PropertyChanged;
                    UpdateData();
                }
            }
        }

        internal List<IPatternColumn> Columns = new List<IPatternColumn>();

        private void Pattern_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "Name":
                    IPattern pattern = (IPattern)sender;
                    // Update mainDB
                    Editor.MPEPatternsDB.PatternRenamed(pattern);
                    break;
                case "Lenght":
                    foreach (var column in MPEPatternColumns)
                        column.UpdateLength();
                    break;
            }
        }

        internal IList<MPEPatternColumn> AddColumnToGroup(IParameter param)
        {
            List<MPEPatternColumn> retList = new List<MPEPatternColumn>();

            IMachine paramMachine = param.Group.Machine;
            int numTracks = paramMachine == Editor.SelectedMachine.Machine ?
                paramMachine.TrackCount : 1;

            if (param.Group.Type == ParameterGroupType.Track)
            {
                for (int i = 0; i < numTracks; i++)
                {
                    var t = CreateNewColumn(param, i);
                    retList.Add(t);
                }
            }
            else
            {
                var t = CreateNewColumn(param, 0);
                retList.Add(t);
            }

            UpdateData();

            return retList;
        }

        internal MPEPatternColumn CreateNewColumn(IParameter param, int track)
        {
            int index = MPEPatternColumns.Count;
            return CreateNewColumn(param, track, index);
        }

        internal MPEPatternColumn CreateNewColumn(IParameter param, int track, int columnIndex)
        {
            MPEPatternColumn column = new MPEPatternColumn(this);
            column.Parameter = param;
            column.Machine = param.Group.Machine;
            column.Graphical = false;
            column.GroupType = param.Group.Type;
            column.ParamTrack = track;

            column.SetBeatCount((int)Editor.lengthBox.Value);
            column.SetRPB(rpb);
            column.UpdateLength();

            MPEPatternColumns.Insert(columnIndex, column);
            MPEPatternColumnsDict[columnIndex] = column;

            return column;
        }

        public MPEPatternColumn? GetColumn(IPatternColumn column)
        {
            int key = GetParamIndex(column.Parameter, column.Track);
            if (MPEPatternColumnsDict.ContainsKey(key))
                return MPEPatternColumnsDict[key];
            else
                return null;
        }

        public MPEPatternColumn? GetColumn(int num)
        {
            if (MPEPatternColumnsDict.ContainsKey(num))
                return MPEPatternColumnsDict[num];
            else
                return null;
        }

        private void Machine_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "TrackCount":
                    UpdateData();
                    break;
            }
        }

        public void CreateDefaultColumns()
        {
            MPEPatternColumnsDict = new Dictionary<int, MPEPatternColumn>();
            MPEPatternColumns = new List<MPEPatternColumn>();

            var lastPattern = Editor.MPEPatternsDB.GetPatterns().LastOrDefault();
            if (lastPattern != null)
            {
                foreach (var lastCol in lastPattern.MPEPatternColumns)
                {
                    MPEPatternColumn column = lastCol.Clone(this);

                    column.SetBeatCount(lastPattern.pattern.Length / lastPattern.RowsPerBeat);
                    column.SetRPB(lastPattern.RowsPerBeat);
                    column.UpdateLength();

                    MPEPatternColumns.Add(column);
                    MPEPatternColumnsDict[MPEPatternColumns.IndexOf(column)] = column;
                }
            }
            else
            {
                //int index = 0;
                int uiIndex = 0;

                foreach (var parAndTrack in pattern.Machine.AllParametersAndTracks())
                {
                    MPEPatternColumn column = new MPEPatternColumn(this);
                    IParameter par = parAndTrack.Item1;
                    int track = parAndTrack.Item2;

                    column.Parameter = par;
                    //column.IndexInGroup = par.IndexInGroup;
                    //column.MachineName = par.Group.Machine.Name;
                    column.Machine = par.Group.Machine;
                    column.Graphical = false;
                    column.GroupType = par.Group.Type;
                    column.ParamTrack = track;
                    //column.ParamIndex = index;

                    column.SetBeatCount((int)Editor.lengthBox.Value);
                    column.SetRPB(rpb);
                    column.UpdateLength();

                    MPEPatternColumns.Add(column);
                    MPEPatternColumnsDict[uiIndex] = column;
                    //index++;
                    uiIndex++;
                }
            }
        }

        public void AddTrack(int trackNumber)
        {
            // Make a cooy of MPE track 0
            var trackColumns = MPEPatternColumns.FindAll(column => column.GroupType == ParameterGroupType.Track && column.ParamTrack == 0).ToList();

            foreach (var trackColumn in trackColumns)
            {
                IParameter par = trackColumn.Parameter;

                MPEPatternColumn column = new MPEPatternColumn(this);
                column.Parameter = par;
                //column.MachineName = par.Group.Machine.Name;
                column.Machine = par.Group.Machine;
                column.Graphical = false;
                column.GroupType = par.Group.Type;
                column.ParamTrack = trackNumber;

                column.SetBeatCount((int)Editor.lengthBox.Value);
                column.SetRPB(rpb);
                column.UpdateLength();

                int index = MPEPatternColumns.IndexOf(trackColumn) + trackColumns.Count;

                MPEPatternColumns.Insert(index, column);
            }
        }

        public void UpdateData()
        {
            // Don't mess with these when audio thread access them
            lock (Editor.syncLock)
            {
                if (this.RowsPerBeat == 0)
                    this.RowsPerBeat = 4;

                if (MPEPatternColumns.Count == 0)
                {
                    CreateDefaultColumns();
                    return;
                }

                MPEPatternColumnsDict = new Dictionary<int, MPEPatternColumn>();
                List<MPEPatternColumn> newColumnList = new List<MPEPatternColumn>();

                // Remove deleted tracks
                foreach (var col in MPEPatternColumns)
                {
                    if (col.GroupType == ParameterGroupType.Track && col.ParamTrack >= pattern.Machine.TrackCount)
                        continue;
                    else
                    {   
                        if (col.BeatRowsList.Count == 0)
                        {
                            col.SetRPB(rpb);
                        }
                        if (col.GetEvents(0, int.MaxValue).Count() > 0)
                        {

                        }
                        newColumnList.Add(col);
                    }   
                }
                MPEPatternColumns = newColumnList;

                // Add added tracks
                int numTracks = MPEPatternColumns.Max(x => x.ParamTrack);
                numTracks++;
                while (numTracks < pattern.Machine.TrackCount)
                {
                    AddTrack(numTracks);
                    numTracks++;
                }

                MPEPatternColumns = MPEPatternColumns.OrderBy(x => x.Machine.ParameterGroups.IndexOf(x.Parameter.Group))
                    .ThenBy(x => x.Machine == this.Pattern.Machine ? 0 : 1)
                    .ThenBy(x => x.Machine.Name)
                    .ThenBy(x => x.ParamTrack)
                    .ThenBy(x => x.VisualIndexInGroup).ToList();

                int visualIndex = 0;
                foreach (var mpecolumn in MPEPatternColumns)
                {
                    MPEPatternColumnsDict[visualIndex] = mpecolumn;
                    visualIndex++;
                }

                // Beat Rows
                foreach (var col in MPEPatternColumns)
                {
                    col.UpdateLength();
                }
            }
        }

        internal void SetBeatCount(int beats)
        {
            foreach (var col in MPEPatternColumns)
            {
                col.SetBeatCount(beats);
            }
        }

        internal MPEPatternColumn GetColumnOrCreate(IPatternColumn patternColumn)
        {
            MPEPatternColumn ret;
            ret = MPEPatternColumns.FirstOrDefault(x => x.Parameter == patternColumn.Parameter && x.ParamTrack == patternColumn.Track && x.Machine == patternColumn.Machine);
            return ret;
        }

        public int GetParamIndex(IParameter parameter, int track)
        {
            int ret = 0;

            foreach (var column in MPEPatternColumns)
            {
                if (column.Parameter == parameter && column.ParamTrack == track)
                    break;
                ret++;
            }
            return ret;
        }

        public MPEPattern(PatternEditor editor, string name)
        {
            this.Editor = editor;
            this.PatternName = name;
        }

        internal void Quantize()
        {
            foreach (var c in MPEPatternColumns)
            {
                Dictionary<int, PatternEvent> dict = new Dictionary<int, PatternEvent>();
                var e = c.GetEvents(0, pattern.Length * PatternEvent.TimeBase).ToArray();
                // Clear
                c.SetEvents(e, false);

                for (int i = 0; i < e.Length; i++)
                {
                    e[i].Time = c.GetTimeQuantized(e[i].Time);
                    dict[e[i].Time] = e[i];
                }

                // Set
                c.SetEvents(dict.Values.ToArray(), true);
            }
        }

        internal class MPEParameterSet
        {
            internal IList<IParameter> parameters;
            internal int track;
            internal string name;
            internal ParameterGroupType groupType;
            internal IMachine machine;

            internal MPEParameterSet()
            {
                parameters = new List<IParameter>();
                track = -1;
            }
        }

        internal IEnumerable<MPEParameterSet> GetParameterSets()
        {
            IList<MPEParameterSet> parameterSets = new List<MPEParameterSet>();

            MPEParameterSet set = new MPEParameterSet();

            foreach (var column in MPEPatternColumns.Where(c => c.GroupType != ParameterGroupType.Input))
            {
                if (set.groupType != column.GroupType || set.track != column.ParamTrack || set.machine != column.Machine)
                {
                    if (set.parameters.Count > 0)
                    {
                        parameterSets.Add(set);
                    }

                    set = new MPEParameterSet();
                    set.track = column.ParamTrack;
                    set.name = column.Machine.Name;
                    set.groupType = column.GroupType;
                    set.machine = column.Machine;
                    if (column.GroupType == ParameterGroupType.Global)
                        set.name += "\nGlobal";
                    else if (column.GroupType == ParameterGroupType.Track)
                        set.name += "\nTrack " + set.track;
                    set.parameters.Add(column.Parameter);
                }
                else
                {
                    set.parameters.Add(column.Parameter);
                }
            }

            if (set.parameters.Count > 0)
            {
                parameterSets.Add(set);
            }

            return parameterSets.ToArray();
        }

        internal MPEPatternColumn GetColumn(IMachine machine, int indexInGroup, int track)
        {
            return MPEPatternColumns.FirstOrDefault(x => x.Machine == machine && x.Parameter.Group.Type == ParameterGroupType.Track && x.Parameter.IndexInGroup == indexInGroup && x.ParamTrack == track);
        }

        internal void InsertColumn(int index, IParameter p, int track)
        {
            PatternColumnType type = p.IndexInGroup == (int)InternalParameter.MidiNote || p.IndexInGroup == -1 ? PatternColumnType.MIDI : PatternColumnType.Parameter;
            ReBuzzPatternColumn pcc = new ReBuzzPatternColumn(this, type, pattern, p.Group.Machine, p, track, null, null);
            Columns.Insert(index, pcc);
            //ColumnAdded?.Invoke(pcc);
        }
    }
}
