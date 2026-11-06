using AtmaFileSystem;
using AwesomeAssertions;
using BuzzGUI.Interfaces;
using ReBuzz.Core;
using ReBuzzTests.Automation;
using ReBuzzTests.Automation.TestMachines;
using ReBuzzTests.Automation.TestMachinesControllers;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace ReBuzzTests
{
    /// <summary>
    /// A .bmx song stores each machine's parameter layout (PARA section) as it was when the song
    /// was saved. When the installed version of the machine has a different layout, the saved
    /// values have to be mapped onto the current parameters instead of being read by index.
    /// Pattern data (PATT section) is stored in the saved layout too, so the song includes a pattern.
    /// A .bmxml song stores the layout too, and its pattern columns and MIDI bindings refer to
    /// parameters by their index in it.
    /// </summary>
    public class LoadingSongWithChangedParametersTests
    {
        private const string BypassAttribute = "[ParameterDecl(ValueDescriptions = [\"no\", \"yes\"])]";
        private const string BypassDecl = "public bool Bypass { get; set; }";
        private const string ExtraDecl = "[ParameterDecl(MaxValue = 127, DefValue = 64)] public int Extra { get; set; }";
        private const int ExtraDefault = 64;
        private const string PatternName = "00";
        private const int MidiChannel = 0;
        private const int MidiController = 7;

        [Test]
        public void LoadsSongSavedBeforeTheMachineAppendedAParameter()
        {
            var song = SaveSongWithBypassedEffect();

            using var driver = new Driver();
            driver.Gear.AddDynamicEffect(new ModifiedEffectInfo(BypassDecl, BypassDecl + Environment.NewLine + ExtraDecl));
            driver.Start();

            var songPath = driver.RandomSongPath();
            File.WriteAllBytes(songPath.ToString(), song);
            driver.DawCommands.LoadSong(DialogChoices.Select(songPath));

            driver.AssertNoErrorsReportedToUser();
            GlobalParameterValue(driver, "Bypass").Should().Be(1);
            GlobalParameterValue(driver, "Extra").Should().Be(ExtraDefault);
            PatternNames(driver).Should().Equal(PatternName);
        }

        [Test]
        public void LoadsSongSavedBeforeTheMachineInsertedAParameterAheadOfExistingOnes()
        {
            var song = SaveSongWithBypassedEffect();

            using var driver = new Driver();
            driver.Gear.AddDynamicEffect(new ModifiedEffectInfo(BypassAttribute, ExtraDecl + Environment.NewLine + BypassAttribute));
            driver.Start();

            var songPath = driver.RandomSongPath();
            File.WriteAllBytes(songPath.ToString(), song);
            driver.DawCommands.LoadSong(DialogChoices.Select(songPath));

            driver.AssertNoErrorsReportedToUser();
            GlobalParameterValue(driver, "Bypass").Should().Be(1);
            GlobalParameterValue(driver, "Extra").Should().Be(ExtraDefault);
            PatternNames(driver).Should().Equal(PatternName);
        }

        // Loading a .bmxml creates WPF objects, which need an STA thread
        [Test]
        [Apartment(ApartmentState.STA)]
        public void LoadsBmxmlSongSavedBeforeTheMachineAppendedAParameter()
        {
            var song = SaveSongWithBypassedEffect(".bmxml");

            using var driver = LoadSongWith(new ModifiedEffectInfo(BypassDecl, BypassDecl + Environment.NewLine + ExtraDecl), song, ".bmxml");

            AssertBypassStateWasRestored(driver);
        }

        [Test]
        [Apartment(ApartmentState.STA)]
        public void LoadsBmxmlSongSavedBeforeTheMachineInsertedAParameterAheadOfExistingOnes()
        {
            var song = SaveSongWithBypassedEffect(".bmxml");

            using var driver = LoadSongWith(new ModifiedEffectInfo(BypassAttribute, ExtraDecl + Environment.NewLine + BypassAttribute), song, ".bmxml");

            AssertBypassStateWasRestored(driver);
        }

        /// <summary>
        /// Pattern editor data refers to parameters by their index in the layout the song was saved
        /// with, so a loaded machine tells pattern editors that layout (ISavedParameterLayout).
        /// </summary>
        [TestCase(".bmx")]
        [TestCase(".bmxml")]
        [Apartment(ApartmentState.STA)]
        public void LoadedMachineMapsParametersOfTheSavedLayoutToItsCurrentOnes(string extension)
        {
            var song = SaveSongWithBypassedEffect(extension);

            using var driver = LoadSongWith(new ModifiedEffectInfo(BypassAttribute, ExtraDecl + Environment.NewLine + BypassAttribute), song, extension);

            driver.AssertNoErrorsReportedToUser();
            var layout = SavedLayout(driver);
            layout.SavedGlobalCount.Should().Be(1);
            layout.SavedGlobal0.Should().Be("Bypass");
            layout.SavedGlobal1.Should().BeNull();
        }

        [Test]
        public void MachineNotLoadedFromASongHasNoSavedParameterLayout()
        {
            using var driver = new Driver();
            driver.Gear.AddDynamicEffect(EffectController.Info);
            driver.Start();
            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(EffectController.NewInstance());

            var layout = SavedLayout(driver);
            layout.SavedGlobalCount.Should().Be(-1);
            layout.SavedGlobal0.Should().Be("Bypass");
        }

        /// <summary>
        /// The Effect's saved global parameter count, and the names of the current parameters for saved
        /// globals 0 and 1, as a pattern editor would see them.
        /// </summary>
        private static (int SavedGlobalCount, string? SavedGlobal0, string? SavedGlobal1) SavedLayout(Driver driver)
        {
            (int, string?, string?) result = default;
            driver.MachineGraph.ExecuteMachineCommand(new MachineCoreCommand(EffectController.NewInstance().InstanceName, machine =>
            {
                ISavedParameterLayout layout = machine;
                result = (layout.SavedGlobalParameterCount,
                    layout.GetCurrentParameter(1, 0)?.Name,
                    layout.GetCurrentParameter(1, 1)?.Name);
            }));
            return result;
        }

        private static Driver LoadSongWith(ModifiedEffectInfo effect, byte[] song, string extension)
        {
            var driver = new Driver();
            driver.Gear.AddDynamicEffect(effect);
            driver.Start();

            var songPath = driver.RandomSongPath(extension);
            File.WriteAllBytes(songPath.ToString(), song);
            driver.DawCommands.LoadSong(DialogChoices.Select(songPath));
            return driver;
        }

        /// <summary>
        /// The saved value, pattern column and MIDI binding all belong to Bypass, and the new
        /// parameter has its default value.
        /// </summary>
        private static void AssertBypassStateWasRestored(Driver driver)
        {
            driver.AssertNoErrorsReportedToUser();
            GlobalParameterValue(driver, "Bypass").Should().Be(1);
            GlobalParameterValue(driver, "Extra").Should().Be(ExtraDefault);
            PatternNames(driver).Should().Equal(PatternName);

            string[] columnParameters = [];
            int[] columnValues = [];
            string[] boundParameters = [];
            driver.MachineGraph.ExecuteMachineCommand(new BuzzMachineCommand(EffectController.NewInstance().InstanceName, (buzz, machine) =>
            {
                var pattern = machine.Patterns.Single(p => p.Name == PatternName);
                columnParameters = pattern.Columns.Select(c => c.Parameter.Name).ToArray();
                columnValues = pattern.Columns.SelectMany(c => c.GetEvents(0, int.MaxValue)).Select(e => e.Value).ToArray();
                boundParameters = buzz.MidiControllerAssignments.ContollerBindings
                    .Where(b => b.Parameter.Group.Machine == machine && b.MidiChannel == MidiChannel && b.MidiController == MidiController)
                    .Select(b => b.Parameter.Name).ToArray();
            }));

            columnParameters.Should().Equal("Bypass");
            columnValues.Should().Equal(1);
            boundParameters.Should().Equal("Bypass");
        }

        /// <summary>
        /// Saves a song containing the unmodified Effect with Bypass set, and returns the file's bytes.
        /// The driver's test directory is removed on dispose, so the song is carried over in memory.
        /// </summary>
        private static byte[] SaveSongWithBypassedEffect(string extension = ".bmx")
        {
            using var driver = new Driver();
            var controller = EffectController.NewInstance();
            driver.Gear.AddDynamicEffect(EffectController.Info);
            driver.Start();

            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(controller);
            driver.MachineGraph.ExecuteMachineCommand(new SetGlobalParameterCommand(controller, "Bypass", 1));
            driver.MachineGraph.ExecuteMachineCommand(new MachineCoreCommand(controller.InstanceName,
                machine => machine.CreatePattern(PatternName, 16)));
            if (extension == ".bmxml")
            {
                // A pattern column and a MIDI binding on Bypass, which .bmxml refers to by index
                driver.MachineGraph.ExecuteMachineCommand(new BuzzMachineCommand(controller.InstanceName, (buzz, machine) =>
                {
                    var bypass = Bypass(machine);
                    var pattern = machine.Patterns.Single(p => p.Name == PatternName);
                    pattern.InsertColumn(0, bypass, 0);
                    pattern.Columns[0].SetEvents([new PatternEvent(0, 1)], true);
                    buzz.MidiControllerAssignments.BindParameter(bypass, 0, MidiChannel, MidiController);
                }));
            }

            var songPath = driver.RandomSongPath(extension);
            driver.DawCommands.SaveCurrentSongForTheFirstTime(DialogChoices.Select(songPath));
            driver.AssertNoErrorsReportedToUser();

            return File.ReadAllBytes(songPath.ToString());
        }

        private static int GlobalParameterValue(Driver driver, string parameterName)
        {
            int value = 0;
            driver.MachineGraph.ExecuteMachineCommand(new MachineCoreCommand(EffectController.NewInstance().InstanceName,
                machine => value = machine.ParameterGroups.Single(g => g.Type == ParameterGroupType.Global)
                    .Parameters.Single(p => p.Name == parameterName).GetValue(0)));
            return value;
        }

        private static string[] PatternNames(Driver driver)
        {
            string[] names = [];
            driver.MachineGraph.ExecuteMachineCommand(new MachineCoreCommand(EffectController.NewInstance().InstanceName,
                machine => names = machine.Patterns.Select(p => p.Name).ToArray()));
            return names;
        }

        private static ParameterCore Bypass(MachineCore machine) =>
            machine.ParameterGroupsList.Single(g => g.Type == ParameterGroupType.Global)
                .ParametersList.Single(p => p.Name == "Bypass");

        /// <summary>
        /// Runs an action on the song's machine instance with the given name.
        /// </summary>
        private class MachineCoreCommand(string instanceName, Action<MachineCore> action) : ITestMachineInstanceCommand
        {
            public void Execute(ReBuzzCore buzzCore, ReBuzzMachines machineCores)
            {
                action(machineCores.GetSongCoreMachineInstance(instanceName));
            }
        }

        /// <summary>
        /// Runs an action on ReBuzz and the song's machine instance with the given name.
        /// </summary>
        private class BuzzMachineCommand(string instanceName, Action<ReBuzzCore, MachineCore> action) : ITestMachineInstanceCommand
        {
            public void Execute(ReBuzzCore buzzCore, ReBuzzMachines machineCores)
            {
                action(buzzCore, machineCores.GetSongCoreMachineInstance(instanceName));
            }
        }

        /// <summary>
        /// The Effect test machine with one piece of its source replaced, so it stands in for a newer
        /// version of the same machine (same name and dll name, different parameter layout).
        /// </summary>
        private class ModifiedEffectInfo(string original, string replacement) : IDynamicTestMachineInfo
        {
            MachineDLL ITestMachineInfo.GetMachineDll(ReBuzzCore buzz, AbsoluteFilePath location) =>
                EffectInfo.Instance.GetMachineDll(buzz, location);

            string ITestMachineInfo.DllName => EffectInfo.Instance.DllName;
            string ITestMachineInfo.RealDllName => EffectInfo.Instance.RealDllName;

            string IDynamicTestMachineInfo.SourceCode
            {
                get
                {
                    var source = EffectInfo.Instance.SourceCode;
                    source.Should().Contain(original);
                    return source.Replace(original, replacement);
                }
            }
        }
    }
}
