using Buzz.MachineInterface;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace ReBuzzTests.Automation
{
    public class TestReadBuffer(int countRead, float[] buffer) //bug move
    {
        public void AssertContainStereoSilence(int count)
        {
            countRead.Should().Be(count * 2);
            buffer.Should().Equal(Enumerable.Repeat(0, count * 2), (f, f1) => Math.Abs(f - f1) <= 0.00000000001);
        }

        public void AssertSamples(ImmutableArray<Sample> expectation)
        {
            (countRead % 2).Should().Be(0);
            IEnumerable<Sample> samplesFromBuffer = buffer
                .Select((value, index) => new { Value = value, Index = index })
                .GroupBy(x => x.Index / 2)
                .Select(group => new Sample(group.First().Value, group.Skip(1).FirstOrDefault().Value));
            samplesFromBuffer.Should().Equal(
                expectation,
                ExpectedSampleValue.AreEqual);
        }
    }
}