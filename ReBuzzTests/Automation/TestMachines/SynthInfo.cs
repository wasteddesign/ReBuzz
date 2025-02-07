using AtmaFileSystem;
using BuzzGUI.Interfaces;
using ReBuzz.Core;

namespace ReBuzzTests.Automation.TestMachines
{
    /// <summary>
    /// Contains additional information about the fake Synth machine.
    ///
    /// This information is awkward to put in the <see cref="Synth"/>
    /// class because it would require a reference to the types defined in this project.
    /// </summary>
    public static class SynthInfo
    {
        internal static MachineDLL GetMachineDll(ReBuzzCore buzz, AbsoluteFilePath location)
        {
            var decl = Synth.GetMachineDecl();
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
            }; //bug clean this up
        }

        public static string DllName => "Synth.dll";
    }
}