using Buzz.MachineInterface;
using System.Collections.Immutable;
using System.Linq;

namespace ReBuzzTests.Automation
{
    public static class SampleExtensions
    {
        public static ImmutableArray<Sample> RepeatTimes(this Sample sample, int times)
        {
            return Enumerable.Repeat(sample, times).ToImmutableArray();
        }
    }
}