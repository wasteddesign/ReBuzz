using AwesomeAssertions;
using Buzz.MachineInterface;
using BuzzGUI.Interfaces;
using ReBuzz.Core;
using System.Collections.Generic;
using System.Linq;

namespace ReBuzzTests
{
    /// <summary>
    /// MachineConnectionCore.DoTap reuses its sample buffer instead of allocating one per
    /// call. Handlers rely on samples.Length, so the buffer must still be exactly
    /// nSamples * 2 long and hold this call's samples.
    /// </summary>
    public class ConnectionTapBufferTests
    {
        static MachineConnectionCore NewConnection() =>
            new MachineConnectionCore(null, new[] { 0 }, null, new[] { 0 }, 0x4000, 0x4000, null, null);

        static Sample[] Ramp(int n, float start) =>
            Enumerable.Range(0, n).Select(i => new Sample(start + i, -(start + i))).ToArray();

        static float[] Interleaved(Sample[] s, int n) =>
            s.Take(n).SelectMany(x => new[] { x.L, x.R }).ToArray();

        [Test]
        public void TapReceivesExactLengthInterleavedSamples()
        {
            var conn = NewConnection();
            var received = new List<float[]>();
            conn.Tap += (buf, stereo, time) => received.Add((float[])buf.Clone());

            var a = Ramp(256, 1);
            var b = Ramp(256, 1000);
            conn.DoTap(a, 256, true, new SongTime());
            conn.DoTap(b, 100, true, new SongTime());
            conn.DoTap(a, 256, true, new SongTime());

            received.Should().HaveCount(3);
            received[0].Should().Equal(Interleaved(a, 256));
            received[1].Should().Equal(Interleaved(b, 100));
            received[2].Should().Equal(Interleaved(a, 256));
        }

        [Test]
        public void TapBufferIsReusedForRepeatedChunkSizes()
        {
            var conn = NewConnection();
            var seen = new List<float[]>();
            conn.Tap += (buf, stereo, time) => seen.Add(buf);

            var s = Ramp(256, 0);
            // Typical pattern: full chunks with a remainder chunk at each tick boundary.
            int[] sizes = { 256, 256, 126, 256, 256, 127, 256, 126 };
            foreach (var n in sizes)
                conn.DoTap(s, n, true, new SongTime());

            seen.Select(b => b.Length).Should().Equal(sizes.Select(n => n * 2));
            seen.Distinct().Should().HaveCount(3, "one buffer per distinct chunk size");
        }

        [Test]
        public void NoTapSubscriberDoesNothing()
        {
            var conn = NewConnection();
            // Must not throw or allocate a buffer when nobody listens.
            conn.DoTap(Ramp(16, 0), 16, true, new SongTime());
        }
    }
}
