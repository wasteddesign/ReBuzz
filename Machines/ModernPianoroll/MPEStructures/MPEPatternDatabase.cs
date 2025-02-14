using BuzzGUI.Interfaces;
using Pianoroll.GUI;
using System.ComponentModel;

namespace WDE.ModernPatternEditor.MPEStructures
{
    internal class MPEPatternDatabase
    {
        private Dictionary<string, MPEPattern> PatternToMPEPattern = new Dictionary<string, MPEPattern>();
        private List<MPEPattern> MPEPatternList = new List<MPEPattern>();
        private IMachine machine;
        //private List<XMLPattern> ImportedPatterns = new List<XMLPattern>();

        public IMachine Machine
        {
            get { return machine; }
            set
            {
                if (machine != null)
                {
                    machine.PropertyChanged -= Machine_PropertyChanged;
                }
                machine = value;

                if (machine != null)
                {
                    machine.PropertyChanged += Machine_PropertyChanged;

                    foreach (var mpepat in MPEPatternList)
                    {
                        mpepat.Pattern = machine.Patterns.FirstOrDefault(x => x.Name == mpepat.PatternName);
                        if (mpepat.Pattern != null)
                            PatternToMPEPattern[mpepat.Pattern.Name] = mpepat;
                    }
                }
            }
        }


        public Editor PianorollEditor { get; }

        public MPEPatternDatabase(Editor editor)
        {
            this.PianorollEditor = editor;
            ParamterMuted = new Dictionary<ParameterTrack, bool>();
        }

        private void Machine_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "TrackCount":
                    {
                        foreach (var mpepat in MPEPatternList)
                        {
                            //mpepat.UpdateData();
                        }
                    }
                    break;
            }
        }

        internal void PatternRenamed(IPattern pattern)
        {
            foreach (var mpePattern in MPEPatternList)
            {
                if (mpePattern.Pattern == pattern)
                {
                    PatternToMPEPattern.Remove(mpePattern.PatternName);
                    mpePattern.PatternName = pattern.Name;
                    PatternToMPEPattern.Add(mpePattern.PatternName, mpePattern);
                    break;
                }
            }
        }

        public MPEPattern GetMPEPattern(IPattern p)
        {
            if (PatternToMPEPattern.ContainsKey(p.Name))
                return PatternToMPEPattern[p.Name];
            else
                return new MPEPattern(PianorollEditor, p.Name);
        }

        public void Release()
        {
            Machine = null;
            foreach (var p in PatternToMPEPattern.Values)
            {
                p.Pattern = null;
            }

            MPEPatternList.Clear();
            PatternToMPEPattern.Clear();
            //ImportedPatterns.Clear();
        }

        internal void AddPattern(IPattern pattern)
        {
            MPEPattern mpePattern = MPEPatternList.FirstOrDefault(x => x.PatternName == pattern.Name);
            if (mpePattern == null)
            {
                mpePattern = new MPEPattern(PianorollEditor, pattern.Name);
                var lastPattern = GetPatterns().LastOrDefault();
                if (lastPattern != null)
                {
                    mpePattern.RowsPerBeat = lastPattern.RowsPerBeat;
                }
                mpePattern.Pattern = pattern;
                MPEPatternList.Add(mpePattern);
            }
            else
            {
                mpePattern.Pattern = pattern;
            }
            PatternToMPEPattern.Add(pattern.Name, mpePattern);

            // If user imported patterns
            /*
            if (ImportedPatterns.Count > 0)
            {
                var xPattern = ImportedPatterns[0];
                ImportedPatterns.RemoveAt(0);

                mpePattern.SetBeatCount(xPattern.LenghtInBeats);
                mpePattern.RowsPerBeat = xPattern.RowsPerBeat;

                foreach (var c in xPattern.Columns)
                {
                    var mpeCol = mpePattern.MPEPatternColumns.FirstOrDefault(x => x.Parameter.Name == c.Parameter
                        && x.ParamTrack == c.Track && x.Machine.Name == c.Machine);
                    if (mpeCol == null)
                    {
                        IMachine mac = Global.Buzz.Song.Machines.FirstOrDefault(x => x.Name == c.Machine);
                        if (mac != null)
                        {
                            IParameter par = mac.AllParameters().FirstOrDefault(x => x.Name == c.Parameter);
                            mpeCol = mpePattern.CreateNewColumn(par, c.Track);
                        }
                    }

                    mpeCol.SetBeats(c.Beats.ToList());
                    mpeCol.SetEvents(c.Events, true, false);
                }
            }
            */

            if (PrepareToCopyPattern)
                DoCopyPattern();
        }


        internal void SetPatterns(List<MPEPattern> patterns)
        {
            MPEPatternList = patterns;
            foreach (var mpepat in MPEPatternList)
            {
                mpepat.Pattern = machine.Patterns.FirstOrDefault(x => x.Name == mpepat.PatternName);
                if (mpepat.Pattern != null)
                    PatternToMPEPattern[mpepat.Pattern.Name] = mpepat;
            }
        }

        internal IEnumerable<MPEPattern> GetPatterns()
        {
            return MPEPatternList;
        }

        internal void RemovePattern(IPattern pattern)
        {
            MPEPattern mpePattern = MPEPatternList.FirstOrDefault(x => x.PatternName == pattern.Name);
            if (mpePattern != null)
            {
                MPEPatternList.Remove(mpePattern);
                PatternToMPEPattern.Remove(pattern.Name);
            }
        }
        /*
        internal bool IsParameterEnabled(IParameter tPar)
        {
            // Need to check only the first one because all patterns have same columns
            var pattern = GetMPEPattern(PianorollEditor.Pattern);
            foreach (var column in pattern.MPEPatternColumns)
            {
                if (column.Parameter == tPar)
                    return true;
            }
            return false;
        }
        */

        private bool PrepareToCopyPattern { get; set; }
        private string CopyPatternOldName { get; set; }
        private string CopyPatternNewName { get; set; }

        internal void PrepareCopy(string newName, string oldName)
        {
            CopyPatternOldName = oldName;
            CopyPatternNewName = newName;
            PrepareToCopyPattern = true;
        }

        private void DoCopyPattern()
        {
            var oldPattern = Machine.Patterns.FirstOrDefault(x => x.Name == CopyPatternOldName);
            var newPattern = Machine.Patterns.FirstOrDefault(x => x.Name == CopyPatternNewName);
            if (oldPattern != null && newPattern != null)
            {
                var oldMPEPattern = GetMPEPattern(oldPattern);
                var newMPEPattern = GetMPEPattern(newPattern);

                newMPEPattern.RowsPerBeat = oldMPEPattern.RowsPerBeat;

                foreach (var oldCol in GetMPEPattern(oldPattern).MPEPatternColumns)
                {
                    int index = oldMPEPattern.MPEPatternColumns.IndexOf(oldCol);
                    var newCol = newMPEPattern.MPEPatternColumnsDict[index];
                    newCol.SetEvents(oldCol.GetEvents(0, oldPattern.Length * PatternEvent.TimeBase).ToArray(), true);
                }
            }
            PrepareToCopyPattern = false;
            CopyPatternOldName = CopyPatternNewName = "";
        }
        /*
        internal void PatternImported(XMLPattern xPattern)
        {
            ImportedPatterns.Add(xPattern);
        }
        */

        public struct ParameterTrack
        {
            public IParameter parameter;
            public int track;
        }

        public Dictionary<ParameterTrack, bool> ParamterMuted { get; set; }
        /*
        internal void SetColumnsMuteFlag(ParameterColumnSet pcs)
        {
            foreach(var c in pcs.Columns)
            {
                var c2 = (ParameterColumn)c;
                var parameter = c2.PatternColumn.Parameter;
                
                ParameterTrack pt = new ParameterTrack() { parameter = parameter, track = c2.PatternColumn.Track };
                ParamterMuted[pt] = pcs.Muted;
            }
        }
        */
        internal bool IsColumnMuted(IParameter parameter, int paramTrack)
        {
            bool ret = false;
            ParameterTrack pt = new ParameterTrack() { parameter = parameter, track = paramTrack };
            if (ParamterMuted.ContainsKey(pt))
                ret = ParamterMuted[pt];

            return ret;
        }
    }
}
