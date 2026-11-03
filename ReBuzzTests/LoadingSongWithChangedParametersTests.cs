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

namespace ReBuzzTests
{
    /// <summary>
    /// A .bmx song stores each machine's parameter layout (PARA section) as it was when the song
    /// was saved. When the installed version of the machine has a different layout, the saved
    /// values have to be mapped onto the current parameters instead of being read by index.
    /// </summary>
    public class LoadingSongWithChangedParametersTests
    {
        private const string BypassAttribute = "[ParameterDecl(ValueDescriptions = [\"no\", \"yes\"])]";
        private const string BypassDecl = "public bool Bypass { get; set; }";
        private const string ExtraDecl = "[ParameterDecl(MaxValue = 127, DefValue = 64)] public int Extra { get; set; }";
        private const int ExtraDefault = 64;

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
        }

        /// <summary>
        /// Saves a song containing the unmodified Effect with Bypass set, and returns the file's bytes.
        /// The driver's test directory is removed on dispose, so the song is carried over in memory.
        /// </summary>
        private static byte[] SaveSongWithBypassedEffect()
        {
            using var driver = new Driver();
            var controller = EffectController.NewInstance();
            driver.Gear.AddDynamicEffect(EffectController.Info);
            driver.Start();

            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(controller);
            driver.MachineGraph.ExecuteMachineCommand(new SetGlobalParameterCommand(controller, "Bypass", 1));

            var songPath = driver.RandomSongPath();
            driver.DawCommands.SaveCurrentSongForTheFirstTime(DialogChoices.Select(songPath));
            driver.AssertNoErrorsReportedToUser();

            return File.ReadAllBytes(songPath.ToString());
        }

        private static int GlobalParameterValue(Driver driver, string parameterName)
        {
            var read = new ReadGlobalParameterCommand(EffectController.NewInstance().InstanceName, parameterName);
            driver.MachineGraph.ExecuteMachineCommand(read);
            return read.Value;
        }

        private class ReadGlobalParameterCommand(string instanceName, string parameterName) : ITestMachineInstanceCommand
        {
            public int Value { get; private set; }

            public void Execute(ReBuzzCore buzzCore, ReBuzzMachines machineCores)
            {
                var machine = machineCores.GetSongCoreMachineInstance(instanceName);
                var globals = machine.ParameterGroups.Single(g => g.Type == ParameterGroupType.Global);
                Value = globals.Parameters.Single(p => p.Name == parameterName).GetValue(0);
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
