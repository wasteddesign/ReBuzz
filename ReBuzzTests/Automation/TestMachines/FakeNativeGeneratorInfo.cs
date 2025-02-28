using AtmaFileSystem;
using BuzzGUI.Interfaces;
using ReBuzz.Core;

namespace ReBuzzTests.Automation.TestMachines
{
    public class FakeNativeGeneratorInfo //bug move to another folder
    {
        public static FakeNativeGeneratorInfo Instance { get; } = new FakeNativeGeneratorInfo();

        internal MachineDLL GetMachineDll(ReBuzzCore buzz, AbsoluteFilePath location)
        {
            return new MachineDLL
            {
                Name = "Fake Native Generator",
                Buzz = buzz,
                Path = location.ToString(),
                Is64Bit = true,
                IsCrashed = false,
                IsManaged = false,
                IsLoaded = false,
                IsMissing = false,
                IsOutOfProcess = false,
                ManagedDLL = null,
                MachineInfo = new MachineInfo
                {
                    Flags = MachineInfoFlags.LOAD_DATA_RUNTIME,
                    Author = "WDE",
                    InternalVersion = 0,
                    MaxTracks = 1,
                    MinTracks = 1,
                    Name = "Fake Native Generator",
                    ShortName = "FakeNativeGen",
                    Type = MachineType.Generator,
                    Version = 66
                },
                Presets = null,
                SHA1Hash = "258A3DE5BA33E71D69271E36557EA8E4E582298E",
                GUIFactoryDecl =
                    new MachineGUIFactoryDecl
                    {
                        IsGUIResizable = true,
                        PreferWindowedGUI = true,
                        UseThemeStyles = false
                    },
                ModuleHandle = 0
            };
        }

        public string DllName => "FakeNativeGenerator.dll";
    }
}
