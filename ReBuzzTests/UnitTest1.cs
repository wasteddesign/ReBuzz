using FluentAssertions.Execution;
using ReBuzz;
using System.Diagnostics;

namespace ReBuzzTests;

public class Tests
{


  [Test]
  [Apartment(ApartmentState.STA)]
  public void ReadsGearFilesOnCreation()
  {
    using var driver = new Driver();

    driver.Start();

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

  }

}