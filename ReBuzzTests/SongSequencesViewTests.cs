using AwesomeAssertions;
using BuzzGUI.Common;
using ReBuzz.Core;
using ReBuzzTests.Automation;
using System.Linq;

namespace ReBuzzTests
{
    /// <summary>
    /// SongCore.Sequences returns a cached read-only collection that is rebuilt whenever the
    /// sequence list changes. It must always match SequencesList, and a collection a caller
    /// already holds must not change underneath it.
    /// </summary>
    public class SongSequencesViewTests
    {
        static void AssertViewMatches(SongCore song) =>
            song.Sequences.Should().Equal(song.SequencesList, (a, b) => ReferenceEquals(a, b));

        [Test]
        public void SequencesFollowsAddRemoveAndSwap()
        {
            using var driver = new Driver();
            driver.Start();
            var song = ((ReBuzzCore)Global.Buzz).SongCore;
            var master = song.MachinesList[0];

            AssertViewMatches(song);
            var held = song.Sequences;
            song.Sequences.Should().BeSameAs(held, "no change, so no rebuild");

            song.AddSequence(master, 0);
            song.AddSequence(master, 1);
            AssertViewMatches(song);
            song.Sequences.Should().HaveCount(held.Count + 2);
            held.Should().HaveCount(0, "a collection already handed out does not change");

            var first = song.SequencesList[0];
            var second = song.SequencesList[1];
            song.SwapSequences(first, second);
            AssertViewMatches(song);
            song.Sequences[0].Should().BeSameAs(second);
            song.Sequences[1].Should().BeSameAs(first);

            song.RemoveSequence(first);
            AssertViewMatches(song);
            song.Sequences.Should().ContainSingle().Which.Should().BeSameAs(second);

            song.RemoveSequence(second);
            AssertViewMatches(song);
            song.Sequences.Should().BeEmpty();
        }
    }
}
