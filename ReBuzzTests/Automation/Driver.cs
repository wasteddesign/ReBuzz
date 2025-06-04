using System;
using System.IO;
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
using ReBuzz.FileOps;
using ReBuzz.MachineManagement;
using ReBuzzTests.Automation.Assertions;
using ReBuzzTests.Automation.TestMachines;
using System.Collections.Generic;
using System.Linq;
using ReBuzzTests.Automation.TestMachinesControllers;
using Serilog;
using Serilog.Events;
using Serilog.Parsing;

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
        
        private Dictionary<string, MachineCore> addedGeneratorInstances = new();
        
        private ReBuzzCoreInitialization initialization;
        private readonly AbsoluteFilePath crashEffectFilePath;
        private readonly AbsoluteFilePath crashGeneratorFilePath;

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
        }

        public AbsoluteFilePath RandomSongPath() => 
            SongsDir.AddFileName($"{Guid.NewGuid():N}.bmx");

        public void NewFile()
        {
            reBuzzCore.ExecuteCommand(BuzzCommand.NewFile);
        }

        public void LoadSong(DialogChoices.FileNameSource source)
        {
            fileNameToLoadChoice.SetTo(source);
            reBuzzCore.ExecuteCommand(BuzzCommand.OpenFile);
        }

        public void SaveCurrentSongForTheFirstTime(DialogChoices.FileNameSource source)
        {
            fileNameToSaveChoice.SetTo(source);
            reBuzzCore.ExecuteCommand(BuzzCommand.SaveFile);
        }

        public void SaveCurrentSong()
        {
            fileNameToSaveChoice.SetTo(DialogChoices.ThrowIfDialogInvoked());
            reBuzzCore.ExecuteCommand(BuzzCommand.SaveFile);
        }

        public void SaveCurrentSongAs(DialogChoices.FileNameSource source)
        {
            fileNameToSaveChoice.SetTo(source);
            reBuzzCore.ExecuteCommand(BuzzCommand.SaveFileAs);
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

        void IInitializationObserver.NotifyMachineManagerCreated(MachineManager machineManager)
        {
            TestContext.Out.WriteLine("MachineManager created");
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

        public void AssertRecentFileListHasEntry(int index, AbsoluteFilePath songPath)
        {
            RecentFiles().ElementAt(index).Should().Be(songPath.ToString());
        }

        public void AssertRecentFileListHasNoEntryFor(AbsoluteFilePath songPath)
        {
            RecentFiles().Should().NotContain(songPath.ToString());
        }

        private IEnumerable<string> RecentFiles()
        {
            return fakeRegistry.ReadNumberedList<string>("File", "Recent File List");
        }

        public void InsertMachineInstanceConnectedToMasterFor(DynamicMachineController controller)
        {
            var addedInstance = InsertMachineInstanceFor(controller);
            ConnectToMaster(addedInstance);
        }

        public void DisconnectFromMaster(DynamicMachineController controller)
        {
            DisconnectFromMaster(SongCoreMachine(controller.InstanceName));
        }

        public MachineCore InsertMachineInstanceFor(DynamicMachineController controller)
        {
            var machineDll = fakeMachineDllScanner.GetMachineDLL(controller.Name);
            File.WriteAllText(machineDll.Path + ".txt", controller.InstanceName);
            CreateInstrument(machineDll, controller.InstanceName);
            var addedInstance = reBuzzCore.SongCore.MachinesList.Last();
            addedGeneratorInstances[controller.InstanceName] = addedInstance;
            return addedInstance;
        }

        public void Connect(
            DynamicMachineController sourceController, 
            DynamicMachineController destinationController)
        {
            ConnectMachineInstances(
                SongCoreMachine(sourceController.InstanceName),
                SongCoreMachine(destinationController.InstanceName));
        }

        public void ExecuteMachineCommand(ITestMachineInstanceCommand command)
        {
            command.Execute(reBuzzCore, addedGeneratorInstances);
        }

        public void AddDynamicGeneratorToGear(IDynamicTestMachineInfo info)
        {
            AddDynamicMachineToGear(info, GearGeneratorsDir);
        }

        public void AddDynamicEffectToGear(IDynamicTestMachineInfo info)
        {
            AddDynamicMachineToGear(info, GearEffectsDir);
        }

        public void AddPrecompiledGeneratorToGear(ITestMachineInfo info)
        {
            addMachineActions.Add((scanner, reBuzz) => scanner.AddPrecompiledMachine(reBuzz, info, GearGeneratorsDir));
        }

        public void AddPrecompiledEffectToGear(ITestMachineInfo info)
        {
            addMachineActions.Add((scanner, reBuzz) => scanner.AddPrecompiledMachine(reBuzz, info, GearEffectsDir));
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

        public void AssertMachineIsCrashed(DynamicMachineController controller)
        {
            SongCoreMachine(controller.InstanceName).DLL.IsCrashed.Should().BeTrue();
            MachineManagerMachine(controller.InstanceName).MachineDLL
                .IsCrashed.Should().BeTrue();
        }

        public void EnableEffectCrashingFor(DynamicMachineController crashingEffect)
        {
            MachineSpecificCrashFileName(crashEffectFilePath, crashingEffect).Create().Dispose();
        }

        public void EnableGeneratorCrashingFor(DynamicMachineController crashingGenerator)
        {
            MachineSpecificCrashFileName(crashGeneratorFilePath, crashingGenerator).Create().Dispose();
        }

        private AbsoluteFilePath MachineSpecificCrashFileName(
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

        private void CreateInstrument(IMachineDLL machineDll, string instanceName)
        {
            reBuzzCore.SongCore.CreateMachine(machineDll.Name, null!, instanceName, null!, null!, null!, -1, 0, 0);
        }

        private void ConnectToMaster(MachineCore instance)
        {
            ConnectMachineInstances(instance, SongCoreMachine("Master"));
        }

        private void DisconnectFromMaster(MachineCore instance)
        {
            DisconnectMachineInstances(instance, SongCoreMachine("Master"));
        }

        public void SetMasterVolumeTo(double newVolume)
        {
            reBuzzCore.MasterVolume = newVolume;
        }

        private void AddDynamicMachineToGear(IDynamicTestMachineInfo info, AbsoluteDirectoryPath targetPath)
        {
            addMachineActions.Add((scanner, reBuzz) => scanner.AddDynamicMachine(reBuzz, info, targetPath));
        }

        private void ConnectMachineInstances(IMachine source, IMachine destination)
        {
            reBuzzCore.SongCore.ConnectMachines(source, destination, 0, 0, DefaultAmp, DefaultPan);
        }

        private void DisconnectMachineInstances(MachineCore source, MachineCore destination)
        {
            reBuzzCore.SongCore.DisconnectMachines(new MachineConnectionCore(source, 0, destination, 0, DefaultAmp,
                DefaultPan, dispatcher));
        }

        private MachineCore SongCoreMachine(string name)
        {
            return reBuzzCore.SongCore.MachinesList.Single(m => m.Name == name);
        }

        private MachineCore MachineManagerMachine(string instanceName)
        {
            return reBuzzCore.MachineManager.NativeMachines.Single(kvp => kvp.Key.Name == instanceName).Key;
        }

        public void AssertLogContainsInvalidPointerMessage()
        {
            inMemorySink.Entries.Should()
                .ContainEquivalentOf(
                    new LogEvent(DateTimeOffset.MaxValue, LogEventLevel.Error, null,
                        new MessageTemplate("Invalid pointer", [new TextToken("Invalid pointer")]), []),
                    options => options.Excluding(e => e.Timestamp));
        }

        public void AssertLogContainsCannotAccessDisposedObjectMessage()
        {
            inMemorySink.Entries.Should()
                .ContainEquivalentOf(
                    new LogEvent(DateTimeOffset.MaxValue, LogEventLevel.Error,
                        null, new MessageTemplate(
                            "Cannot access a disposed object.\r\nObject name: 'Microsoft.Win32.SafeHandles.SafeWaitHandle'.",
                            [
                                new TextToken(
                                    "Cannot access a disposed object.\r\nObject name: 'Microsoft.Win32.SafeHandles.SafeWaitHandle'.")
                            ]), []),
                    options => options.Excluding(e => e.Timestamp));
        }

        public void AssertLogContainsIndexOutsideArrayBoundsMessage()
        {
            inMemorySink.Entries.Should()
                .ContainEquivalentOf(
                    new LogEvent(DateTimeOffset.MaxValue, LogEventLevel.Error,
                        null, new MessageTemplate(
                            "Index was outside the bounds of the array.",
                            [
                                new TextToken("Index was outside the bounds of the array.")
                            ]), []),
                    options => options.Excluding(e => e.Timestamp));
        }
    }
}