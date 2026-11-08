using AwesomeAssertions;
using BuzzGUI.Interfaces;
using ReBuzz.Core;
using ReBuzzTests.Automation;
using ReBuzzTests.Automation.TestMachinesControllers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace ReBuzzTests
{
    /// <summary>
    /// Showing a pattern in the pattern editor makes the editor rebuild its view. While a song loads,
    /// patterns aren't shown one by one as they are created; the last created pattern is shown once
    /// when loading ends, as before.
    /// </summary>
    public class LoadingSongPatternEditorTests
    {
        private static readonly EffectController First = EffectController.NewInstance("e1");
        private static readonly EffectController Second = EffectController.NewInstance("e2");

        private static readonly (string Name, int Length)[] FirstPatterns = [("A", 16), ("B", 32)];
        private static readonly (string Name, int Length)[] SecondPatterns = [("C", 24), ("D", 8)];

        // Loading a .bmxml creates WPF objects, which need an STA thread
        [Apartment(ApartmentState.STA)]
        [TestCase(".bmx")]
        [TestCase(".bmxml")]
        public void ShowsOnlyTheLastCreatedPatternWhenASongLoads(string extension)
        {
            var song = SaveSongWithPatterns(extension);
            using var driver = StartDriver();
            var shownPatterns = new List<(string Machine, string Pattern)>();
            RunOnBuzzCore(driver, buzzCore => buzzCore.SetPatternEditorPatternChanged += p =>
            {
                if (p != null)
                {
                    shownPatterns.Add((p.Machine.Name, p.Name));
                }
            });

            LoadSong(driver, song, extension);

            driver.AssertNoErrorsReportedToUser();
            Patterns(driver, First).Should().Equal(FirstPatterns);
            Patterns(driver, Second).Should().Equal(SecondPatterns);
            shownPatterns.Should().Equal((Second.InstanceName, "D"));
            ShownPattern(driver).Should().Be((Second.InstanceName, "D"));
        }

        /// <summary>
        /// Saves a song with two effects, each with patterns, and returns the file's bytes.
        /// </summary>
        private static byte[] SaveSongWithPatterns(string extension)
        {
            using var driver = StartDriver();

            driver.MachineGraph.InsertMachineInstanceFor(First);
            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(Second);
            driver.MachineGraph.Connect(First, Second);
            CreatePatterns(driver, First, FirstPatterns);
            CreatePatterns(driver, Second, SecondPatterns);

            var songPath = driver.RandomSongPath(extension);
            driver.DawCommands.SaveCurrentSongForTheFirstTime(DialogChoices.Select(songPath));
            driver.AssertNoErrorsReportedToUser();

            return File.ReadAllBytes(songPath.ToString());
        }

        private static Driver StartDriver()
        {
            var driver = new Driver();
            driver.Gear.AddDynamicEffect(EffectController.Info);
            driver.Start();
            return driver;
        }

        private static void LoadSong(Driver driver, byte[] song, string extension)
        {
            var songPath = driver.RandomSongPath(extension);
            File.WriteAllBytes(songPath.ToString(), song);
            driver.DawCommands.LoadSong(DialogChoices.Select(songPath));
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

        private static (string Machine, string Pattern)? ShownPattern(Driver driver)
        {
            (string Machine, string Pattern)? shown = null;
            RunOnBuzzCore(driver, buzzCore =>
            {
                IPattern p = buzzCore.PatternEditorPattern;
                shown = p != null ? (p.Machine.Name, p.Name) : null;
            });
            return shown;
        }

        private static void RunOnBuzzCore(Driver driver, Action<ReBuzzCore> action)
        {
            driver.MachineGraph.ExecuteMachineCommand(new BuzzCoreCommand(action));
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

        private class BuzzCoreCommand(Action<ReBuzzCore> action) : ITestMachineInstanceCommand
        {
            public void Execute(ReBuzzCore buzzCore, ReBuzzMachines machineCores)
            {
                action(buzzCore);
            }
        }
    }
}
