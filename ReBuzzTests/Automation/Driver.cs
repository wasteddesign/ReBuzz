using AtmaFileSystem;
using AtmaFileSystem.IO;
using BuzzGUI.Common;
using BuzzGUI.Common.Settings;
using BuzzGUI.Interfaces;
using Core.NullableReferenceTypesExtensions;
using FluentAssertions;
using FluentAssertions.Execution;
using ReBuzz.AppViews;
using ReBuzz.Audio;
using ReBuzz.Core;
using ReBuzz.MachineManagement;
using ReBuzzTests.Automation.Assertions;
using ReBuzzTests.Automation.TestMachinesControllers;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
        /// Taken from the production code
        /// </summary>
        private const int DefaultAmp = 0x4000;

        /// <summary>
        /// Taken from the production code
        /// </summary>
        private const int DefaultPan = 0x4000;

        /// <summary>
        /// This is a temp dir where each test has its own ReBuzz root directory
        /// </summary>
        private static readonly AbsoluteDirectoryPath TestDataRootPath =
            AbsoluteDirectoryPath.Value(Path.GetTempPath()).AddDirectoryName("ReBuzzTestData");

        /// <summary>  
        /// An in-memory sink used to capture and verify log messages during tests.
        /// This allows tests to assert that specific log messages were emitted
        /// without writing to actual log files.
        /// </summary>
        private static readonly InMemorySink inMemorySink;

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
        /// ReBuzz bin32 directory for the current test
        /// </summary>
        private AbsoluteDirectoryPath Bin32Dir => reBuzzRootDir.AddDirectoryName("bin32");

        /// <summary>
        /// ReBuzz bin64 directory for the current test
        /// </summary>
        private AbsoluteDirectoryPath Bin64Dir => reBuzzRootDir.AddDirectoryName("bin64");

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

        /// <summary>
        /// ReBuzz songs directory for the current test
        /// </summary>
        private AbsoluteDirectoryPath SongsDir => reBuzzRootDir.AddDirectoryName("Songs");

        private ReBuzzCore reBuzzCore;
        private readonly FakeFileNameChoice fileNameToLoadChoice = new();
        private readonly FakeUserMessages fakeUserMessages;
        private FakeFileNameChoice fileNameToSaveChoice = new();
        private readonly FakeInMemoryRegistry fakeRegistry = new();
        private readonly FakeDispatcher dispatcher = new();
        private readonly FakeMachineDLLScanner fakeMachineDllScanner;

        /// <summary>
        /// Actions to add machines to the fake machine DLL scanner.
        /// It is done this way because the machine DLL scanner requires the ReBuzzCore instance
        /// to add machines and ReBuzzCore is available only after the driver is started.
        /// So instead of adding machines directly, we add actions that will add them
        /// when the driver is started.
        /// </summary>
        private List<Action<FakeMachineDLLScanner, ReBuzzCore>> addMachineActions = [];
        
        private ReBuzzCoreInitialization initialization;

        /// <summary>
        /// Represents the absolute file path to the effect crash file.
        /// </summary>
        /// <remarks>The crash file is meant as a communication mechanism for machines to crash.
        /// The reason it's done with a file is that we want to be able to crash during machine constructor
        /// and there is no other way to pass that information.</remarks>
        private readonly AbsoluteFilePath crashEffectFilePath;

        /// <summary>
        /// Represents the absolute file path to the generator crash file.
        /// </summary>
        /// <remarks><see cref="crashEffectFilePath"/></remarks>
        private readonly AbsoluteFilePath crashGeneratorFilePath;

        private ReBuzzMachines reBuzzMachines;

        public ReBuzzCommandsDriverExtension DawCommands => new(reBuzzCore, fileNameToSaveChoice, fileNameToLoadChoice);
        public GearDriverExtension Gear => new(GearGeneratorsDir, GearEffectsDir, addMachineActions);
        public RecentFilesDriverExtension RecentFiles => new(fakeRegistry);
        public MachineGraphDriverExtension MachineGraph => new(reBuzzCore, reBuzzMachines, fakeMachineDllScanner, dispatcher, DefaultPan, DefaultAmp);
        public ReBuzzLogDriverExtension ReBuzzLog => new(inMemorySink);

        public Driver()
        {
            ResetGlobalState();
            fakeUserMessages = new FakeUserMessages();
            fakeMachineDllScanner = new FakeMachineDLLScanner(GearDir);
            crashEffectFilePath = GearEffectsDir.AddFileName("crash_fake_machine");
            crashGeneratorFilePath = GearGeneratorsDir.AddFileName("crash_fake_machine");
        }

        static Driver()
        {
            AssertionEngine.Configuration.Formatting.MaxLines = 10000;

            // Cleaning up on start because the machine dlls created in previous test run
            // were probably held locked by the previous test process which prevented deleting them.
            AttemptToCleanupTestRootDirs();
            inMemorySink = new InMemorySink();
        }

        public void Start()
        {
            SetupDirectoryStructure();

            EngineSettings engineSettings = Global.EngineSettings;
            var buzzPath = reBuzzRootDir.ToString();
            GeneralSettings generalSettings = Global.GeneralSettings;
            var registryRoot = Global.RegistryRoot; //Should not matter as the registry is in memory

            reBuzzCore = new ReBuzzCore(
                generalSettings,
                engineSettings,
                buzzPath,
                registryRoot,
                fakeMachineDllScanner,
                dispatcher,
                fakeRegistry,
                fileNameToLoadChoice,
                fileNameToSaveChoice,
                fakeUserMessages, 
                new FakeKeyboard());
            fakeMachineDllScanner.AddFakeModernPatternEditor(reBuzzCore);
            addMachineActions.ForEach(addMachine => addMachine(fakeMachineDllScanner, reBuzzCore));
            initialization = new ReBuzzCoreInitialization(reBuzzCore, buzzPath, dispatcher, fakeRegistry, new FakeKeyboard());
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
            reBuzzCore.SaveSong += s => { TestContext.Out.WriteLine($"SaveSong: {s}"); };
            initialization.StartReBuzzEngineStep6();

            reBuzzCore.FileEvent += (type, s, arg3) =>
            {
                TestContext.Out.WriteLine($"FileEvent: {type}, {s}, {arg3}");
            };

            reBuzzCore.BuzzCommandRaised += command =>
            {
                TestContext.Out.WriteLine("Command: " + command);
                if (command == BuzzCommand.Exit)
                {
                    reBuzzCore.Playing = false;
                    reBuzzCore.Release();
                }
            };
            reBuzzMachines = new ReBuzzMachines(reBuzzCore);
        }

        public AbsoluteFilePath RandomSongPath() => 
            SongsDir.AddFileName($"{Guid.NewGuid():N}.bmx");

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
            Bin32Dir.Create();
            Bin64Dir.Create();
            GearDir.Create();
            GearEffectsDir.Create();
            GearGeneratorsDir.Create();
            ThemesDir.Create();
            SongsDir.Create();
            CopyGearFilesFromSourceCodeToReBuzzTestDir();
            CopyReBuzzEngineToReBuzzTestDir();
        }

        private void CopyGearFilesFromSourceCodeToReBuzzTestDir()
        {
            foreach (var gearFile in AbsoluteDirectoryPath.OfThisFile().AddDirectoryName("Gear")
                         .EnumerateFiles())
            {
                gearFile.Copy(GearDir + gearFile.FileName(), true);
            }
        }

        private void CopyReBuzzEngineToReBuzzTestDir()
        {
            // ReBuzzEngine is a separate project that is built to a specific directory based on platform and configuration.
            // That's why the build of the test project creates a special file that includes
            // this build-time data. The tests can then read it and use the path to copy
            // the ReBuzzEngine binaries to the test directory.

            var reBuzzBinariesDirString =
                File.ReadLines(AbsoluteDirectoryPath.OfExecutingAssembly().AddFileName("ReBuzzLocation.txt").ToString())
                    .First().OrThrow();
            var reBuzzBinariesDir = AbsoluteDirectoryPath.Value(reBuzzBinariesDirString);

            foreach (var filePath in reBuzzBinariesDir.AddDirectoryName("bin32").EnumerateFiles())
            {
                filePath.Copy(Bin32Dir + filePath.FileName(), true);
            }

            foreach (var filePath in reBuzzBinariesDir.AddDirectoryName("bin64").EnumerateFiles())
            {
                filePath.Copy(Bin64Dir + filePath.FileName(), true);
            }
        }

        public void Dispose()
        {
            reBuzzCore?.ExecuteCommand(BuzzCommand.Exit);
            initialization.ShutDownReBuzzEngine();
            AttemptToCleanupTestRootDirs();
        }

        public void AssertInitialStateAfterNewFile()
        {
            InitialStateAssertions.AssertInitialState(GearDir,
                reBuzzCore,
                new InitialStateAfterNewFileAssertions(),
                new InitialSongStateAssertions());
        }

        public void AssertInitialStateAfterAppStart()
        {
            InitialStateAssertions.AssertInitialState(GearDir,
                reBuzzCore,
                new InitialStateAfterAppStartAssertions(),
                new InitialSongStateAssertions());
        }

        public void AssertStateAfterLoadingAnEmptySong(AbsoluteFilePath emptySongPath)
        {
            InitialStateAssertions.AssertInitialState(
                GearDir,
                reBuzzCore,
                new InitialStateAfterLoadingEmptySongAssertions(),
                new EmptySongStateWhenSongHasANameAssertions(emptySongPath));
        }

        public void AssertInitialStateAfterSavingEmptySong(AbsoluteFilePath emptySongPath)
        {
            InitialStateAssertions.AssertInitialState(
                GearDir,
                reBuzzCore,
                new InitialStateAfterAppStartAssertions(),
                new EmptySongStateWhenSongHasANameAssertions(emptySongPath));

        }

        void IInitializationObserver.NotifyMachineManagerCreated(MachineManager machineManager)
        {
            TestContext.Out.WriteLine("MachineManager created");
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

        public void AssertErrorReportedToUser(string expectedCaption, string expectedMessage)
        {
            using (new AssertionScope())
            {
                fakeUserMessages.Caption.Should().Be(expectedCaption);
                fakeUserMessages.Message.Should().Be(expectedMessage);
            }
        }

        public void AssertNoErrorsReportedToUser()
        {
            using (new AssertionScope())
            {
                fakeUserMessages.Caption.Should().BeEmpty();
                fakeUserMessages.Message.Should().BeEmpty();
                fakeUserMessages.StackStrace.Should().BeEmpty();
            }
        }

        public TestReadBuffer ReadStereoSamples(int count)
        {
            var workManager = new WorkManager(reBuzzCore, new WorkThreadEngine(1), 0, new EngineSettings
            {
                AccurateBPM = true,
                EqualPowerPanning = true,
                LowLatencyGC = true,
                MachineDelayCompensation = false,
                Multithreading = false,
                ProcessMutedMachines = false,
                SubTickTiming = true,
            });
            var readSamplesCount = count * 2;
            var buffer = new float[readSamplesCount];
            var result = workManager.ThreadRead(buffer, 0, readSamplesCount);
            return new TestReadBuffer(result, buffer);
        }

        /// <summary>
        /// Asserts that the specified machine instance is in a crashed state.
        /// </summary>
        /// <param name="controller">
        /// The <see cref="DynamicMachineController"/> representing the machine instance to be checked.
        /// </param>
        /// <exception cref="AssertionException">
        /// Thrown if the machine instance is not in a crashed state.
        /// </exception>
        public void AssertMachineIsCrashed(DynamicMachineController controller)
        {
            reBuzzMachines.GetSongCoreMachineInstance(controller.InstanceName).DLL.IsCrashed.Should().BeTrue();
            reBuzzMachines.GetMachineManagerMachine(controller.InstanceName).MachineDLL
                .IsCrashed.Should().BeTrue();
        }

        /// <summary>
        /// Enables crashing behavior for the specified effect, simulating a scenario where the effect causes a crash.
        /// </summary>
        /// <param name="crashingEffect">
        /// The instance of <see cref="DynamicMachineController"/> representing the effect to be configured for crashing.
        /// </param>
        public void EnableEffectCrashingFor(DynamicMachineController crashingEffect)
        {
            MachineSpecificCrashFileName(crashEffectFilePath, crashingEffect).Create().Dispose();
        }

        /// <summary>
        /// Enables the crashing behavior for the specified generator, simulating a scenario where the generator causes a crash.
        /// </summary>
        /// <param name="crashingGenerator">
        /// The instance of <see cref="DynamicMachineController"/> representing the generator 
        /// for which crashing behavior should be enabled.
        /// </param>
        public void EnableGeneratorCrashingFor(DynamicMachineController crashingGenerator)
        {
            MachineSpecificCrashFileName(crashGeneratorFilePath, crashingGenerator).Create().Dispose();
        }

        private static AbsoluteFilePath MachineSpecificCrashFileName(
            AbsoluteFilePath crashingMachineDllPath, DynamicMachineController crashingGenerator)
        {
            return crashingMachineDllPath.ChangeFileNameTo(crashingMachineDllPath.FileName()
                .AppendBeforeExtension("_" + crashingGenerator.InstanceName));
        }

        /// <summary>
        /// Resets the global state before each test
        /// </summary>
        private static void ResetGlobalState()
        {
            ReBuzzCore.GlobalState = new BuzzGlobalState();
            ReBuzzCore.subTickInfo = new SubTickInfoExtended();
            ReBuzzCore.masterInfo = new MasterInfoExtended();
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Sink(inMemorySink)
                .MinimumLevel.Debug()
                .CreateLogger();
        }

        public void SetMasterVolumeTo(double newVolume)
        {
            reBuzzCore.MasterVolume = newVolume;
        }
    }
}