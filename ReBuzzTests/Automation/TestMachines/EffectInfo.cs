using AtmaFileSystem;
using BuzzGUI.Interfaces;
using ReBuzz.Core;

namespace ReBuzzTests.Automation.TestMachines
{
    /// <summary>
    /// Contains additional information about the fake Effect machine.
    ///
    /// This information is awkward to put in the <see cref="Effect"/>
    /// class because it would require a reference to the types defined in this project.
    /// </summary>
    public class EffectInfo : ITestMachineInfo
    {
        public static ITestMachineInfo Instance { get; } = new EffectInfo();
        MachineDLL ITestMachineInfo.GetMachineDll(ReBuzzCore buzz, AbsoluteFilePath location)
        {
            var decl = Effect.GetMachineDecl();
            return new MachineDLL
            {
                Name = decl.Name,
                Buzz = buzz,
                Path = location.ToString(),
                Is64Bit = true,
                IsCrashed = false,
                IsManaged = true,
                IsLoaded = false,
                IsMissing = false,
                IsOutOfProcess = false,
                ManagedDLL = null,
                MachineInfo = new MachineInfo
                {
                    Flags = MachineInfoFlags.LOAD_DATA_RUNTIME,
                    Author = decl.Author,
                    InternalVersion = 0,
                    MaxTracks = decl.MaxTracks,
                    MinTracks = 1,
                    Name = decl.Name,
                    ShortName = decl.ShortName,
                    Type = MachineType.Effect,
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

        public string DllName => Effect.GetMachineDecl().Name + ".dll";
        public string SourceCode => Effect.GetSourceCode();
    }
}