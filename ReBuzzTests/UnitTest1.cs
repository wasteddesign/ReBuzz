using System.Threading;
using AtmaFileSystem;
using Buzz.MachineInterface;
using FluentAssertions;
using System.Reflection;
using System.Linq;

namespace ReBuzzTests;

public class Tests
{
  [Test]
  public void CanInvokeEmittedMethods()
  {
    var assemblyLocation = AbsoluteDirectoryPath.OfExecutingAssembly().AddFileName("MyAssembly.dll");
    DynamicCompiler.CompileAndSave(FakeModernPatternEditor.GetSourceCode(), assemblyLocation);

    var assembly = Assembly.LoadFile(assemblyLocation.ToString());

    var type = assembly.ExportedTypes.Single(t => t.Name.Contains("FakeModernPatternEditor"));

    var decl = type.GetMethod("GetMachineDecl").Invoke(null, []) as MachineDecl;
    decl.Should().BeEquivalentTo(FakeModernPatternEditor.GetMachineDecl());
  }

  [Test]
  [Apartment(ApartmentState.STA)]
  public void ReadsGearFilesOnCreation()
  {
    using var driver = new Driver();

    driver.Start();

    driver.NewFile();

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