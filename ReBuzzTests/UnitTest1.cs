using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using AtmaFileSystem;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using FluentAssertions;
using FluentAssertions.Execution;
using ReBuzz.Core;
using ReBuzz.FileOps;
using ReBuzz.Midi;

namespace ReBuzzTests;

public class Tests
{
  private Dictionary<string, Color> colorDictionary = new Dictionary<string, Color>
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

  [Test]
  public void ReadsGearFilesOnCreation()
  {
    using var driver = new Driver();

    driver.Start();

    driver.AssertRequiredPropertiesAreInitialized();
    driver.AssertGearMachinesConsistOf([
      "Jeskola Pianoroll",
      "Modern Pattern Editor",
      "Jeskola Pattern XP",
      "Jeskola Pattern XP mod",
      "Modern Pianoroll",
      "Polac VST 1.1",
      "Polac VSTi 1.1",
      "Jeskola XS-1",
      "CyanPhase Buzz OverLoader",
      "CyanPhase DX Instrument Adapter",
      "CyanPhase DX Effect Adapter",
      "CyanPhase DMO Effect Adapter",
      "11-MidiCCout",
      "Rymix*",
      "FireSledge ParamEQ",
      "BTDSys Pulsar"
    ]);

    driver.NewFile();

    var rebuzzCore = driver.ReBuzzCore;
    
    // Assertions for ReBuzzCore properties
    rebuzzCore.HostVersion.Should().Be(66);
    rebuzzCore.BPM.Should().Be(126);
    rebuzzCore.TPB.Should().Be(4);
    rebuzzCore.Speed.Should().Be(0);
    rebuzzCore.Playing.Should().BeFalse();
    rebuzzCore.Recording.Should().BeFalse();
    rebuzzCore.Looping.Should().BeFalse();
    rebuzzCore.AudioDeviceDisabled.Should().BeFalse();
    rebuzzCore.MIDIControllers.Should().BeEmpty();
    //bug different results with R# and NCrunch: rebuzzCore.Theme.Should().BeEquivalentTo(new ReBuzzTheme());
    rebuzzCore.VUMeterLevel.Item1.Should().Be(0.0); 
    rebuzzCore.VUMeterLevel.Item2.Should().Be(0.0);
    rebuzzCore.MidiControllerAssignments.MIDIControllers.Should().BeEmpty();
    rebuzzCore.MidiControllerAssignments.ReBuzzMIDIControllers.Should().BeEmpty();
    rebuzzCore.MidiControllerAssignments.Song.Should().Be(rebuzzCore.SongCore);
    
    rebuzzCore.SongCore.BuzzCore.Should().Be(rebuzzCore);
    rebuzzCore.SongCore.ActionStack.Actions.Should().BeEmpty();
    rebuzzCore.SongCore.ActionStack.CanRedo.Should().BeFalse();
    rebuzzCore.SongCore.ActionStack.CanUndo.Should().BeFalse();
    rebuzzCore.SongCore.ActionStack.MaxNumberOfActions.Should().Be(int.MaxValue);
    rebuzzCore.SongCore.Associations.Should().BeEmpty();
    rebuzzCore.SongCore.CanRedo.Should().BeFalse();
    rebuzzCore.SongCore.CanUndo.Should().BeFalse();
    rebuzzCore.SongCore.LoopStart.Should().Be(0);
    rebuzzCore.SongCore.LoopEnd.Should().Be(16);
    rebuzzCore.SongCore.PlayPosition.Should().Be(0);
    rebuzzCore.SongCore.Sequences.Should().BeEmpty();
    rebuzzCore.SongCore.SequencesList.Should().BeEmpty();
    rebuzzCore.SongCore.SongName.Should().BeNullOrEmpty();
    rebuzzCore.SongCore.SoloMode.Should().BeFalse();
    rebuzzCore.SongCore.Wavetable.Song.Should().Be(rebuzzCore.Song);
    rebuzzCore.SongCore.Wavetable.Volume.Should().Be(0);
    rebuzzCore.SongCore.Wavetable.Waves.Should().Equal(Enumerable.Range(0,200).Select(_ => null as IWave).ToArray());
    rebuzzCore.SongCore.Machines.Should().HaveCount(1);
    rebuzzCore.SongCore.MachinesList.Should().Equal(rebuzzCore.SongCore.Machines.Cast<MachineCore>());

    AssertIsMasterMachine(rebuzzCore.SongCore.Machines[0], rebuzzCore);

    rebuzzCore.Song.Should().BeSameAs(rebuzzCore.SongCore);

    // Additional assertions for ReBuzzCore properties
    
    foreach (var (key, value) in colorDictionary)
    {
      rebuzzCore.ThemeColors[key].Should().Be(value);
    }

    rebuzzCore.MachineIndex.Should().BeEquivalentTo(new MenuItemCore());
    rebuzzCore.MIDIFocusMachine.Should().Be(rebuzzCore.SongCore.MachinesList[0]);
    rebuzzCore.MIDIFocusLocked.Should().BeFalse();
    rebuzzCore.MIDIActivity.Should().BeFalse();
    rebuzzCore.IsPianoKeyboardVisible.Should().BeFalse();
    rebuzzCore.IsSettingsWindowVisible.Should().BeFalse();
    rebuzzCore.IsCPUMonitorWindowVisible.Should().BeFalse();
    rebuzzCore.IsHardDiskRecorderWindowVisible.Should().BeFalse();
    rebuzzCore.IsFullScreen.Should().BeFalse();
    rebuzzCore.Instruments.Should().BeEmpty();
    rebuzzCore.AudioDrivers.Should().NotBeEmpty();
    rebuzzCore.SelectedAudioDriver.Should().BeNullOrEmpty();
    rebuzzCore.SelectedAudioDriverSampleRate.Should().Be(44100);
    rebuzzCore.MachineDLLsList.Should().HaveCount(1);
    var modernPatternEditor = rebuzzCore.MachineDLLsList["Modern Pattern Editor"];
    modernPatternEditor.Buzz.Should().Be(rebuzzCore);
    modernPatternEditor.Path.Should().Be(AbsoluteDirectoryPath.OfExecutingAssembly().AddFileName("StubMachine.dll").ToString()); //bug parameterize
    modernPatternEditor.Info.Name.Should().Be(FakeModernPatternEditor.GetMachineDecl().Name);
    modernPatternEditor.Info.Author.Should().Be(FakeModernPatternEditor.GetMachineDecl().Author);
    modernPatternEditor.Info.Flags.Should().Be(MachineInfoFlags.STEREO_EFFECT | MachineInfoFlags.LOAD_DATA_RUNTIME);
    //bug more assertions and take some of this from the fake machine scanner.

    //
    //// Assertions for static properties
    ReBuzzCore.buildNumber.Should().Be(0);
    ReBuzzCore.AppDataPath.Should().Be("ReBuzz");
    ReBuzzCore.GlobalState.AudioFrame.Should().Be(0);
    ReBuzzCore.GlobalState.ADWritePos.Should().Be(0);
    ReBuzzCore.GlobalState.ADPlayPos.Should().Be(0);
    ReBuzzCore.GlobalState.SongPosition.Should().Be(0);
    ReBuzzCore.GlobalState.LoopStart.Should().Be(0);
    ReBuzzCore.GlobalState.LoopEnd.Should().Be(0);
    ReBuzzCore.GlobalState.SongEnd.Should().Be(0);
    ReBuzzCore.GlobalState.StateFlags.Should().Be(0);
    ReBuzzCore.GlobalState.MIDIFiltering.Should().Be(0);
    ReBuzzCore.GlobalState.SongClosing.Should().Be(0);
  }

  private static void AssertIsMasterMachine(IMachine machine, ReBuzzCore rebuzzCore)
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
      expectedName: "Amp",
      expectedDescription: "Amp (0=0%, 4000=100%, FFFE=~400%)",
      expectedFlags: ParameterFlags.State,
      expectedDefault: 16384,
      expectedParentGroup: machine.ParameterGroups[0],
      expectedIndexInGroup: 0,
      expectedMinValue: 0,
      expectedMaxValue: ushort.MaxValue - 1,
      expectedNoValue: 0,
      expectedType: ParameterType.Word);
    AssertParameter(
      parameter: machine.ParameterGroups[0].Parameters[1],
      expectedName: "Pan",
      expectedDescription: "Pan (0=Left, 4000=Center, 8000=Right)",
      expectedFlags: ParameterFlags.State,
      expectedDefault: 16384,
      expectedParentGroup: machine.ParameterGroups[0],
      expectedIndexInGroup: 1,
      expectedMinValue: 0,
      expectedMaxValue: short.MaxValue + 1,
      expectedNoValue: 0,
      expectedType: ParameterType.Word);


    //TODO:
    var secondGroupParameters = machine.ParameterGroups[1].Parameters;
    secondGroupParameters.Should().HaveCount(3);

    AssertParameter(
      parameter: secondGroupParameters[0],
      expectedName: "Volume",
      expectedDescription: "Master Volume (0=0 dB, 4000=-80 dB)",
      expectedFlags: ParameterFlags.State,
      expectedDefault: 0,
      expectedParentGroup: machine.ParameterGroups[1],
      expectedIndexInGroup: 0,
      expectedMinValue: 0,
      expectedMaxValue: 16384,
      expectedNoValue: ushort.MaxValue,
      expectedType: ParameterType.Word);

    AssertParameter(
      parameter: secondGroupParameters[1],
      expectedName: "BPM",
      expectedDescription: "Beats Per Minute (10-200 hex)",
      expectedFlags: ParameterFlags.State,
      expectedDefault: 126,
      expectedParentGroup: machine.ParameterGroups[1],
      expectedIndexInGroup: 1,
      expectedMinValue: 10,
      expectedMaxValue: 512,
      expectedNoValue: 65535,
      expectedType: ParameterType.Word);

    AssertParameter(
      parameter: secondGroupParameters[2],
      expectedName: "TPB",
      expectedDescription: "Ticks Per Beat (1-20 hex)",
      expectedFlags: ParameterFlags.State,
      expectedDefault: 4,
      expectedParentGroup: machine.ParameterGroups[1],
      expectedIndexInGroup: 2,
      expectedMinValue: 1,
      expectedMaxValue: 32,
      expectedNoValue: 255,
      expectedType: ParameterType.Byte);

    machine.Patterns.Should().BeEmpty();
    machine.PerformanceData.CycleCount.Should().Be(0);
    machine.PerformanceData.MaxEngineLockTime.Should().Be(0);
    machine.PerformanceData.PerformanceCount.Should().Be(0);
    machine.PerformanceData.SampleCount.Should().Be(0);
    machine.Position.Item1.Should().Be(0);
    machine.Position.Item2.Should().Be(0);
    machine.ManagedMachine.Should().BeNull();
    machine.PatternEditorDLL.Should().BeNull(); //bug investigate!!
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
    machine.DLL.Path.Should().Be(AbsoluteDirectoryPath.OfExecutingAssembly().AddDirectoryName("Gear").ToString());
    machine.DLL.Presets.Should().BeNull();
    machine.DLL.SHA1Hash.Should().BeNull();
  }

  private static void AssertParameter(
    IParameter parameter,
    string expectedName,
    string expectedDescription,
    ParameterFlags expectedFlags,
    int expectedDefault,
    IParameterGroup expectedParentGroup,
    int expectedIndexInGroup,
    int expectedMinValue,
    int expectedMaxValue,
    int expectedNoValue,
    ParameterType expectedType)
  {
    using (new AssertionScope())
    {
      parameter.Name.Should().Be(expectedName);
      parameter.Description.Should().Be(expectedDescription);
      parameter.Flags.Should().Be(expectedFlags);
      parameter.DefValue.Should().Be(expectedDefault);
      parameter.Group.Should().Be(expectedParentGroup);
      parameter.IndexInGroup.Should().Be(expectedIndexInGroup);
      parameter.MinValue.Should().Be(expectedMinValue);
      parameter.MaxValue.Should().Be(expectedMaxValue);
      parameter.NoValue.Should().Be(expectedNoValue);
      parameter.Type.Should().Be(expectedType);
    }
  }
}