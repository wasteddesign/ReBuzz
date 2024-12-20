using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AtmaFileSystem;
using AtmaFileSystem.IO;
using BuzzGUI.Common;
using BuzzGUI.Common.Settings;
using BuzzGUI.Interfaces;
using FluentAssertions;
using libsndfile;
using Microsoft.Win32;
using ReBuzz;
using ReBuzz.AppViews;
using ReBuzz.Core;
using ReBuzz.FileOps;
using ReBuzz.MachineManagement;

namespace ReBuzzTests.Automation
{
    public class Driver : IDisposable, IInitializationObserver
    {
        private static readonly AbsoluteDirectoryPath TestDataRootPath =
            AbsoluteDirectoryPath.Value(Path.GetTempPath()).AddDirectoryName("ReBuzzTestData");

        private readonly AbsoluteDirectoryPath gearDir;
        private readonly AbsoluteDirectoryPath themesDir;
        private ReBuzzCore reBuzzCore;
        private readonly AbsoluteDirectoryPath gearEffectsDir;
        private readonly AbsoluteDirectoryPath gearGeneratorsDir;

        private readonly AbsoluteDirectoryPath reBuzzRootDir =
            TestDataRootPath.AddDirectoryName($"{Guid.NewGuid()}__{DateTime.UtcNow.Ticks}");

        static Driver()
        {
            AssertionOptions.FormattingOptions.MaxLines = 10000;

            // Cleaning up on start because the machine dlls created in previous test run
            // were probably held locked by the previous test process which prevented deleting them.
            AttemptToCleanupTestRootDirs();
        }

        public Driver()
        {
            gearDir = reBuzzRootDir.AddDirectoryName("Gear");
            gearEffectsDir = gearDir.AddDirectoryName("Effects");
            gearGeneratorsDir = gearDir.AddDirectoryName("Generators");
            themesDir = reBuzzRootDir.AddDirectoryName("Themes");
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
            var fakeMachineDllScanner = new FakeMachineDLLScanner(gearDir);
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

        private void SetupDirectoryStructure()
        {
            reBuzzRootDir.Create();
            gearDir.Create();
            gearEffectsDir.Create();
            gearGeneratorsDir.Create();
            themesDir.Create();
            foreach (AbsoluteFilePath gearFile in AbsoluteDirectoryPath.OfThisFile().AddDirectoryName("Gear")
                         .EnumerateFiles())
            {
                gearFile.Copy(gearDir + gearFile.FileName(), true);
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
            InitialStateAssertions.AssertInitialState(gearDir, reBuzzCore, new InitialStateAfterNewFileAssertions());
        }

        public void AssertInitialStateAfterAppStart()
        {
            InitialStateAssertions.AssertInitialState(gearDir, reBuzzCore, new InitialStateAfterAppStartAssertions());
        }

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