using System;

namespace Buzz.MachineInterface
{
    public struct Sample
    {
        public float L;
        public float R;

        public Sample(float s)
        {
            L = s;
            R = s;
        }

        public Sample(float l, float r)
        {
            L = l;
            R = r;
        }

        public static implicit operator Sample(float s)
        {
            return new Sample(s);
        }

        public static Sample operator +(Sample a, Sample b) { return new Sample(a.L + b.L, a.R + b.R); }
        public static Sample operator -(Sample a, Sample b) { return new Sample(a.L - b.L, a.R - b.R); }
        public static Sample operator *(Sample a, Sample b) { return new Sample(a.L * b.L, a.R * b.R); }
        public static Sample operator /(Sample a, Sample b) { return new Sample(a.L / b.L, a.R / b.R); }
        public static Sample operator %(Sample a, Sample b) { return new Sample(a.L % b.L, a.R % b.R); }

        public Sample Clamp(float minimum, float maximum)
        {
            return new Sample(Math.Min(Math.Max(L, minimum), maximum), Math.Min(Math.Max(R, minimum), maximum));
        }

    }
}
