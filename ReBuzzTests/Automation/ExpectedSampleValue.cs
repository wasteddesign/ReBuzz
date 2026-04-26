using Buzz.MachineInterface;
using System;

namespace ReBuzzTests.Automation
{
    /// <summary>
    /// Helper class for comparing expected sample values with actual sample values.
    /// </summary>
    public static class ExpectedSampleValue
    {
        /// <summary>
        /// Taken from the production code
        /// </summary>
        private const float DenormalizationPrevention = -1e-15f;

        /// <summary>
        /// Taken from the production code
        /// </summary>
        private const float QuantizationStep = 1 / 32768.0f;

        public static Sample From(Sample s, double volume = 1)
        {
            return new Sample(CalculateExpected(s.L, volume), CalculateExpected(s.R, volume));
        }

        private static float CalculateExpected(float sampleValue, double volume)
        {
            return ((sampleValue * (float)volume + DenormalizationPrevention) * QuantizationStep);
        }

        public static bool AreEqual(Sample sample1, Sample sample2)
        {
            return AreCloseEnough(sample1.L, sample2.L) 
                   && AreCloseEnough(sample1.R, sample2.R);
        }

        private static bool AreCloseEnough(float sample1L, float sample2L)
        {
            return Math.Abs(Math.Abs(sample1L) - Math.Abs(sample2L)) < float.Epsilon;
        }

        public static Sample Zero()
        {
            return new Sample(-1.0000305E-15F, -1.0000305E-15F);
        }
    }
}