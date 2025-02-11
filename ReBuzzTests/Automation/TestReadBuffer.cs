using Buzz.MachineInterface;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace ReBuzzTests.Automation
{
    public class TestReadBuffer(int countRead, float[] buffer)
    {
        public void AssertContainStereoSilence(int count)
        {
            countRead.Should().Be(count * 2);
            buffer.Should().Equal(Enumerable.Repeat(0, count * 2),
                (actual, expected) => Math.Abs(actual - expected) <= 0.00000000001);
        }

        public void AssertAreEqualTo(ImmutableArray<Sample> expectation)
        {
            AssertIsEven(countRead);
            var samplesFromBuffer = ConvertToSamples(buffer);
            samplesFromBuffer.Should().Equal(
                expectation,
                ExpectedSampleValue.AreEqual);
        }

        private static IEnumerable<Sample> ConvertToSamples(float[] buffer)
        {
            return buffer
                .Select((value, index) => new { Value = value, Index = index })
                .GroupBy(x => x.Index / 2)
                .Select(group => new Sample(group.First().Value, group.Skip(1).Single().Value));
        }

        private static void AssertIsEven(int value)
        {
            (value % 2).Should().Be(0);
        }
    }
}