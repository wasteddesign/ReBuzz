using BuzzGUI.Interfaces;
using FluentAssertions;
using FluentAssertions.Execution;

namespace ReBuzzTests.Automation.Assertions
{
    /// <summary>
    /// Represents an expected machine parameter.
    /// Includes factory methods for some well-known parameters.
    /// </summary>
    public record ExpectedMachineParameter(
        string ExpectedName,
        string ExpectedDescription,
        ParameterFlags ExpectedFlags,
        int ExpectedDefault,
        int ExpectedMinValue,
        int ExpectedMaxValue,
        int ExpectedNoValue,
        ParameterType ExpectedType)
    {
        public static ExpectedMachineParameter ATrackParam()
        {
            return new ExpectedMachineParameter(
                "ATrackParam",
                "ATrackParam",
                ParameterFlags.State,
                0,
                0,
                127,
                255,
                ParameterType.Byte);
        }

        public static ExpectedMachineParameter Bypass()
        {
            return new ExpectedMachineParameter(
                "Bypass",
                "Bypass",
                ParameterFlags.State,
                0,
                0,
                1,
                255,
                ParameterType.Switch);
        }

        public static ExpectedMachineParameter Gain()
        {
            return new ExpectedMachineParameter(
                "Gain",
                "Gain",
                ParameterFlags.State,
                80,
                0,
                127,
                255,
                ParameterType.Byte);
        }

        internal void AssertIsMatchedBy(
            string name,
            string description,
            ParameterFlags flags,
            int defValue,
            int minValue,
            int maxValue,
            int noValue,
            ParameterType type)
        {
            using (new AssertionScope())
            {
                name.Should().Be(ExpectedName);
                description.Should().Be(ExpectedDescription);
                flags.Should().Be(ExpectedFlags);
                defValue.Should().Be(ExpectedDefault);
                minValue.Should().Be(ExpectedMinValue);
                maxValue.Should().Be(ExpectedMaxValue);
                noValue.Should().Be(ExpectedNoValue);
                type.Should().Be(ExpectedType);
            }
        }

        public static ExpectedMachineParameter Pan()
        {
            return new ExpectedMachineParameter("Pan",
                "Pan (0=Left, 4000=Center, 8000=Right)",
                ParameterFlags.State,
                16384,
                0,
                short.MaxValue + 1,
                0,
                ParameterType.Word);
        }

        public static ExpectedMachineParameter Volume()
        {
            return new ExpectedMachineParameter(
                "Volume",
                "Master Volume (0=0 dB, 4000=-80 dB)",
                ParameterFlags.State,
                0,
                0,
                16384,
                ushort.MaxValue,
                ParameterType.Word);
        }

        public static ExpectedMachineParameter Bpm()
        {
            return new ExpectedMachineParameter(
                "BPM",
                "Beats Per Minute (10-200 hex)",
                ParameterFlags.State,
                126,
                10,
                512,
                65535,
                ParameterType.Word);
        }

        public static ExpectedMachineParameter Tpb()
        {
            return new ExpectedMachineParameter(
                "TPB",
                "Ticks Per Beat (1-20 hex)",
                ParameterFlags.State,
                4,
                1,
                32,
                255,
                ParameterType.Byte);
        }

        public static ExpectedMachineParameter Amp()
        {
            return new ExpectedMachineParameter(
                "Amp",
                "Amp (0=0%, 4000=100%, FFFE=~400%)",
                ParameterFlags.State,
                16384,
                0,
                ushort.MaxValue - 1,
                0,
                ParameterType.Word);
        }
    }
}