using AtmaFileSystem;
using Buzz.MachineInterface;
using BuzzGUI.Interfaces;
using FluentAssertions;
using FluentAssertions.Execution;
using ReBuzz.Core;
using ReBuzz.MachineManagement;
using ReBuzz.ManagedMachine;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace ReBuzzTests.Automation;

public record ExpectedParameter //bug move
{
    public ParameterType ExpectedType;
    public int ExpectedNoValue;
    public int ExpectedMaxValue;
    public int ExpectedMinValue;
    public int ExpectedDefault;
    public ParameterFlags ExpectedFlags;
    public string ExpectedDescription;
    public string ExpectedName;

    public ExpectedParameter(
        string expectedName,
        string expectedDescription,
        ParameterFlags expectedFlags,
        int expectedDefault,
        int expectedMinValue,
        int expectedMaxValue,
        int expectedNoValue,
        ParameterType expectedType)
    {
        ExpectedName = expectedName;
        ExpectedDescription = expectedDescription;
        ExpectedFlags = expectedFlags;
        ExpectedDefault = expectedDefault;
        ExpectedMinValue = expectedMinValue;
        ExpectedMaxValue = expectedMaxValue;
        ExpectedNoValue = expectedNoValue;
        ExpectedType = expectedType;
    }

    public static ExpectedParameter ATrackParam()
    {
        return new ExpectedParameter(
            expectedName: "ATrackParam",
            expectedDescription: "ATrackParam",
            expectedFlags: ParameterFlags.State,
            expectedDefault: 0,
            expectedMinValue: 0,
            expectedMaxValue: 127,
            expectedNoValue: 255,
            expectedType: ParameterType.Byte);
    }

    public static ExpectedParameter Bypass()
    {
        return new ExpectedParameter(
            expectedName: "Bypass",
            expectedDescription: "Bypass",
            expectedFlags: ParameterFlags.State,
            expectedDefault: 0,
            expectedMinValue: 0,
            expectedMaxValue: 1,
            expectedNoValue: 255,
            expectedType: ParameterType.Switch);
    }

    public static ExpectedParameter Gain()
    {
        return new ExpectedParameter(
            expectedName: "Gain",
            expectedDescription: "Gain",
            expectedFlags: ParameterFlags.State,
            expectedDefault: 80,
            expectedMinValue: 0,
            expectedMaxValue: 127,
            expectedNoValue: 255,
            expectedType: ParameterType.Byte);
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

    public static ExpectedParameter Pan()
    {
        return new ExpectedParameter(expectedName: "Pan",
            expectedDescription: "Pan (0=Left, 4000=Center, 8000=Right)",
            expectedFlags: ParameterFlags.State,
            expectedDefault: 16384,
            expectedMinValue: 0,
            expectedMaxValue: short.MaxValue + 1,
            expectedNoValue: 0,
            expectedType: ParameterType.Word);
    }

    public static ExpectedParameter Volume()
    {
        return new ExpectedParameter(
            expectedName: "Volume",
            expectedDescription: "Master Volume (0=0 dB, 4000=-80 dB)",
            expectedFlags: ParameterFlags.State,
            expectedDefault: 0,
            expectedMinValue: 0,
            expectedMaxValue: 16384,
            expectedNoValue: ushort.MaxValue,
            expectedType: ParameterType.Word);
    }

    public static ExpectedParameter Bpm()
    {
        return new ExpectedParameter(
            expectedName: "BPM",
            expectedDescription: "Beats Per Minute (10-200 hex)",
            expectedFlags: ParameterFlags.State,
            expectedDefault: 126,
            expectedMinValue: 10,
            expectedMaxValue: 512,
            expectedNoValue: 65535,
            expectedType: ParameterType.Word);
    }

    public static ExpectedParameter Tpb()
    {
        return new ExpectedParameter(
            expectedName: "TPB",
            expectedDescription: "Ticks Per Beat (1-20 hex)",
            expectedFlags: ParameterFlags.State,
            expectedDefault: 4,
            expectedMinValue: 1,
            expectedMaxValue: 32,
            expectedNoValue: 255,
            expectedType: ParameterType.Byte);
    }

    public static ExpectedParameter Amp()
    {
        return new ExpectedParameter(
            expectedName: "Amp",
            expectedDescription: "Amp (0=0%, 4000=100%, FFFE=~400%)",
            expectedFlags: ParameterFlags.State,
            expectedDefault: 16384,
            expectedMinValue: 0,
            expectedMaxValue: ushort.MaxValue - 1,
            expectedNoValue: 0,
            expectedType: ParameterType.Word);
    }
}

public static class InitialStateAssertions
{
    private static readonly Dictionary<string, Color> ColorDictionary = new Dictionary<string, Color>
  {
    { "DC BG", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) }, { "DC Text", Color.FromArgb(0xFF, 0xC0, 0xC0, 0xC0) },
    { "IV BG", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) }, { "IV Text", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) },
    { "MV Amp BG", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF) },
    { "MV Amp Handle", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) }, { "MV Arrow", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF) },
    { "MV Background", Color.FromArgb(0xFF, 0xDA, 0xD6, 0xC9) },
    { "MV Control", Color.FromArgb(0xFF, 0xAD, 0xAD, 0xAD) }, { "MV Effect", Color.FromArgb(0xFF, 0xC7, 0xAD, 0xA9) },
    { "MV Effect LED Border", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) },
    { "MV Effect LED Off", Color.FromArgb(0xFF, 0x64, 0x1E, 0x1E) },
    { "MV Effect LED On", Color.FromArgb(0xFF, 0xFF, 0x64, 0x64) },
    { "MV Effect Mute", Color.FromArgb(0xFF, 0x9F, 0x8A, 0x87) },
    { "MV Effect Pan BG", Color.FromArgb(0xFF, 0x92, 0x65, 0x5F) },
    { "MV Generator", Color.FromArgb(0xFF, 0xA9, 0xAE, 0xC7) },
    { "MV Generator LED Border", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) },
    { "MV Generator LED Off", Color.FromArgb(0xFF, 0x28, 0x28, 0x8C) },
    { "MV Generator LED On", Color.FromArgb(0xFF, 0x64, 0x64, 0xFF) },
    { "MV Generator Mute", Color.FromArgb(0xFF, 0x87, 0x8B, 0x9F) },
    { "MV Generator Pan BG", Color.FromArgb(0xFF, 0x5F, 0x67, 0x92) },
    { "MV Line", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) },
    { "MV Machine Border", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) },
    { "MV Machine Select 1", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF) },
    { "MV Machine Select 2", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) },
    { "MV Machine Text", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) },
    { "MV Master", Color.FromArgb(0xFF, 0xC6, 0xBE, 0xAA) },
    { "MV Master LED Border", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) },
    { "MV Master LED Off", Color.FromArgb(0xFF, 0x59, 0x59, 0x22) },
    { "MV Master LED On", Color.FromArgb(0xFF, 0xE8, 0xE8, 0xC1) },
    { "MV Pan Handle", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) }, { "PE BG", Color.FromArgb(0xFF, 0xDA, 0xD6, 0xC9) },
    { "PE BG Dark", Color.FromArgb(0xFF, 0xBD, 0xB5, 0x9F) },
    { "PE BG Very Dark", Color.FromArgb(0xFF, 0x9F, 0x93, 0x73) },
    { "PE Sel BG", Color.FromArgb(0xFF, 0xF7, 0xF7, 0xF4) }, { "PE Text", Color.FromArgb(0xFF, 0x30, 0x30, 0x21) },
    { "SA Amp BG", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) }, { "SA Amp Line", Color.FromArgb(0xFF, 0x00, 0xC8, 0x00) },
    { "SA Freq BG", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) },
    { "SA Freq Line", Color.FromArgb(0xFF, 0x00, 0xC8, 0x00) }, { "SE BG", Color.FromArgb(0xFF, 0xDA, 0xD6, 0xC9) },
    { "SE BG Dark", Color.FromArgb(0xFF, 0xBD, 0xB5, 0x9F) },
    { "SE BG Very Dark", Color.FromArgb(0xFF, 0x9F, 0x93, 0x73) },
    { "SE Break Box", Color.FromArgb(0xFF, 0xE0, 0xB0, 0x6D) }, { "SE Line", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) },
    { "SE Mute Box", Color.FromArgb(0xFF, 0xDD, 0x81, 0x6C) },
    { "SE Pattern Box", Color.FromArgb(0xFF, 0xC6, 0xBE, 0xA9) },
    { "SE Sel BG", Color.FromArgb(0xFF, 0xF7, 0xF7, 0xF4) },
    { "SE Song Position", Color.FromArgb(0xFF, 0xFF, 0xFF, 0x00) },
    { "SE Text", Color.FromArgb(0xFF, 0x30, 0x30, 0x21) }, { "black", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) }
  };

    public static void AssertInitialState(AbsoluteDirectoryPath gearDir, ReBuzzCore reBuzzCore, IAdditionalInitialStateAssertions additionalAssertions)
    {
        // Assertions for ReBuzzCore properties
        reBuzzCore.HostVersion.Should().Be(66);
        reBuzzCore.BPM.Should().Be(126);
        reBuzzCore.TPB.Should().Be(4);
        reBuzzCore.Speed.Should().Be(0);
        reBuzzCore.Playing.Should().BeFalse();
        reBuzzCore.Recording.Should().BeFalse();
        reBuzzCore.Looping.Should().BeFalse();
        reBuzzCore.AudioDeviceDisabled.Should().BeFalse();
        reBuzzCore.MIDIControllers.Should().BeEmpty();
        //bug different results with R# and NCrunch: rebuzzCore.Theme.Should().BeEquivalentTo(new ReBuzzTheme());
        reBuzzCore.VUMeterLevel.Item1.Should().Be(0.0);
        reBuzzCore.VUMeterLevel.Item2.Should().Be(0.0);
        reBuzzCore.MidiControllerAssignments.MIDIControllers.Should().BeEmpty();
        reBuzzCore.MidiControllerAssignments.ReBuzzMIDIControllers.Should().BeEmpty();
        reBuzzCore.MidiControllerAssignments.Song.Should().Be(reBuzzCore.SongCore);

        AssertInitialStateOfSongAndSongCore(reBuzzCore.SongCore, reBuzzCore.Song, reBuzzCore, gearDir, additionalAssertions);

        // Additional assertions for ReBuzzCore properties

        foreach (var (key, value) in ColorDictionary)
        {
            reBuzzCore.ThemeColors[key].Should().Be(value);
        }

        reBuzzCore.MachineIndex.Should().BeEquivalentTo(new MenuItemCore());
        reBuzzCore.MIDIFocusMachine.Should().Be(reBuzzCore.SongCore.MachinesList[0]);
        reBuzzCore.MIDIFocusLocked.Should().BeFalse();
        reBuzzCore.MIDIActivity.Should().BeFalse();
        reBuzzCore.IsPianoKeyboardVisible.Should().BeFalse();
        reBuzzCore.IsSettingsWindowVisible.Should().BeFalse();
        reBuzzCore.IsCPUMonitorWindowVisible.Should().BeFalse();
        reBuzzCore.IsHardDiskRecorderWindowVisible.Should().BeFalse();
        reBuzzCore.IsFullScreen.Should().BeFalse();
        reBuzzCore.Instruments.Should().BeEmpty();
        reBuzzCore.AudioDrivers.Should().NotBeEmpty();
        reBuzzCore.SelectedAudioDriver.Should().BeNullOrEmpty();
        reBuzzCore.SelectedAudioDriverSampleRate.Should().Be(44100);
        reBuzzCore.ActiveView.Should().Be(BuzzView.MachineView);
        reBuzzCore.MachineDLLsList.Should().HaveCount(1);
        reBuzzCore.AUTO_CONVERT_WAVES.Should().BeFalse();

        AssertInitialStateOfMachineManager(reBuzzCore.MachineManager, reBuzzCore, additionalAssertions, gearDir);

        AssertFakeModernPatternEditor(reBuzzCore.MachineDLLsList, reBuzzCore, gearDir);
        //TODO

        //bug more assertions and take some of this from the fake machine scanner.

        //
        //// Assertions for static properties
        ReBuzzCore.buildNumber.Should().NotBe(0);
        ReBuzzCore.AppDataPath.Should().Be("ReBuzz");
        ReBuzzCore.GlobalState.AudioFrame.Should().Be(0);
        ReBuzzCore.GlobalState.ADWritePos.Should().Be(0);
        ReBuzzCore.GlobalState.ADPlayPos.Should().Be(0);
        ReBuzzCore.GlobalState.SongPosition.Should().Be(0);
        ReBuzzCore.GlobalState.LoopStart.Should().Be(0);
        ReBuzzCore.GlobalState.LoopEnd.Should().Be(16);
        ReBuzzCore.GlobalState.SongEnd.Should().Be(16);
        ReBuzzCore.GlobalState.StateFlags.Should().Be(0);
        ReBuzzCore.GlobalState.MIDIFiltering.Should().Be(0);
        ReBuzzCore.GlobalState.SongClosing.Should().Be(0);
    }

    private static void AssertInitialStateOfSongAndSongCore(
        SongCore songCore,
        ISong song,
        ReBuzzCore reBuzzCore,
        AbsoluteDirectoryPath gearDir,
        IAdditionalInitialStateAssertions additionalAssertions)
    {
        songCore.BuzzCore.Should().Be(reBuzzCore);
        songCore.ActionStack.Actions.Should().BeEmpty();
        songCore.ActionStack.CanRedo.Should().BeFalse();
        songCore.ActionStack.CanUndo.Should().BeFalse();
        songCore.ActionStack.MaxNumberOfActions.Should().Be(int.MaxValue);
        songCore.Associations.Should().BeEmpty();
        songCore.CanRedo.Should().BeFalse();
        songCore.CanUndo.Should().BeFalse();
        songCore.LoopStart.Should().Be(0);
        songCore.LoopEnd.Should().Be(16);
        songCore.PlayPosition.Should().Be(0);
        songCore.Sequences.Should().BeEmpty();
        songCore.SequencesList.Should().BeEmpty();
        songCore.SongName.Should().BeNullOrEmpty();
        songCore.SoloMode.Should().BeFalse();
        songCore.Wavetable.Song.Should().Be(song);
        songCore.Wavetable.Volume.Should().Be(0);
        songCore.Wavetable.Waves.Should().Equal(Enumerable.Range(0, 200).Select(_ => null as IWave).ToArray());
        songCore.Machines.Should().HaveCount(1);

        AssertIsMasterMachine(songCore.Machines[0], reBuzzCore, gearDir, additionalAssertions);
        songCore.MachinesList[0].Should().Be(songCore.Machines[0]);
        additionalAssertions.AssertSongCore(songCore, gearDir, reBuzzCore);

        song.Should().BeSameAs(songCore);
    }

    private static void AssertInitialStateOfMachineManager(
        MachineManager machineManager, ReBuzzCore reBuzzCore, IAdditionalInitialStateAssertions additionalAssertions,
        AbsoluteDirectoryPath gearDir)
    {
        machineManager.Should().NotBeNull();
        machineManager.Buzz.Should().Be(reBuzzCore);
        machineManager.IsSingleProcessMode.Should().BeFalse();
        machineManager.NativeMachines.Should().BeEmpty();
        additionalAssertions.AssertMachineManager(reBuzzCore, gearDir, machineManager);
    }

    private static void AssertFakeModernPatternEditor( //bug use this
      Dictionary<string, MachineDLL> machineDlLsList, ReBuzzCore reBuzzCore, AbsoluteDirectoryPath gearDir)
    {
        var modernPatternEditor = machineDlLsList["Modern Pattern Editor"];
        AssertFakeModernPatternEditor(reBuzzCore, gearDir, modernPatternEditor);
    }

    internal static void AssertFakeModernPatternEditor(
        ReBuzzCore reBuzzCore, AbsoluteDirectoryPath gearDir, MachineDLL modernPatternEditor)
    {
        var modernPatternEditorDll = FakeModernPatternEditorInfo.GetMachineDll(reBuzzCore,
            gearDir.AddFileName("StubMachine.dll"));
        modernPatternEditor.Buzz.Should().Be(reBuzzCore);
        modernPatternEditor.Path.Should().Be(modernPatternEditorDll.Path);
        modernPatternEditor.Name.Should().Be(modernPatternEditorDll.Name);
        modernPatternEditor.Is64Bit.Should().Be(modernPatternEditorDll.Is64Bit);
        modernPatternEditor.IsCrashed.Should().Be(modernPatternEditorDll.IsCrashed);
        modernPatternEditor.IsManaged.Should().Be(modernPatternEditorDll.IsManaged);
        modernPatternEditor.IsLoaded.Should().Be(true);
        modernPatternEditor.IsMissing.Should().Be(modernPatternEditorDll.IsMissing);
        modernPatternEditor.IsOutOfProcess.Should().Be(modernPatternEditorDll.IsOutOfProcess);
        modernPatternEditor.ModuleHandle.Should().Be(modernPatternEditorDll.ModuleHandle);
        modernPatternEditor.SHA1Hash.Should().Be(modernPatternEditorDll.SHA1Hash);
        modernPatternEditor.Info.Should().Be(modernPatternEditor.MachineInfo);

        modernPatternEditor.MachineInfo.Name.Should().Be(modernPatternEditorDll.MachineInfo.Name);
        modernPatternEditor.MachineInfo.Author.Should().Be(modernPatternEditorDll.MachineInfo.Author);
        modernPatternEditor.MachineInfo.Flags.Should().Be(MachineInfoFlags.STEREO_EFFECT | MachineInfoFlags.LOAD_DATA_RUNTIME);
        modernPatternEditor.MachineInfo.InternalVersion.Should().Be(modernPatternEditorDll.MachineInfo.InternalVersion);
        modernPatternEditor.MachineInfo.MaxTracks.Should().Be(modernPatternEditorDll.MachineInfo.MaxTracks);
        modernPatternEditor.MachineInfo.MinTracks.Should().Be(modernPatternEditorDll.MachineInfo.MinTracks);
        modernPatternEditor.MachineInfo.ShortName.Should().Be(modernPatternEditorDll.MachineInfo.ShortName);
        modernPatternEditor.MachineInfo.Type.Should().Be(modernPatternEditorDll.MachineInfo.Type);

        modernPatternEditor.ManagedDLL.machineInfo.Should().Be(FakeModernPatternEditor.GetMachineDecl());
        modernPatternEditor.ManagedDLL.MachineInfo.Should().Be(modernPatternEditor.MachineInfo);
        modernPatternEditor.ManagedDLL.WorkFunctionType.Should().Be(ManagedMachineDLL.WorkFunctionTypes.Effect);
        modernPatternEditor.ManagedDLL.Assembly.Should().NotBeNull();
        modernPatternEditor.ManagedDLL.constructor.Should().NotBeNull();

        modernPatternEditor.ManagedDLL.globalParameters.Should().HaveCount(2);
        AssertParameter(parameter: modernPatternEditor.ManagedDLL.globalParameters[0], ExpectedParameter.Gain());
        AssertParameter(parameter: modernPatternEditor.ManagedDLL.globalParameters[1], ExpectedParameter.Bypass());

        modernPatternEditor.ManagedDLL.trackParameters.Should().HaveCount(1);
        AssertParameter(modernPatternEditor.ManagedDLL.trackParameters[0], ExpectedParameter.ATrackParam());

        modernPatternEditor.ManagedDLL.machineType.Name.Should().Be(nameof(FakeModernPatternEditor));
        modernPatternEditor.Presets.Should().BeNull();
    }

    internal static void AssertIsMasterMachine(
        IMachine machine, ReBuzzCore rebuzzCore, AbsoluteDirectoryPath gearDir,
        IAdditionalInitialStateAssertions additionalAssertions)
    {
        machine.Name.Should().Be("Master");
        machine.Attributes.Should().BeEmpty();
        machine.BaseOctave.Should().Be(4);
        machine.Commands.Should().BeEmpty();
        machine.Data.Should().BeNull();
        machine.EnvelopeNames.Should().BeEmpty();
        machine.HasStereoInput.Should().BeTrue();
        machine.HasStereoOutput.Should().BeTrue();
        machine.Graph.Should().Be(rebuzzCore.SongCore);
        machine.InputChannelCount.Should().Be(1);
        machine.OutputChannelCount.Should().Be(0);
        machine.Inputs.Should().BeEmpty();
        machine.IsActive.Should().BeFalse();
        machine.IsBypassed.Should().BeFalse();
        machine.IsControlMachine.Should().BeFalse();
        machine.IsMuted.Should().BeFalse();
        machine.IsSoloed.Should().BeFalse();
        machine.IsWireless.Should().BeFalse();
        machine.TrackCount.Should().Be(0);
        machine.LastEngineThread.Should().Be(0);
        machine.Latency.Should().Be(0);
        machine.MIDIInputChannel.Should().Be(-1);
        machine.Outputs.Should().BeEmpty();
        machine.OverrideLatency.Should().Be(0);
        machine.OversampleFactor.Should().Be(1);

        machine.ParameterGroups.Should().HaveCount(3);
        machine.ParameterGroups[0].Machine.Should().Be(machine);
        machine.ParameterGroups[0].TrackCount.Should().Be(0);

        machine.ParameterGroups[0].Parameters.Should().HaveCount(2);
        AssertParameter(
            parameter: machine.ParameterGroups[0].Parameters[0], 
            expectedParameter: ExpectedParameter.Amp(),
            expectedParentGroup: machine.ParameterGroups[0], 
            expectedIndexInGroup: 0);
        AssertParameter(
            parameter: machine.ParameterGroups[0].Parameters[1],
            expectedParameter: ExpectedParameter.Pan(),
            expectedParentGroup: machine.ParameterGroups[0],
            expectedIndexInGroup: 1);


        //TODO:
        var secondGroupParameters = machine.ParameterGroups[1].Parameters;
        secondGroupParameters.Should().HaveCount(3);

        AssertParameter(
          parameter: secondGroupParameters[0], 
          expectedParameter: ExpectedParameter.Volume(),
          expectedParentGroup: machine.ParameterGroups[1], 
          expectedIndexInGroup: 0);

        AssertParameter(
          parameter: secondGroupParameters[1], 
          expectedParameter: ExpectedParameter.Bpm(),
          expectedParentGroup: machine.ParameterGroups[1], 
          expectedIndexInGroup: 1);

        AssertParameter(
          parameter: secondGroupParameters[2], 
          expectedParameter: ExpectedParameter.Tpb(),
          expectedParentGroup: machine.ParameterGroups[1], 
          expectedIndexInGroup: 2);

        machine.Patterns.Should().BeEmpty();
        machine.PerformanceData.CycleCount.Should().Be(0);
        machine.PerformanceData.MaxEngineLockTime.Should().Be(0);
        machine.PerformanceData.PerformanceCount.Should().Be(0);
        machine.PerformanceData.SampleCount.Should().Be(0);
        machine.Position.Item1.Should().Be(0);
        machine.Position.Item2.Should().Be(0);
        machine.ManagedMachine.Should().BeNull();
        additionalAssertions.AssertPatternEditor(rebuzzCore, gearDir, machine);
        machine.DLL.Buzz.Should().Be(rebuzzCore);
        machine.DLL.Info.Should().BeEquivalentTo(new MachineInfo
        {

            Author = "WDE",
            Flags = MachineInfoFlags.NO_OUTPUT,
            InternalVersion = 100,
            MaxTracks = 0,
            MinTracks = 0,
            Name = "Master",
            ShortName = "Master",
            Type = MachineType.Master,
            Version = 100
        });
        machine.DLL.IsCrashed.Should().BeFalse();

        machine.DLL.IsLoaded.Should().BeTrue();
        machine.DLL.IsMissing.Should().BeFalse();
        machine.DLL.IsOutOfProcess.Should().BeFalse();
        machine.DLL.IsManaged.Should().BeTrue();
        machine.DLL.Path.Should().Be(gearDir.ToString());
        machine.DLL.Presets.Should().BeNull();
        machine.DLL.SHA1Hash.Should().BeNull();
    }

    internal static void AssertParameter(
        IParameter parameter,
        ExpectedParameter expectedParameter,
        IParameterGroup expectedParentGroup,
        int expectedIndexInGroup)
    {
        using (new AssertionScope())
        {
            expectedParameter.AssertIsMatchedBy(
                name: parameter.Name,
                description: parameter.Description,
                flags: parameter.Flags,
                defValue: parameter.DefValue,
                minValue: parameter.MinValue,
                maxValue: parameter.MaxValue,
                noValue: parameter.NoValue,
                type: parameter.Type);
            parameter.Group.Should().Be(expectedParentGroup);
            parameter.IndexInGroup.Should().Be(expectedIndexInGroup);
        }
    }

    private static void AssertParameter(
      MachineParameter parameter, ExpectedParameter expectedParameter)
    {
        expectedParameter.AssertIsMatchedBy(
            name: parameter.Name,
            description: parameter.Description,
            flags: parameter.Flags,
            defValue: parameter.DefValue,
            minValue: parameter.MinValue,
            maxValue: parameter.MaxValue,
            noValue: parameter.NoValue,
            type: parameter.Type);
    }

    internal static void AssertInitialPerformanceData(MachinePerformanceData performanceData)
    {
        performanceData.CycleCount.Should().Be(0);
        performanceData.MaxEngineLockTime.Should().Be(0);
        performanceData.PerformanceCount.Should().Be(0);
        performanceData.SampleCount.Should().Be(0);
    }

    internal static void AssertFakeModernPatternEditor(
        IBuzzMachine managedMachine, ManagedMachineHost managedMachineHost)
    {
        managedMachine.Should().BeEquivalentTo(new FakeModernPatternEditor(managedMachineHost));
    }

    internal static void AssertMasterInfoInitialState(MasterInfo masterInfo)
    {
        masterInfo.BeatsPerMin.Should().Be(126);
        masterInfo.GrooveData.Should().Be(0);
        masterInfo.GrooveSize.Should().Be(0);
        masterInfo.PosInGroove.Should().Be(0);
        masterInfo.PosInTick.Should().Be(0);
        masterInfo.SamplesPerSec.Should().Be(44100);
        masterInfo.TicksPerBeat.Should().Be(4);
        masterInfo.TicksPerSec.Should().Be(8.4f);
    }

    internal static void AssertSubTickInfoInitialState(SubTickInfo subTickInfo)
    {
        subTickInfo.CurrentSubTick.Should().Be(0);
        subTickInfo.PosInSubTick.Should().Be(0);
        subTickInfo.SamplesPerSubTick.Should().Be(262);
        subTickInfo.SubTicksPerTick.Should().Be(20);
    }

    internal static void AssertFakeModernPatternEditorHostInitialState(
        ReBuzzCore reBuzzCore, ManagedMachineHost managedMachineHost)
    {
        AssertFakeModernPatternEditor(managedMachineHost.ManagedMachine, managedMachineHost);
        AssertMasterInfoInitialState(managedMachineHost.MasterInfo);
        AssertSubTickInfoInitialState(managedMachineHost.SubTickInfo);
        managedMachineHost.Commands.Should().BeEquivalentTo(new FakeModernPatternEditor(managedMachineHost).Commands);
        managedMachineHost.InputChannelCount.Should().Be(1);
        managedMachineHost.OutputChannelCount.Should().Be(1);
        managedMachineHost.IsControlMachine.Should().BeFalse();
        managedMachineHost.Latency.Should().Be(0);
        managedMachineHost.Machine.Should()
            .Be(reBuzzCore.MachineManager.ManagedMachines.Keys.Single(machine =>
                machine.Name == managedMachineHost.Machine.Name));
        managedMachineHost.MachineState.Should().NotBeEmpty();
        managedMachineHost.PatternEditorControl.Should().BeNull();
    }
}