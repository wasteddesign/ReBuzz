using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Windows;
using AtmaFileSystem;
using AtmaFileSystem.IO;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using FluentAssertions;
using ReBuzz.Audio;
using ReBuzz.Core;
using ReBuzz.MachineManagement;
using ReBuzz.ManagedMachine;

namespace ReBuzzTests;

public class Driver : IDisposable
{
  private readonly AbsoluteDirectoryPath _gearDir;
  private readonly AbsoluteDirectoryPath _themesDir;
  private ReBuzzCore _reBuzzCore;
  private readonly AbsoluteDirectoryPath _gearEffectsDir;
  private readonly AbsoluteDirectoryPath _gearGeneratorsDir;

  static Driver()
  {
    if (Application.Current == null) //bug hack
    {
      new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
    }

    AssertionOptions.FormattingOptions.MaxLines = 10000;
  }

  public Driver()
  {
    var rebuzzRootDir = AbsoluteDirectoryPath.Value(Global.BuzzPath);
    _gearDir = rebuzzRootDir.AddDirectoryName("Gear");
    _gearEffectsDir = _gearDir.AddDirectoryName("Effects");
    _gearGeneratorsDir = _gearDir.AddDirectoryName("Generators");
    _themesDir = rebuzzRootDir.AddDirectoryName("Themes");
  }

  public ReBuzzCore ReBuzzCore => _reBuzzCore; //bug hide

  public void AssertGearMachinesConsistOf(ImmutableList<string> expectedMachineNames)
  {
    _reBuzzCore.Gear.Machine.Select(m => m.Name).Should().Equal(expectedMachineNames);
  }

  public void Start()
  {
    SetupDirectoryStructure();
    var engineSettings = Global.EngineSettings;
    var buzzPath = Global.BuzzPath;
    _reBuzzCore = new ReBuzzCore(Global.GeneralSettings, Global.EngineSettings, Global.BuzzPath, Global.RegistryRoot, new FakeMachineDLLScanner());
    Global.Buzz = _reBuzzCore;
    _reBuzzCore.AudioEngine = new AudioEngine(_reBuzzCore, engineSettings, buzzPath);
    var songCore = new SongCore();
    _reBuzzCore.SongCore = songCore;
    songCore.BuzzCore = _reBuzzCore;
    var machineManager = new MachineManager(songCore, engineSettings, buzzPath);
    _reBuzzCore.MachineManager = machineManager;
    machineManager.Buzz = _reBuzzCore;
    
    //bug needed for new file
    _reBuzzCore.SongCore.WavetableCore = new WavetableCore(_reBuzzCore);
    _reBuzzCore.OpenFile += s => { TestContext.Out.WriteLine("OpenFile: " + s); }; //bug
    _reBuzzCore.PropertyChanged += (sender, args) => { TestContext.Out.WriteLine("PropertyChanged: " + args.PropertyName); };
    _reBuzzCore.SetPatternEditorControl += (control) => { TestContext.Out.WriteLine( "SetPatternEditorControl: " + control); };
    _reBuzzCore.ScanDlls();
    _reBuzzCore.CreateMaster();
  }

  public void NewFile()
  {
    _reBuzzCore.ExecuteCommand(BuzzCommand.NewFile);
  }

  private void SetupDirectoryStructure()
  {
    _gearDir.Create();
    _gearEffectsDir.Create();
    _gearGeneratorsDir.Create();
    _themesDir.Create();
    foreach (var gearFile in AbsoluteDirectoryPath.OfThisFile().AddDirectoryName("Gear").EnumerateFiles())
    {
      gearFile.Copy(_gearDir + gearFile.FileName(), overwrite: true);
    }
  }

  public void Dispose()
  {
    //bug not yet _reBuzzCore.ExecuteCommand(BuzzCommand.Exit); //bug logs
  }

  public void AssertRequiredPropertiesAreInitialized()
  {
    //bug is this really needed?
    ReBuzzCore.Should().NotBeNull();
    ReBuzzCore.Gear.Should().NotBeNull();
    ReBuzzCore.Gear.Machine.Should().NotBeEmpty();
    ReBuzzCore.AudioEngine.Should().NotBeNull();
    ReBuzzCore.SongCore.Should().NotBeNull();
    ReBuzzCore.SongCore.BuzzCore.Should().Be(ReBuzzCore);
    ReBuzzCore.SongCore.WavetableCore.Should().NotBeNull();
    ReBuzzCore.MachineManager.Should().NotBeNull();
  }
}