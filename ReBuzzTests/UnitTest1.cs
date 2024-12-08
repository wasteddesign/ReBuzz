using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using AtmaFileSystem;
using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using FluentAssertions;
using FluentAssertions.Execution;
using ReBuzz.Core;
using ReBuzz.FileOps;
using ReBuzz.MachineManagement;
using ReBuzz.ManagedMachine;
using ReBuzz.Midi;

namespace ReBuzzTests;

public class Tests
{
  [Test]
  public void ReadsGearFilesOnCreation([Values(1,2)] int x)
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

    driver.AssertInitialState();
  }
}