using System;
using System.IO;
using AtmaFileSystem;
using AtmaFileSystem.IO;
using BuzzGUI.Common;
using BuzzGUI.Common.Settings;
using BuzzGUI.Interfaces;
using FluentAssertions;
using ReBuzz.AppViews;
using ReBuzz.Core;
using ReBuzz.MachineManagement;
using ReBuzzTests.Automation.Assertions;

namespace ReBuzzTests.Automation
{
    /// <summary>
    /// The driver class for the ReBuzz automation tests.
    /// It is an "intention layer" between the production code and the tests.
    /// Is it an implementation of the Driver pattern.
    /// See https://github.com/grzesiek-galezowski/driver-pattern-demo/tree/main/DriverInFunctionalHttpApiTests
    /// for a more generic description.
    /// See https://docs.specflow.org/projects/specflow/en/latest/Guides/DriverPattern.html for description specific
    /// to the community around the SpecFlow tool where this pattern is more popular.
    /// </summary>
    public class Driver : IDisposable, IInitializationObserver
    {
        /// <summary>
        /// This is a temp dir where each test has its own ReBuzz root directory
        /// </summary>
        private static readonly AbsoluteDirectoryPath TestDataRootPath =
            AbsoluteDirectoryPath.Value(Path.GetTempPath()).AddDirectoryName("ReBuzzTestData");

        /// <summary>
        /// ReBuzz root directory for the current test. Each test gets its own root directory.
        /// This follows the Persistent Fresh Fixture pattern
        /// (see http://xunitpatterns.com/Fresh%20Fixture.html#Persistent%20Fresh%20Fixture)
        /// and allows making the tests more independent of each other.
        /// </summary>
        private readonly AbsoluteDirectoryPath reBuzzRootDir =
            TestDataRootPath.AddDirectoryName($"{Guid.NewGuid()}__{DateTime.UtcNow.Ticks}");

        /// <summary>
        /// ReBuzz gear directory for the current test
        /// </summary>
        private AbsoluteDirectoryPath GearDir => reBuzzRootDir.AddDirectoryName("Gear");

        /// <summary>
        /// ReBuzz themes directory for the current test
        /// </summary>
        private AbsoluteDirectoryPath ThemesDir => reBuzzRootDir.AddDirectoryName("Themes");

        /// <summary>
        /// ReBuzz gear/effects directory for the current test
        /// </summary>
        private AbsoluteDirectoryPath GearEffectsDir => GearDir.AddDirectoryName("Effects");

        /// <summary>
        /// ReBuzz gear/generators directory for the current test
        /// </summary>
        private AbsoluteDirectoryPath GearGeneratorsDir => GearDir.AddDirectoryName("Generators");

        private ReBuzzCore reBuzzCore;

        static Driver()
        {
            AssertionOptions.FormattingOptions.MaxLines = 10000;

            // Cleaning up on start because the machine dlls created in previous test run
            // were probably held locked by the previous test process which prevented deleting them.
            AttemptToCleanupTestRootDirs();
        }

        public void Start()
        {
            SetupDirectoryStructure();

            EngineSettings engineSettings = Global.EngineSettings;
            var buzzPath = reBuzzRootDir.ToString();
            GeneralSettings generalSettings = Global.GeneralSettings;
            var registryRoot = Global.RegistryRoot; //Should not matter as the registry is in memory

            var dispatcher = new FakeDispatcher();
            var registryExInstance = new FakeInMemoryRegistry();
            var fakeMachineDllScanner = new FakeMachineDLLScanner(GearDir);
            reBuzzCore = new ReBuzzCore(generalSettings, engineSettings, buzzPath, registryRoot, fakeMachineDllScanner,
                dispatcher, registryExInstance);
            fakeMachineDllScanner.AddFakeModernPatternEditor(reBuzzCore);

            var initialization = new ReBuzzCoreInitialization(reBuzzCore, buzzPath, dispatcher, registryExInstance);
            initialization.StartReBuzzEngineStep1((sender, args) =>
            {
                TestContext.Out.WriteLine($"PropertyChanged: {args.PropertyName}");
            });
            initialization.StartReBuzzEngineStep2(nint.MaxValue);
            initialization.StartReBuzzEngineStep3(engineSettings, this);
            initialization.StartReBuzzEngineStep4(
                new FakeMachineDb(),
                s => { TestContext.Out.WriteLine($"DatabaseEvent: {s}"); },
                () => { TestContext.Out.WriteLine("OnPatternEditorActivated"); },
                () => { TestContext.Out.WriteLine("OnSequenceEditorActivated"); },
                s => { TestContext.Out.WriteLine($"OnShowSettings: {s}"); },
                control => { TestContext.Out.WriteLine($"SetPatternEditorControl: {control}"); },
                b => { TestContext.Out.WriteLine($"OnFullScreenChanged: {b}"); },
                s => { TestContext.Out.WriteLine($"OnThemeChanged: {s}"); }
            );

            initialization.StartReBuzzEngineStep5(s => { TestContext.Out.WriteLine("OpenFile: " + s); });
            initialization.StartReBuzzEngineStep6();

            reBuzzCore.BuzzCommandRaised += command =>
            {
                TestContext.Out.WriteLine("Command: " + command);
                if (command == BuzzCommand.Exit)
                {
                    reBuzzCore.Playing = false;
                    reBuzzCore.Release();
                }
            };
        }

        public void NewFile()
        {
            reBuzzCore.ExecuteCommand(BuzzCommand.NewFile);
        }

        /// <summary>
        /// Sets up the directory structure for the current test
        /// </summary>
        private void SetupDirectoryStructure()
        {
            reBuzzRootDir.Create();
            GearDir.Create();
            GearEffectsDir.Create();
            GearGeneratorsDir.Create();
            ThemesDir.Create();
            CopyGearFilesFromSourceCodeToReBuzzTestDir();
        }

        private void CopyGearFilesFromSourceCodeToReBuzzTestDir()
        {
            foreach (AbsoluteFilePath gearFile in AbsoluteDirectoryPath.OfThisFile().AddDirectoryName("Gear")
                         .EnumerateFiles())
            {
                gearFile.Copy(GearDir + gearFile.FileName(), true);
            }
        }

        public void Dispose()
        {
            reBuzzCore?.ExecuteCommand(BuzzCommand.Exit);
            AttemptToCleanupTestRootDirs();
        }

        void IInitializationObserver.NotifyMachineManagerCreated(MachineManager machineManager)
        {
            TestContext.Out.WriteLine("MachineManager created");
        }

        public void AssertInitialStateAfterNewFile()
        {
            InitialStateAssertions.AssertInitialState(GearDir, reBuzzCore, new InitialStateAfterNewFileAssertions());
        }

        public void AssertInitialStateAfterAppStart()
        {
            InitialStateAssertions.AssertInitialState(GearDir, reBuzzCore, new InitialStateAfterAppStartAssertions());
        }

        /// <summary>
        /// Attempts to clean up all the test root directories.
        /// This typically will not be able to delete the machine dlls
        /// created in this test run as they are loaded inside the currently running process
        /// and there is no way to unload them.
        /// </summary>
        private static void AttemptToCleanupTestRootDirs()
        {
            if (TestDataRootPath.Exists())
            {
                foreach (AbsoluteDirectoryPath singleTestRoot in TestDataRootPath.EnumerateDirectories())
                {
                    try
                    {
                        singleTestRoot.Delete(true);
                        TestContext.Out.WriteLine("Deleted " + singleTestRoot);
                    }
                    catch
                    {
                        TestContext.Out.WriteLine("Could not delete " + singleTestRoot);
                    }
                }
            }
        }
    }
}