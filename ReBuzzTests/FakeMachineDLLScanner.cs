using System;
using System.Collections.Generic;
using AtmaFileSystem;
using BuzzGUI.Interfaces;
using ReBuzz.Core;
using ReBuzz.FileOps;

namespace ReBuzzTests;

internal class FakeMachineDLLScanner : IMachineDLLScanner //bug move
{
  public Dictionary<string, MachineDLL> GetMachineDLLs(ReBuzzCore buzz, string buzzPath)
  {
    var assemblyLocation = AbsoluteDirectoryPath.OfCurrentWorkingDirectory().AddFileName("StubMachine.dll"); //bug
    //bug delete the test machines
    //bug set the buzz path correctly
    DynamicCompiler.CompileAndSave(FakeModernPatternEditor.GetSourceCode(), assemblyLocation);

    return new Dictionary<string, MachineDLL>() //bug fill some of this stuff from machine decl
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