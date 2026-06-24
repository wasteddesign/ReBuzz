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

        public void UpdateMax(float[] buf, bool stereo, SongTime s)
        {
            int count = buf.Length;

            float scale = (1.0f / 32768.0f);
            if (stereo)
            {
                count = count / 2;
                for (int i = 0; i < count; i += 2)
                {
                    maxSampleLeft = Math.Max(maxSampleLeft, Math.Abs(buf[i]));
                    maxSampleRight = Math.Max(maxSampleRight, Math.Abs(buf[i + 1]));
                }
            }
            else
            {
                for (int i = 0; i < count; i += 2)
                {
                    maxSampleLeft = Math.Max(maxSampleLeft, Math.Abs(buf[i]));
                }
                maxSampleRight = maxSampleLeft;
            }

            maxSampleLeft *= scale;
            maxSampleRight *= scale;
        }

        public (double, double) GetLevels()
        {
            var db = Math.Min(Math.Max(Decibel.FromAmplitude(maxSampleLeft), -VUMeterRange), 0.0);
            double left = (db + VUMeterRange) / VUMeterRange;
            db = Math.Min(Math.Max(Decibel.FromAmplitude(maxSampleRight), -VUMeterRange), 0.0);
            double right = (db + VUMeterRange) / VUMeterRange;

            maxSampleLeft = 0;
            maxSampleRight = 0;

            return (left, right);
        }
    }
}
