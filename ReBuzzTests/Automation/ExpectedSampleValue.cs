using Buzz.MachineInterface;
using System;

namespace ReBuzzTests.Automation
{
    public static class ExpectedSampleValue
    {
        private const float DenormalizationPrevention = 1e-15f;
        private const float QuantizationStep = 1 / 32768.0f;

        //bug assumes volume is 1
        public static Sample From(Sample s)
        {
            return new Sample((s.L+ DenormalizationPrevention) * (QuantizationStep), (s.R + DenormalizationPrevention) * (QuantizationStep));
        }

        public static bool AreEqual(Sample sample1, Sample sample2)
        {
            return Math.Abs(sample1.L - sample2.L) < ExpectedSampleValue.DenormalizationPrevention && Math.Abs(sample1.R - sample2.R) < ExpectedSampleValue.DenormalizationPrevention;
        }
    }
}