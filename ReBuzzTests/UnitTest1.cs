using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
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

    rebuzzCore.SongCore.Machines[0].Name.Should().Be("Master");
    rebuzzCore.SongCore.Machines[0].Attributes.Should().BeEmpty();
    rebuzzCore.SongCore.Machines[0].BaseOctave.Should().Be(4);
    rebuzzCore.SongCore.Machines[0].Commands.Should().BeEmpty();
    rebuzzCore.SongCore.Machines[0].Data.Should().BeNull();
    rebuzzCore.SongCore.Machines[0].EnvelopeNames.Should().BeEmpty();
    rebuzzCore.SongCore.Machines[0].HasStereoInput.Should().BeTrue();
    rebuzzCore.SongCore.Machines[0].HasStereoOutput.Should().BeTrue();
    rebuzzCore.SongCore.Machines[0].Graph.Should().Be(rebuzzCore.SongCore);
    rebuzzCore.SongCore.Machines[0].InputChannelCount.Should().Be(1);
    rebuzzCore.SongCore.Machines[0].OutputChannelCount.Should().Be(0);
    rebuzzCore.SongCore.Machines[0].Inputs.Should().BeEmpty();
    rebuzzCore.SongCore.Machines[0].IsActive.Should().BeFalse();
    rebuzzCore.SongCore.Machines[0].IsBypassed.Should().BeFalse();
    rebuzzCore.SongCore.Machines[0].IsControlMachine.Should().BeFalse();
    rebuzzCore.SongCore.Machines[0].IsMuted.Should().BeFalse();
    rebuzzCore.SongCore.Machines[0].IsSoloed.Should().BeFalse();
    rebuzzCore.SongCore.Machines[0].IsWireless.Should().BeFalse();
    rebuzzCore.SongCore.Machines[0].TrackCount.Should().Be(0);
    rebuzzCore.SongCore.Machines[0].LastEngineThread.Should().Be(0);
    rebuzzCore.SongCore.Machines[0].Latency.Should().Be(0);
    rebuzzCore.SongCore.Machines[0].MIDIInputChannel.Should().Be(-1);
    rebuzzCore.SongCore.Machines[0].Outputs.Should().BeEmpty();
    rebuzzCore.SongCore.Machines[0].OverrideLatency.Should().Be(0);
    rebuzzCore.SongCore.Machines[0].OversampleFactor.Should().Be(1);

    rebuzzCore.SongCore.Machines[0].ParameterGroups.Should().HaveCount(3);
    rebuzzCore.SongCore.Machines[0].ParameterGroups[0].Machine.Should().Be(rebuzzCore.SongCore.Machines[0]);
    rebuzzCore.SongCore.Machines[0].ParameterGroups[0].TrackCount.Should().Be(0);

    rebuzzCore.SongCore.Machines[0].ParameterGroups[0].Parameters.Should().HaveCount(2);
    AssertParameter(
      parameter: rebuzzCore.SongCore.Machines[0].ParameterGroups[0].Parameters[0],
      expectedName: "Amp",
      expectedDescription: "Amp (0=0%, 4000=100%, FFFE=~400%)",
      expectedFlags: ParameterFlags.State,
      expectedDefault: 16384,
      expectedParentGroup: rebuzzCore.SongCore.Machines[0].ParameterGroups[0],
      expectedIndexInGroup: 0,
      expectedMinValue: 0,
      expectedMaxValue: ushort.MaxValue - 1,
      expectedNoValue: 0,
      expectedType: ParameterType.Word);
    AssertParameter(
      parameter: rebuzzCore.SongCore.Machines[0].ParameterGroups[0].Parameters[1],
      expectedName: "Pan",
      expectedDescription: "Pan (0=Left, 4000=Center, 8000=Right)",
      expectedFlags: ParameterFlags.State,
      expectedDefault: 16384,
      expectedParentGroup: rebuzzCore.SongCore.Machines[0].ParameterGroups[0],
      expectedIndexInGroup: 1,
      expectedMinValue: 0,
      expectedMaxValue: short.MaxValue + 1,
      expectedNoValue: 0,
      expectedType: ParameterType.Word);


    //TODO:
    var secondGroupParameters = rebuzzCore.SongCore.Machines[0].ParameterGroups[1].Parameters;
    secondGroupParameters.Should().HaveCount(3);

    AssertParameter(
      parameter: secondGroupParameters[0],
      expectedName: "Volume",
      expectedDescription: "Master Volume (0=0 dB, 4000=-80 dB)",
      expectedFlags: ParameterFlags.State,
      expectedDefault: 0,
      expectedParentGroup: rebuzzCore.SongCore.Machines[0].ParameterGroups[1],
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
      expectedParentGroup: rebuzzCore.SongCore.Machines[0].ParameterGroups[1],
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
      expectedParentGroup: rebuzzCore.SongCore.Machines[0].ParameterGroups[1],
      expectedIndexInGroup: 2,
      expectedMinValue: 1,
      expectedMaxValue: 32,
      expectedNoValue: 255,
      expectedType: ParameterType.Byte);

    rebuzzCore.SongCore.Machines[0].Patterns.Should().BeEmpty();
    rebuzzCore.SongCore.Machines[0].PerformanceData.CycleCount.Should().Be(0);
    rebuzzCore.SongCore.Machines[0].PerformanceData.MaxEngineLockTime.Should().Be(0);
    rebuzzCore.SongCore.Machines[0].PerformanceData.PerformanceCount.Should().Be(0);
    rebuzzCore.SongCore.Machines[0].PerformanceData.SampleCount.Should().Be(0);
    rebuzzCore.SongCore.Machines[0].Position.Item1.Should().Be(0);
    rebuzzCore.SongCore.Machines[0].Position.Item2.Should().Be(0);
    rebuzzCore.SongCore.Machines[0].ManagedMachine.Should().BeNull();
    rebuzzCore.SongCore.Machines[0].PatternEditorDLL.Should().Be(rebuzzCore.SongCore.Machines[0].DLL);
    rebuzzCore.SongCore.Machines[0].DLL.Buzz.Should().Be(rebuzzCore);
    rebuzzCore.SongCore.Machines[0].DLL.Info.Should().BeEquivalentTo(new MachineInfo
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
    rebuzzCore.SongCore.Machines[0].DLL.IsCrashed.Should().BeFalse();

    rebuzzCore.SongCore.Machines[0].DLL.IsLoaded.Should().BeTrue();
    rebuzzCore.SongCore.Machines[0].DLL.IsMissing.Should().BeFalse();
    rebuzzCore.SongCore.Machines[0].DLL.IsOutOfProcess.Should().BeFalse();
    rebuzzCore.SongCore.Machines[0].DLL.IsManaged.Should().BeTrue();
    rebuzzCore.SongCore.Machines[0].DLL.Path.Should().Be(AbsoluteDirectoryPath.OfExecutingAssembly().AddDirectoryName("Gear").ToString());
    rebuzzCore.SongCore.Machines[0].DLL.Presets.Should().BeNull();
    rebuzzCore.SongCore.Machines[0].DLL.SHA1Hash.Should().BeNull();



    rebuzzCore.Song.Should().BeSameAs(rebuzzCore.SongCore);

    //TODO
    //bug

    
    // Additional assertions for ReBuzzCore properties
    rebuzzCore.ThemeColors.Should().BeNull();
    rebuzzCore.MachineIndex.Should().BeNull();
    rebuzzCore.MIDIFocusMachine.Should().BeNull();
    rebuzzCore.MIDIFocusLocked.Should().BeFalse();
    rebuzzCore.MIDIActivity.Should().BeFalse();
    rebuzzCore.IsPianoKeyboardVisible.Should().BeFalse();
    rebuzzCore.IsSettingsWindowVisible.Should().BeFalse();
    rebuzzCore.IsCPUMonitorWindowVisible.Should().BeFalse();
    rebuzzCore.IsHardDiskRecorderWindowVisible.Should().BeFalse();
    rebuzzCore.IsFullScreen.Should().BeFalse();
    rebuzzCore.MachineDLLsList.Should().BeEmpty();
    rebuzzCore.Instruments.Should().BeEmpty();
    rebuzzCore.AudioDrivers.Should().BeEmpty();
    rebuzzCore.SelectedAudioDriver.Should().BeNullOrEmpty();
    rebuzzCore.SelectedAudioDriverSampleRate.Should().Be(0);
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