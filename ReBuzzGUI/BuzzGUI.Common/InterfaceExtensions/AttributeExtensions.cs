using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BuzzGUI.Common.InterfaceExtensions
{
    public static class AttributeExtensions
    {
        const int MaxAttributeRange = 256;
        const int PresentableNumber = 40;

        public static bool IsProbablyAPeertuneAttribute(this IAttribute a)
        {
            return a.MinValue == 0 && a.DefValue == 12000 && a.MaxValue == 24000;
        }

        public static IEnumerable<int> GetPresentableNumberOfValues(this IAttribute a)
        {
            if (a.IsProbablyAPeertuneAttribute())
            {
                return LinqExtensions.RangeIncludingEnd(11000, 13000, 50);
            }
            else if (a.MaxValue - a.MinValue > MaxAttributeRange)
            {
                return LinqExtensions.RangeExcludingEnd(a.MinValue, a.MaxValue, (double)(a.MaxValue - a.MinValue) / PresentableNumber)
                    .Select(v => (int)Math.Round(v))
                    .Concat(Enumerable.Repeat(a.DefValue, 1))
                    .Concat(Enumerable.Repeat(a.MaxValue, 1))
                    .Distinct().OrderBy(v => v);
            }
            else
            {
                return Enumerable.Range(a.MinValue, a.MaxValue - a.MinValue + 1);
            }

        }
    }
}
