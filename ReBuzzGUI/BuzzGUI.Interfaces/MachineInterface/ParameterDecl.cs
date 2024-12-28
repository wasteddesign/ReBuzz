using System;

namespace Buzz.MachineInterface
{
    public enum Transformations { Linear, Quadratic, Cubic, Exponential };
    public enum Descriptors { None, Percentage, Decibel, Hertz, Milliseconds };

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
    public class ParameterDecl : Attribute
    {
        public ParameterDecl()
        {
            DecimalDigitCount = 1;
        }

        public string Name { get; set; }
        public string Description { get; set; }
        public int MinValue { get; set; }
        public int MaxValue { get; set; }
        public object DefValue { get; set; }

        /// <summary>Interpolator.SetTarget time in milliseconds</summary>
        public float ResponseTime { get; set; }

        public Transformations Transformation { get; set; }

        /// <summary>For Linear, Quadratic and Cubic transformations</summary>
        public int TransformUnityValue { get; set; }

        /// <summary>For all transformations, overrides TransformUnityValue</summary>
        public float TransformMin { get; set; }

        /// <summary>For all transformations, overrides TransformUnityValue</summary>
        public float TransformMax { get; set; }

        public Descriptors ValueDescriptor { get; set; }
        public string[] ValueDescriptions { get; set; }
        public int DecimalDigitCount { get; set; }

        public bool IsStateless { get; set; }
        public bool IsWaveNumber { get; set; }
        public bool IsTiedToNext { get; set; }
        public bool IsAscii { get; set; }
        public string ValidAscii { get; set; }

    }
}
