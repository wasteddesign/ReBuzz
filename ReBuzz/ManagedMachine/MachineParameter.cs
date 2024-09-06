using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.Linq;
using System.Reflection;

namespace ReBuzz.ManagedMachine
{

    internal class MachineParameter
    {
        internal class Delegates
        {
            internal delegate void BoolSetterDelegate(bool v);

            internal delegate void IntSetterDelegate(int v);

            internal delegate void FloatSetterDelegate(float v);

            internal delegate Interpolator InterpolatorGetterDelegate();

            internal delegate void NoteSetterDelegate(Note v);

            internal delegate void BoolTrackSetterDelegate(bool v, int track);

            internal delegate void IntTrackSetterDelegate(int v, int track);

            internal delegate void FloatTrackSetterDelegate(float v, int track);

            internal delegate void NoteTrackSetterDelegate(Note v, int track);

            internal BoolSetterDelegate BoolSetter;

            internal IntSetterDelegate IntSetter;

            internal FloatSetterDelegate FloatSetter;

            internal Interpolator Interpolator;

            internal NoteSetterDelegate NoteSetter;

            internal BoolTrackSetterDelegate BoolTrackSetter;

            internal IntTrackSetterDelegate IntTrackSetter;

            internal FloatTrackSetterDelegate FloatTrackSetter;

            internal NoteTrackSetterDelegate NoteTrackSetter;

            public Delegates(PropertyInfo pi, IBuzzMachine m)
            {
                if (pi.PropertyType == typeof(Interpolator) && pi.GetGetMethod() != null)
                {
                    InterpolatorGetterDelegate interpolatorGetterDelegate = Delegate.CreateDelegate(typeof(InterpolatorGetterDelegate), m, pi.GetGetMethod()) as InterpolatorGetterDelegate;
                    Interpolator = interpolatorGetterDelegate();
                }
                if (pi.GetSetMethod() != null)
                {
                    if (pi.PropertyType == typeof(bool))
                    {
                        BoolSetter = Delegate.CreateDelegate(typeof(BoolSetterDelegate), m, pi.GetSetMethod()) as BoolSetterDelegate;
                    }
                    else if (pi.PropertyType == typeof(int))
                    {
                        IntSetter = Delegate.CreateDelegate(typeof(IntSetterDelegate), m, pi.GetSetMethod()) as IntSetterDelegate;
                    }
                    else if (pi.PropertyType == typeof(float))
                    {
                        FloatSetter = Delegate.CreateDelegate(typeof(FloatSetterDelegate), m, pi.GetSetMethod()) as FloatSetterDelegate;
                    }
                    else if (pi.PropertyType == typeof(Note))
                    {
                        NoteSetter = Delegate.CreateDelegate(typeof(NoteSetterDelegate), m, pi.GetSetMethod()) as NoteSetterDelegate;
                    }
                }
            }

            public Delegates(MethodInfo mi, IBuzzMachine m)
            {
                Type parameterType = mi.GetParameters()[0].ParameterType;
                if (parameterType == typeof(bool))
                {
                    BoolTrackSetter = Delegate.CreateDelegate(typeof(BoolTrackSetterDelegate), m, mi) as BoolTrackSetterDelegate;
                }
                else if (parameterType == typeof(int))
                {
                    IntTrackSetter = Delegate.CreateDelegate(typeof(IntTrackSetterDelegate), m, mi) as IntTrackSetterDelegate;
                }
                else if (parameterType == typeof(bool))
                {
                    FloatTrackSetter = Delegate.CreateDelegate(typeof(FloatTrackSetterDelegate), m, mi) as FloatTrackSetterDelegate;
                }
                else if (parameterType == typeof(Note))
                {
                    NoteTrackSetter = Delegate.CreateDelegate(typeof(NoteTrackSetterDelegate), m, mi) as NoteTrackSetterDelegate;
                }
            }
        }

        public ParameterType Type;

        public string Name;

        public string Description;

        public int MinValue;

        public int MaxValue;

        public int NoValue;

        public ParameterFlags Flags;

        public int DefValue;

        private readonly ParameterDecl decl;

        private int setValueCount;

        private PropertyInfo PropertyInfo { get; set; }

        private MethodInfo MethodInfo { get; set; }

        public IntPtr NativeStruct { get; private set; }

        private void Error(string message)
        {
            throw new Exception($"{Name}: {message}");
        }

        public MachineParameter(PropertyInfo pi)
        {
            PropertyInfo = pi;
            decl = pi.GetCustomAttributes(inherit: false).OfType<ParameterDecl>().First();
            Create(pi.PropertyType, pi.Name);
        }

        public MachineParameter(MethodInfo mi)
        {
            MethodInfo = mi;
            decl = mi.GetCustomAttributes(inherit: false).OfType<ParameterDecl>().First();
            if (mi.GetParameters().Length != 2)
            {
                Error("Invalid method signature");
            }
            if (mi.GetParameters()[1].ParameterType != typeof(int))
            {
                Error("Invalid method signature");
            }
            Create(mi.GetParameters()[0].ParameterType, mi.Name);
        }

        private void Create(Type pt, string declname)
        {
            Name = decl.Name ?? declname;
            Description = decl.Description ?? Name;
            Flags = ((!decl.IsStateless) ? ParameterFlags.State : ParameterFlags.None);
            if (decl.IsWaveNumber)
            {
                if (pt != typeof(int))
                {
                    Error("IsWaveNumber requires an int property");
                }
                Type = ParameterType.Byte;
                Flags |= ParameterFlags.Wave;
                NoValue = 0;
                MinValue = 1;
                MaxValue = 200;
                DefValue = 1;
            }
            else if (pt == typeof(bool))
            {
                Type = ParameterType.Switch;
                MinValue = 0;
                MaxValue = 1;
                NoValue = 255;
                DefValue = ((decl.DefValue is bool) ? (((bool)decl.DefValue) ? 1 : 0) : 0);
                if (decl.Transformation != 0)
                {
                    Error("Transformations require float or Interpolator property type.");
                }
            }
            else if (pt == typeof(Note))
            {
                Type = ParameterType.Note;
                MinValue = 1;
                MaxValue = 156;
                NoValue = 0;
                if (decl.Transformation != 0)
                {
                    Error("Transformations require float or Interpolator property type.");
                }
            }
            else
            {
                if (pt != typeof(int) && pt != typeof(float) && pt != typeof(Interpolator))
                {
                    Error("Invalid parameter type. The supported types are bool, int, float and Interpolator.");
                }
                if (MethodInfo != null && pt == typeof(Interpolator))
                {
                    Error("Invalid parameter type. The supported types for track parameters are bool, int and float.");
                }
                if (decl.ValueDescriptions != null && (decl.DefValue == null || decl.MaxValue < decl.ValueDescriptions.Length - 1))
                {
                    decl.MinValue = 0;
                    decl.MaxValue = decl.ValueDescriptions.Length - 1;
                    if (decl.DefValue == null)
                    {
                        decl.DefValue = 0;
                    }
                }
                if (decl.MinValue < 0 || decl.MinValue > 65534)
                {
                    Error("Invalid MinValue");
                }
                if (decl.MaxValue < 0 || decl.MaxValue > 65534 || decl.MaxValue < decl.MinValue)
                {
                    Error("Invalid MaxValue");
                }
                if (!decl.IsStateless && decl.DefValue == null)
                {
                    Error("DefValue missing");
                }
                if (!decl.IsStateless && (!(decl.DefValue is int) || (int)decl.DefValue < decl.MinValue || (int)decl.DefValue > decl.MaxValue))
                {
                    Error("Invalid DefValue");
                }
                if (decl.IsStateless && decl.MinValue == 0 && decl.MaxValue == 255)
                {
                    Type = ParameterType.Byte;
                    NoValue = -1;
                }
                else if (decl.MaxValue > 254)
                {
                    Type = ParameterType.Word;
                    NoValue = 65535;
                }
                else
                {
                    Type = ParameterType.Byte;
                    NoValue = 255;
                }
                MinValue = decl.MinValue;
                MaxValue = decl.MaxValue;
                DefValue = ((decl.DefValue != null) ? ((int)decl.DefValue) : MinValue);
                if (pt != typeof(float) && pt != typeof(Interpolator) && decl.Transformation != 0)
                {
                    Error("Transformations require float or Interpolator property type.");
                }
                if (decl.Transformation == Transformations.Exponential)
                {
                    if (decl.TransformMin == 0f || decl.TransformMax == 0f)
                    {
                        Error("Transformations.Exponential requires non-zero TransformMin and TransformMax.");
                    }
                    else if (Math.Sign(decl.TransformMin) != Math.Sign(decl.TransformMax))
                    {
                        Error("Transformations.Exponential requires TransformMin and TransformMax of the same sign.");
                    }
                }
                if (Type == ParameterType.Byte)
                {
                    if (decl.IsTiedToNext)
                    {
                        Flags |= ParameterFlags.TiedToNext;
                    }
                    if (decl.IsAscii)
                    {
                        Flags |= ParameterFlags.Ascii;
                    }
                }
            }
        }

        public Delegates CreateDelegates(IBuzzMachine m)
        {
            if (PropertyInfo != null)
            {
                return new Delegates(PropertyInfo, m);
            }
            return new Delegates(MethodInfo, m);
        }

        private float Transform(int value)
        {
            if (decl.TransformMin != 0f || decl.TransformMax != 0f)
            {
                float transformMin = decl.TransformMin;
                float transformMax = decl.TransformMax;
                float num = value / (float)MaxValue;
                switch (decl.Transformation)
                {
                    case Transformations.Linear:
                        return transformMin + num * (transformMax - transformMin);
                    case Transformations.Quadratic:
                        return transformMin + num * num * (transformMax - transformMin);
                    case Transformations.Cubic:
                        return transformMin + num * num * num * (transformMax - transformMin);
                    case Transformations.Exponential:
                        {
                            double num2 = Math.Log(transformMin);
                            double num3 = Math.Log(transformMax);
                            return (float)Math.Exp(num2 + (double)num * (num3 - num2));
                        }
                    default:
                        return 0f;
                }
            }
            float num4 = value / (float)((decl.TransformUnityValue > 0) ? decl.TransformUnityValue : MaxValue);
            switch (decl.Transformation)
            {
                case Transformations.Quadratic:
                    num4 *= num4;
                    break;
                case Transformations.Cubic:
                    num4 = num4 * num4 * num4;
                    break;
            }
            return num4;
        }

        public void SetValue(IBuzzMachineHost host, Delegates delegates, int track, int value)
        {
            if (PropertyInfo != null)
            {
                if (delegates.Interpolator != null)
                {
                    delegates.Interpolator.SetTarget(Transform(value), (setValueCount > 0) ? host.MsToSamples(decl.ResponseTime) : 0);
                }
                else if (delegates.FloatSetter != null)
                {
                    delegates.FloatSetter(Transform(value));
                }
                else if (delegates.IntSetter != null)
                {
                    delegates.IntSetter(value);
                }
                else if (delegates.BoolSetter != null)
                {
                    delegates.BoolSetter(value != 0);
                }
                else if (delegates.NoteSetter != null)
                {
                    delegates.NoteSetter(new Note(value));
                }
            }
            else if (delegates.FloatTrackSetter != null)
            {
                delegates.FloatTrackSetter(Transform(value), track);
            }
            else if (delegates.IntTrackSetter != null)
            {
                delegates.IntTrackSetter(value, track);
            }
            else if (delegates.BoolTrackSetter != null)
            {
                delegates.BoolTrackSetter(value != 0, track);
            }
            else if (delegates.NoteTrackSetter != null)
            {
                delegates.NoteTrackSetter(new Note(value), track);
            }
            setValueCount++;
        }

        public string DescribeValue(IBuzzMachineHost host, Delegates delegates, int value)
        {
            string text = null;
            float num = Transform(value);
            string text2 = "{0:F" + decl.DecimalDigitCount + "}";
            if (decl.ValueDescriptions != null)
            {
                if (value >= 0 && value < decl.ValueDescriptions.Length)
                {
                    text = decl.ValueDescriptions[value];
                }
            }
            else if (decl.ValueDescriptor == Descriptors.Percentage)
            {
                text = string.Format(text2 + "%", num * 100f);
            }
            else if (decl.ValueDescriptor == Descriptors.Decibel)
            {
                text = ((num != 0f) ? string.Format(text2 + " dB", Decibel.FromAmplitude(num)) : "-inf. dB");
            }
            else if (decl.ValueDescriptor == Descriptors.Hertz)
            {
                text = string.Format(text2 + " Hz", num);
            }
            else if (decl.ValueDescriptor == Descriptors.Milliseconds)
            {
                text = string.Format(text2 + " ms", num);
            }

            return text;
        }

        public bool IsValidAsciiChar(char ch)
        {
            if (decl == null || decl.ValidAscii == null)
            {
                return true;
            }
            return decl.ValidAscii.Contains(ch);
        }
    }
}
