using System.Collections.Immutable;
using System.Windows;
using AtmaFileSystem;
using AtmaFileSystem.IO;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using FluentAssertions;
using ReBuzz.Audio;
using ReBuzz.Core;
using ReBuzz.MachineManagement;

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

  public void AssertGearMachinesConsistOf(ImmutableList<string> expectedMachineNames)
  {
    _reBuzzCore.Gear.Machine.Select(m => m.Name).Should().Equal(expectedMachineNames);
  }

  public void Start()
  {
    SetupDirectoryStructure();
    _reBuzzCore = new ReBuzzCore();
    _reBuzzCore.AudioEngine = new AudioEngine(_reBuzzCore);
    var songCore = new SongCore();
    _reBuzzCore.SongCore = songCore;
    songCore.BuzzCore = _reBuzzCore;
    var machineManager = new MachineManager(songCore);
    _reBuzzCore.MachineManager = machineManager;
    machineManager.Buzz = _reBuzzCore;
    
    //bug needed for new file
    _reBuzzCore.SongCore.WavetableCore = new WavetableCore(_reBuzzCore);
    _reBuzzCore.OpenFile += s => { }; //bug
    _reBuzzCore.PropertyChanged += (sender, args) => { };
    _reBuzzCore.ScanDlls();
    _reBuzzCore.CreateMaster();

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
}