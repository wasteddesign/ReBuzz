using System;
using System.Collections.Generic;
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
using ReBuzz.FileOps;
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

  public void AssertGearMachinesConsistOf(ImmutableList<string> expectedMachineNames)
  {
    _reBuzzCore.Gear.Machine.Select(m => m.Name).Should().Equal(expectedMachineNames);
  }

  public void Start()
  {
    SetupDirectoryStructure();
    var generalSettings = Global.GeneralSettings;
    var engineSettings = Global.EngineSettings;
    var registryRoot = Global.RegistryRoot;
    //bug var buzzPath = Global.BuzzPath;
    var buzzPath = "C:\\Program Files\\ReBuzz\\"; //bug
    _reBuzzCore = new ReBuzzCore(generalSettings, engineSettings, buzzPath, registryRoot, new FakeMachineDLLScanner());
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
    _reBuzzCore.OpenFile += s => { }; //bug
    _reBuzzCore.PropertyChanged += (sender, args) => { };
    _reBuzzCore.SetPatternEditorControl += (control) => { };
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
}

internal class FakeMachineDLLScanner : IMachineDLLScanner
{
  public Dictionary<string, MachineDLL> GetMachineDLLs(ReBuzzCore buzz, string buzzPath)
  {
    var assemblyLocation = AbsoluteDirectoryPath.OfCurrentWorkingDirectory().AddFileName("StubMachine.dll");
    //bug delete the test machines
    //bug set the buzz path correctly
    DynamicCompiler.CompileAndSave(FakeModernPatternEditor.GetSourceCode(), assemblyLocation);

    return new Dictionary<string, MachineDLL>()
    {
      ["Modern Pattern Editor"] = new()
      {
        Name = "Modern Pattern Editor",
        Buzz = buzz,
        Path = assemblyLocation.ToString(),
        Is64Bit = true,
        IsCrashed = false,
        IsManaged = true,
        IsLoaded = false,
        IsMissing = false,
        IsOutOfProcess = false,
        ManagedDLL = null,
        MachineInfo = new MachineInfo()
        {
          Flags = MachineInfoFlags.NO_OUTPUT | MachineInfoFlags.CONTROL_MACHINE | MachineInfoFlags.PATTERN_EDITOR | MachineInfoFlags.LOAD_DATA_RUNTIME,
          Author = "WDE",
          InternalVersion = 0,
          MaxTracks = 0,
          MinTracks = 0,
          Name = "Modern Pattern Editor",
          ShortName = "MPE",
          Type = MachineType.Generator,
          Version = 66
        },
        Presets = null,
        SHA1Hash = "258A3DE5BA33E71D69271E36557EA8E4E582298E",
        GUIFactoryDecl = new MachineGUIFactoryDecl {IsGUIResizable = true, PreferWindowedGUI = true, UseThemeStyles = false},
        ModuleHandle = 0,
      },
    };
  }

  public void AddMachineDllsToDictionary(XMLMachineDLL[] xMLMachineDLLs, Dictionary<string, MachineDLL> md)
  {
    
  }

  public XMLMachineDLL ValidateDll(ReBuzzCore buzz, string libName, string path, string buzzPath)
  {
    throw new NotImplementedException("should not be called");
  }
}