using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace BuzzGUI.Common.InterfaceExtensions
{
    public static class PatternExtensions
    {
        public static void UpdateWaveReferences(this IPattern pattern, IDictionary<int, int> map)
        {
            foreach (var c in pattern.Columns.Where(c => c.Type == PatternColumnType.Parameter && c.Parameter.Flags.HasFlag(ParameterFlags.Wave)))
                c.RemapValues(map);

            pattern.UpdatePEMachineWaveReferences(map);
        }
    }
}
