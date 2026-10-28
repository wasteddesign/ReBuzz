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
        /// The production code used to add a DC offset to every sample to prevent denormals.
        /// It was removed in the May 2026 "Optimizations" commits (cf3df5e, 6a64352, 9e70598),
        /// so expected samples no longer carry it.
        /// </summary>
        private const float DenormalizationPrevention = 0f;

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
            return From(new Sample(0f, 0f));
        }
    }
}