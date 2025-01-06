using System;

namespace BuzzGUI.Common.DSP
{
    public static class DSP
    {
        public static float AbsMax(float[] samples)
        {
            float ms = 0.0f;

            for (int i = 0; i < samples.Length; i++)
            {
                float x = Math.Abs(samples[i]);
                if (x > ms) ms = x;
            }

            return ms;
        }

        public static int FirstNonZeroOffset(float[] samples)
        {
            for (int i = 0; i < samples.Length; i++)
                if (samples[i] != 0.0f) return i;

            return -1;
        }

        public static void Scale(float[] samples, float s)
        {
            for (int i = 0; i < samples.Length; i++) samples[i] *= s;
        }

        public static float[] ScaledCopy(float[] samples, float s)
        {
            float[] r = new float[samples.Length];
            for (int i = 0; i < samples.Length; i++) r[i] = samples[i] * s;
            return r;
        }

    }
}
