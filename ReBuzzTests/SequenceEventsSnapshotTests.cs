using AwesomeAssertions;
using BuzzGUI.Interfaces;
using ReBuzz.Core;
using ReBuzzTests.Automation;
using System.Linq;
using Wintellect.PowerCollections;

namespace ReBuzzTests
{
    /// <summary>
    /// The audio thread reads SequenceCore.EventsSnapshot instead of enumerating the
    /// EventsList tree. These tests check that every way of changing a sequence leaves
    /// the snapshot identical to the live dictionary (same keys, same order, same
    /// SequenceEvent instances).
    /// </summary>
    public class SequenceEventsSnapshotTests
    {
        // Break / Mute events have no pattern, so UpdateSpans() does not touch them and
        // the sequence needs no machine or pattern.
        static SequenceEvent Mute() => new SequenceEvent(SequenceEventType.Mute);
        static SequenceEvent Break() => new SequenceEvent(SequenceEventType.Break);

        static SequenceCore NewSequence()
        {
            var seq = new SequenceCore(null);
            // Clear() invokes SpanCleared without a null check.
            seq.SpanCleared += (_, _) => { };
            return seq;
        }

        static void AssertSnapshotMatches(SequenceCore seq)
        {
            var live = seq.EventsList.ToArray();
            var snapshot = seq.EventsSnapshot;
            snapshot.Select(kv => kv.Key).Should().Equal(live.Select(kv => kv.Key));
            snapshot.Select(kv => kv.Value).Should().Equal(live.Select(kv => kv.Value),
                (a, b) => ReferenceEquals(a, b));
            snapshot.Select(kv => kv.Key).Should().BeInAscendingOrder();
        }

        [Test]
        public void SnapshotFollowsEveryKindOfSequenceEdit()
        {
            using var driver = new Driver();
            driver.Start(); // SetEvent and friends call Global.Buzz.SetModifiedFlag()

            var seq = NewSequence();
            AssertSnapshotMatches(seq);

            // SetEvent: add, out of order
            seq.SetEvent(64, Mute());
            seq.SetEvent(0, Mute());
            seq.SetEvent(32, Break());
            AssertSnapshotMatches(seq);
            seq.EventsSnapshot.Select(kv => kv.Key).Should().Equal(0, 32, 64);

            // SetEvent: replace the value at an existing key (key set unchanged)
            var replacement = Break();
            seq.SetEvent(32, replacement);
            AssertSnapshotMatches(seq);
            seq.EventsSnapshot[1].Value.Should().BeSameAs(replacement);

            // SetEvent: remove
            seq.SetEvent(0, null);
            AssertSnapshotMatches(seq);

            // Insert / Delete / Clear reassign EventsList
            seq.Insert(16, 8);
            AssertSnapshotMatches(seq);
            seq.EventsSnapshot.Select(kv => kv.Key).Should().Equal(40, 72);

            seq.Delete(0, 8);
            AssertSnapshotMatches(seq);
            seq.EventsSnapshot.Select(kv => kv.Key).Should().Equal(32, 64);

            seq.SetEvent(100, Mute());
            seq.Clear(30, 40);
            AssertSnapshotMatches(seq);
            seq.EventsSnapshot.Select(kv => kv.Key).Should().Equal(100);

            // RemoveEventAt (used by MachineCore.DeletePattern)
            seq.SetEvent(5, Mute());
            seq.RemoveEventAt(100).Should().BeTrue();
            seq.RemoveEventAt(100).Should().BeFalse();
            AssertSnapshotMatches(seq);

            // Direct reassignment of the public property
            var od = new OrderedDictionary<int, SequenceEvent>();
            od.Add(7, Mute());
            od.Add(3, Break());
            seq.EventsList = od;
            AssertSnapshotMatches(seq);

            // Release
            seq.Release();
            AssertSnapshotMatches(seq);
            seq.EventsSnapshot.Should().BeEmpty();
        }

        [Test]
        public void SnapshotIsReusedUntilTheSequenceChanges()
        {
            using var driver = new Driver();
            driver.Start();

            var seq = NewSequence();
            seq.SetEvent(0, Mute());
            seq.SetEvent(16, Mute());

            var first = seq.EventsSnapshot;
            seq.EventsSnapshot.Should().BeSameAs(first, "no edit happened, so no rebuild");

            seq.SetEvent(32, Mute());
            seq.EventsSnapshot.Should().NotBeSameAs(first);
        }
    }
}
