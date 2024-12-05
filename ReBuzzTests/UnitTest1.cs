using System.Threading;
using System.Windows;
using FluentAssertions;
using FluentAssertions.Execution;
using ReBuzz.Core;

namespace ReBuzzTests;

public class Tests
{
  [Test]
  public void ReadsGearFilesOnCreation([Values(1,2)] int z)
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

    //var rebuzzCore = driver.ReBuzzCore;
    //
    //// Assertions for ReBuzzCore properties
    //rebuzzCore.HostVersion.Should().Be(66);
    //rebuzzCore.BPM.Should().Be(126);
    //rebuzzCore.TPB.Should().Be(4);
    //rebuzzCore.Speed.Should().Be(0);
    //rebuzzCore.Playing.Should().BeFalse();
    //rebuzzCore.Recording.Should().BeFalse();
    //rebuzzCore.Looping.Should().BeFalse();
    //rebuzzCore.AudioDeviceDisabled.Should().BeFalse();
    //rebuzzCore.MIDIControllers.Should().BeEmpty();
    //rebuzzCore.Theme.Should().BeNull();
    //rebuzzCore.VUMeterLevel.Should().BeNull();
    //rebuzzCore.MidiControllerAssignments.Should().BeNull();
    //
    //// Additional assertions for ReBuzzCore properties
    //rebuzzCore.ThemeColors.Should().BeNull();
    //rebuzzCore.MachineIndex.Should().BeNull();
    //rebuzzCore.MIDIFocusMachine.Should().BeNull();
    //rebuzzCore.MIDIFocusLocked.Should().BeFalse();
    //rebuzzCore.MIDIActivity.Should().BeFalse();
    //rebuzzCore.IsPianoKeyboardVisible.Should().BeFalse();
    //rebuzzCore.IsSettingsWindowVisible.Should().BeFalse();
    //rebuzzCore.IsCPUMonitorWindowVisible.Should().BeFalse();
    //rebuzzCore.IsHardDiskRecorderWindowVisible.Should().BeFalse();
    //rebuzzCore.IsFullScreen.Should().BeFalse();
    //rebuzzCore.MachineDLLsList.Should().BeEmpty();
    //rebuzzCore.Instruments.Should().BeEmpty();
    //rebuzzCore.AudioDrivers.Should().BeEmpty();
    //rebuzzCore.SelectedAudioDriver.Should().BeNullOrEmpty();
    //rebuzzCore.SelectedAudioDriverSampleRate.Should().Be(0);
    //
    //// Assertions for static properties
    //ReBuzzCore.buildNumber.Should().Be(0);
    //ReBuzzCore.AppDataPath.Should().Be("ReBuzz");
    //ReBuzzCore.GlobalState.AudioFrame.Should().Be(0);
    //ReBuzzCore.GlobalState.ADWritePos.Should().Be(0);
    //ReBuzzCore.GlobalState.ADPlayPos.Should().Be(0);
    //ReBuzzCore.GlobalState.SongPosition.Should().Be(0);
    //ReBuzzCore.GlobalState.LoopStart.Should().Be(0);
    //ReBuzzCore.GlobalState.LoopEnd.Should().Be(0);
    //ReBuzzCore.GlobalState.SongEnd.Should().Be(0);
    //ReBuzzCore.GlobalState.StateFlags.Should().Be(0);
    //ReBuzzCore.GlobalState.MIDIFiltering.Should().Be(0);
    //ReBuzzCore.GlobalState.SongClosing.Should().Be(0);
  }
}