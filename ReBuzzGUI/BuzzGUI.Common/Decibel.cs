using System;

namespace BuzzGUI.Common
{
    public static class Decibel
    {
        public static double FromAmplitude(double a) { return Math.Log10(a) * 20.0; }
        public static double ToAmplitude(double db) { return Math.Pow(10, db * 0.05); }
    }
}
