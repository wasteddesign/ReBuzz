using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace BuzzGUI.Common
{
    public class VUMeter
    {
        const double VUMeterRange = 80.0;
        float maxSampleLeft;
        float maxSampleRight;

        // buf is interleaved stereo [L,R,L,R,...] in both callers (DoTap builds
        // nSamples*2; master resSamples likewise). offset/count delimit the live
        // region in floats; count < 0 means "to end of buffer". The stereo flag
        // selects separate L/R vs. collapse-to-left (mono display), NOT the buffer
        // layout. Fields hold the RAW peak; scaling to amplitude happens once in
        // GetLevels (applying scale here would compound across updates between reads).
        public void UpdateMax(float[] buf, bool stereo, SongTime s, int offset = 0, int count = -1)
        {
            if (count < 0)
                count = buf.Length - offset;
            int end = offset + count;
            if (end > buf.Length)
                end = buf.Length;

            if (stereo)
            {
                for (int i = offset; i + 1 < end; i += 2)
                {
                    maxSampleLeft = Math.Max(maxSampleLeft, Math.Abs(buf[i]));
                    maxSampleRight = Math.Max(maxSampleRight, Math.Abs(buf[i + 1]));
                }
            }
            else
            {
                for (int i = offset; i < end; i += 2)
                {
                    maxSampleLeft = Math.Max(maxSampleLeft, Math.Abs(buf[i]));
                }
                maxSampleRight = maxSampleLeft;
            }
        }

        public (double, double) GetLevels()
        {
            const float scale = 1.0f / 32768.0f;

            var db = Math.Min(Math.Max(Decibel.FromAmplitude(maxSampleLeft * scale), -VUMeterRange), 0.0);
            double left = (db + VUMeterRange) / VUMeterRange;
            db = Math.Min(Math.Max(Decibel.FromAmplitude(maxSampleRight * scale), -VUMeterRange), 0.0);
            double right = (db + VUMeterRange) / VUMeterRange;

            maxSampleLeft = 0;
            maxSampleRight = 0;

            return (left, right);
        }
    }
}
