using BuzzGUI.Interfaces;
using FluentAssertions;
using FluentAssertions.Execution;

namespace ReBuzzTests.Automation;

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
            ExpectedName: "ATrackParam",
            ExpectedDescription: "ATrackParam",
            ExpectedFlags: ParameterFlags.State,
            ExpectedDefault: 0,
            ExpectedMinValue: 0,
            ExpectedMaxValue: 127,
            ExpectedNoValue: 255,
            ExpectedType: ParameterType.Byte);
    }

    public static ExpectedMachineParameter Bypass()
    {
        return new ExpectedMachineParameter(
            ExpectedName: "Bypass",
            ExpectedDescription: "Bypass",
            ExpectedFlags: ParameterFlags.State,
            ExpectedDefault: 0,
            ExpectedMinValue: 0,
            ExpectedMaxValue: 1,
            ExpectedNoValue: 255,
            ExpectedType: ParameterType.Switch);
    }

    public static ExpectedMachineParameter Gain()
    {
        return new ExpectedMachineParameter(
            ExpectedName: "Gain",
            ExpectedDescription: "Gain",
            ExpectedFlags: ParameterFlags.State,
            ExpectedDefault: 80,
            ExpectedMinValue: 0,
            ExpectedMaxValue: 127,
            ExpectedNoValue: 255,
            ExpectedType: ParameterType.Byte);
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
        return new ExpectedMachineParameter(ExpectedName: "Pan",
            ExpectedDescription: "Pan (0=Left, 4000=Center, 8000=Right)",
            ExpectedFlags: ParameterFlags.State,
            ExpectedDefault: 16384,
            ExpectedMinValue: 0,
            ExpectedMaxValue: short.MaxValue + 1,
            ExpectedNoValue: 0,
            ExpectedType: ParameterType.Word);
    }

    public static ExpectedMachineParameter Volume()
    {
        return new ExpectedMachineParameter(
            ExpectedName: "Volume",
            ExpectedDescription: "Master Volume (0=0 dB, 4000=-80 dB)",
            ExpectedFlags: ParameterFlags.State,
            ExpectedDefault: 0,
            ExpectedMinValue: 0,
            ExpectedMaxValue: 16384,
            ExpectedNoValue: ushort.MaxValue,
            ExpectedType: ParameterType.Word);
    }

    public static ExpectedMachineParameter Bpm()
    {
        return new ExpectedMachineParameter(
            ExpectedName: "BPM",
            ExpectedDescription: "Beats Per Minute (10-200 hex)",
            ExpectedFlags: ParameterFlags.State,
            ExpectedDefault: 126,
            ExpectedMinValue: 10,
            ExpectedMaxValue: 512,
            ExpectedNoValue: 65535,
            ExpectedType: ParameterType.Word);
    }

    public static ExpectedMachineParameter Tpb()
    {
        return new ExpectedMachineParameter(
            ExpectedName: "TPB",
            ExpectedDescription: "Ticks Per Beat (1-20 hex)",
            ExpectedFlags: ParameterFlags.State,
            ExpectedDefault: 4,
            ExpectedMinValue: 1,
            ExpectedMaxValue: 32,
            ExpectedNoValue: 255,
            ExpectedType: ParameterType.Byte);
    }

    public static ExpectedMachineParameter Amp()
    {
        return new ExpectedMachineParameter(
            ExpectedName: "Amp",
            ExpectedDescription: "Amp (0=0%, 4000=100%, FFFE=~400%)",
            ExpectedFlags: ParameterFlags.State,
            ExpectedDefault: 16384,
            ExpectedMinValue: 0,
            ExpectedMaxValue: ushort.MaxValue - 1,
            ExpectedNoValue: 0,
            ExpectedType: ParameterType.Word);
    }
}