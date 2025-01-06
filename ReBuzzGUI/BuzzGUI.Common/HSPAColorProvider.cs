using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace BuzzGUI.Common
{
    public class HSPAColorProvider
    {
        public IEnumerable<Color> Colors { get; private set; }

        public HSPAColorProvider(int count, double h0, double h1, double s0, double s1, double p0, double p1)
        {
            Colors = Enumerable.Range(0, count).Select(i =>
                HSPA.Blend(new HSPA(h0, s0, p0), new HSPA(h1, s1, p1), (double)i / (count - 1)).ToColor());
        }

        public HSPAColorProvider(int count, double h0, double h1, double s0, double s1, double p0, double p1, double a0, double a1)
        {
            Colors = Enumerable.Range(0, count).Select(i =>
                HSPA.Blend(new HSPA(h0, s0, p0, a0), new HSPA(h1, s1, p1, a1), (double)i / (count - 1)).ToColor());
        }

    }
}
