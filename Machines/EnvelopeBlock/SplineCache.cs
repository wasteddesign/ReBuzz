using System;
using TestMySpline;

namespace EnvelopeBlock
{
    public class SplineCache
    {
        private float[] yValues;
        private int steps = 0;
        private float stepSizeInSeconds = 0;
#pragma warning disable CS0414 // The field 'SplineCache.startTime' is assigned but its value is never used
        private float startTime = 0;
#pragma warning restore CS0414 // The field 'SplineCache.startTime' is assigned but its value is never used
        private double lengthInSeconds = 0;

        public SplineCache()
        {

        }

        public float[] YValues { get => yValues; set => yValues = value; }
        public double LengthInSeconds { get => lengthInSeconds; set => lengthInSeconds = value; }
        public float StepSizeInSeconds { get => stepSizeInSeconds; set => stepSizeInSeconds = value; }

        public void CreateSpline(IEnvelopePoint[] points, double stepsPerSec = 100)
        {
            float[] x = new float[points.Length];
            float[] y = new float[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                x[i] = (float)points[i].TimeStamp;
                y[i] = (float)points[i].Value;
            }

            LengthInSeconds = points[points.Length - 1].TimeStamp - points[0].TimeStamp;
            steps = (int)((points[points.Length - 1].TimeStamp - points[0].TimeStamp) * stepsPerSec); // Steps per second
            float[] xs = new float[steps];

            StepSizeInSeconds = (float)((points[points.Length - 1].TimeStamp - points[0].TimeStamp) / ((double)steps - 1.0));

            for (int i = 0; i < steps; i++)
            {
                xs[i] = (float)(points[0].TimeStamp + i * StepSizeInSeconds);
            }

            CubicSpline spline = new CubicSpline();
            YValues = spline.FitAndEval(x, y, xs);
        }

        internal void CutMinMax(float min, float max)
        {
            for (int i = 0; i < YValues.Length; i++)
            {
                YValues[i] = Math.Max(YValues[i], min);
                YValues[i] = Math.Min(YValues[i], max);
            }
        }
    }
}
