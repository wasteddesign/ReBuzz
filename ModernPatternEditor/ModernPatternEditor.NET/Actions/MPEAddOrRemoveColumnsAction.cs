using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.Linq;
using WDE.ModernPatternEditor;
using WDE.ModernPatternEditor.MPEStructures;

namespace BuzzGUI.Common.Actions.PatternActions
{
    public class MPEAddOrRemoveColumnsAction : PatternAction
    {
        // <Position, Column>
        List<MPEPatternColumn> removedColumns = new List<MPEPatternColumn>();
        List<MPEPatternColumn> addedColumns = new List<MPEPatternColumn>();

        IEnumerable<IParameter> parameters;

        MPEPattern mpePattern;
        PatternVM patternVM;

        public MPEAddOrRemoveColumnsAction(MPEPattern pattern, PatternVM patternVM, IEnumerable<IParameter> parameters)
            : base(pattern.Pattern)
        {
            mpePattern = pattern;
            this.patternVM = patternVM;
            this.parameters = parameters;
        }

        protected override void DoAction()
        {
            // 1. Removed colunms
            var rc = mpePattern.MPEPatternColumns.Where(x => parameters.All(p => p != x.Parameter && x.GroupType == ParameterGroupType.Track)).ToList();
            for (int i = 0; i < rc.Count(); i++)
            {
                var column = rc.ElementAt(i);
                removedColumns.Add(column);
                mpePattern.MPEPatternColumns.Remove(column);
            }

            // 2. Insert added columns
            var ip = parameters.Where(p => mpePattern.MPEPatternColumns.All(x => p != x.Parameter));

            foreach (var param in ip)
            {
                var mpeColumns = mpePattern.AddColumnToGroup(param);
                foreach (var column in mpeColumns)
                {
                    addedColumns.Add(column);
                }
            }

            mpePattern.UpdateData();
            patternVM.Pattern = Pattern; //Update
        }


        protected override void UndoAction()
        {
            foreach (var column in removedColumns)
            {
                mpePattern.MPEPatternColumns.Add(column);
            }

            foreach (var column in addedColumns)
            {
                mpePattern.MPEPatternColumns.Remove(column);
            }

            removedColumns.Clear();
            addedColumns.Clear();
            mpePattern.UpdateData();
            patternVM.Pattern = Pattern; //Update
        }
    }
}
