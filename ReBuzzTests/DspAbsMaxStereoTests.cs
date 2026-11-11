using AwesomeAssertions;
using BuzzGUI.Common.DSP;
using System;
using System.Linq;

namespace ReBuzzTests
{
    /// <summary>
    /// DSP.AbsMaxStereo replaces "split into L and R arrays, then AbsMax each" in the
    /// meter Tap handlers. It must give exactly the same peaks.
    /// </summary>
    public class DspAbsMaxStereoTests
    {
        static (float, float) SplitThenAbsMax(float[] interleaved)
        {
            int n = interleaved.Length / 2;
            var l = new float[n];
            var r = new float[n];
            for (int i = 0; i < n; i++)
            {
                l[i] = interleaved[i * 2];
                r[i] = interleaved[i * 2 + 1];
            }
            return (DSP.AbsMax(l), DSP.AbsMax(r));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(253)]
        [TestCase(512)]
        public void MatchesSplitThenAbsMax(int length)
        {
            var rng = new Random(length + 17);
            var buf = Enumerable.Range(0, length).Select(_ => (float)(rng.NextDouble() * 65536 - 32768)).ToArray();

            DSP.AbsMaxStereo(buf, out float l, out float r);

            (l, r).Should().Be(SplitThenAbsMax(buf));
        }

        [Test]
        public void HandlesNaNAndInfinityTheSameWay()
        {
            float[] buf = { float.NaN, -3f, 2f, float.NegativeInfinity, -7f, float.NaN, 1f, 0f };

            DSP.AbsMaxStereo(buf, out float l, out float r);

            (l, r).Should().Be(SplitThenAbsMax(buf));
        }
    }
}
