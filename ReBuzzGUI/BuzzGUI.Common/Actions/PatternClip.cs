using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BuzzGUI.Common.Actions
{
    public class PatternClip
    {
        readonly List<PatternColumnClip> columns;

        public PatternClip(IPattern p)
        {
            columns = p.Columns.Select(c => new PatternColumnClip(c, -int.MaxValue, int.MaxValue)).ToList();
        }

        public void CopyTo(IPattern p)
        {
            for (int i = 0; i < Math.Min(columns.Count, p.Columns.Count); i++)
            {
                columns[i].CopyTo(p.Columns[i]);
            }
        }
    }
}
