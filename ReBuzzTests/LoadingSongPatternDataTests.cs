using AwesomeAssertions;
using ReBuzz.Core;
using ReBuzzTests.Automation;
using ReBuzzTests.Automation.TestMachinesControllers;
using System;
using System.IO;
using System.Linq;

namespace ReBuzzTests
{
    /// <summary>
    /// The PATT section of a .bmx song stores the patterns of all machines one after another. Pattern
    /// data is skipped for machines whose patterns belong to a pattern editor machine, as in songs
    /// saved by ReBuzz. The skip has to end exactly where the next machine's patterns start.
    /// </summary>
    public class LoadingSongPatternDataTests
    {
        private static readonly EffectController First = EffectController.NewInstance("e1");
        private static readonly EffectController Second = EffectController.NewInstance("e2");

        private static readonly (string Name, int Length)[] FirstPatterns = [("A", 16), ("B", 32)];
        private static readonly (string Name, int Length)[] SecondPatterns = [("C", 24)];

        [Test]
        public void LoadsPatternsOfMachinesThatUseAPatternEditorMachine()
        {
            var song = SaveSongWithPatterns();

            using var driver = LoadSong(song);

            driver.AssertNoErrorsReportedToUser();
            Patterns(driver, First).Should().Equal(FirstPatterns);
            Patterns(driver, Second).Should().Equal(SecondPatterns);
        }

        /// <summary>
        /// Saves a song with two effects, each with patterns, and returns the file's bytes. The first
        /// effect feeds the second, so the second effect's patterns also have an input column.
        /// </summary>
        private static byte[] SaveSongWithPatterns()
        {
            using var driver = new Driver();
            driver.Gear.AddDynamicEffect(EffectController.Info);
            driver.Start();

            driver.MachineGraph.InsertMachineInstanceFor(First);
            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(Second);
            driver.MachineGraph.Connect(First, Second);
            CreatePatterns(driver, First, FirstPatterns);
            CreatePatterns(driver, Second, SecondPatterns);

            var songPath = driver.RandomSongPath();
            driver.DawCommands.SaveCurrentSongForTheFirstTime(DialogChoices.Select(songPath));
            driver.AssertNoErrorsReportedToUser();

            return File.ReadAllBytes(songPath.ToString());
        }

        private static Driver LoadSong(byte[] song)
        {
            var driver = new Driver();
            driver.Gear.AddDynamicEffect(EffectController.Info);
            driver.Start();

            var songPath = driver.RandomSongPath();
            File.WriteAllBytes(songPath.ToString(), song);
            driver.DawCommands.LoadSong(DialogChoices.Select(songPath));
            return driver;
        }

        private static void CreatePatterns(Driver driver, EffectController controller, (string Name, int Length)[] patterns)
        {
            driver.MachineGraph.ExecuteMachineCommand(new MachineCoreCommand(controller.InstanceName, machine =>
            {
                foreach (var (name, length) in patterns)
                {
                    machine.CreatePattern(name, length);
                }
            }));
        }

        private static (string Name, int Length)[] Patterns(Driver driver, EffectController controller)
        {
            (string Name, int Length)[] patterns = [];
            driver.MachineGraph.ExecuteMachineCommand(new MachineCoreCommand(controller.InstanceName,
                machine => patterns = machine.Patterns.Select(p => (p.Name, p.Length)).ToArray()));
            return patterns;
        }

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
    }
}
