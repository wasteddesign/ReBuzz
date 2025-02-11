using Buzz.MachineInterface;
using System;

namespace ReBuzzTests.Automation
{
    public static class ExpectedSampleValue
    {
        private const float DenormalizationPrevention = 1e-15f;
        private const float QuantizationStep = 1 / 32768.0f;

        public static Sample From(Sample s, float volume = 1)
        {
            return new Sample(CalculateExpected(s.L, volume), CalculateExpected(s.R, volume));
        }

        private static float CalculateExpected(float sampleValue, float volume)
        {
            return (sampleValue+ DenormalizationPrevention) * QuantizationStep * volume;
        }

        public static bool AreEqual(Sample sample1, Sample sample2)
        {
            return AreCloseEnough(sample1.L, sample2.L) 
                   && AreCloseEnough(sample1.R, sample2.R);
        }

        public static bool AreCloseEnough(float sample1L, float sample2L)
        {
            return Math.Abs(sample1L - sample2L) < DenormalizationPrevention;
        }
    }
}