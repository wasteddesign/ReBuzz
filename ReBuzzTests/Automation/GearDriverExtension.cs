using AtmaFileSystem;
using AtmaFileSystem.IO;
using ReBuzz.Core;
using ReBuzzTests.Automation.TestMachines;
using ReBuzzTests.Automation.TestMachinesControllers;
using System;
using System.Collections.Generic;

namespace ReBuzzTests.Automation
{
    public class GearDriverExtension
    {
        private AbsoluteDirectoryPath gearGeneratorsDir;
        private readonly AbsoluteDirectoryPath gearEffectsDir;
        private List<Action<FakeMachineDLLScanner, ReBuzzCore>> addMachineActions;
        private readonly AbsoluteFilePath crashGeneratorFilePath;
        private readonly AbsoluteFilePath crashEffectFilePath;

        internal GearDriverExtension(
            AbsoluteDirectoryPath gearGeneratorsDir,
            AbsoluteDirectoryPath gearEffectsDir,
            List<Action<FakeMachineDLLScanner, ReBuzzCore>> addMachineActions,
            AbsoluteFilePath crashGeneratorFilePath,
            AbsoluteFilePath crashEffectFilePath)
        {
            this.gearGeneratorsDir = gearGeneratorsDir;
            this.gearEffectsDir = gearEffectsDir;
            this.addMachineActions = addMachineActions;
            this.crashGeneratorFilePath = crashGeneratorFilePath;
            this.crashEffectFilePath = crashEffectFilePath;
        }

        public void AddPrecompiledGenerator(ITestMachineInfo info)
        {
            AbsoluteDirectoryPath absoluteDirectoryPath = gearGeneratorsDir;
            List<Action<FakeMachineDLLScanner, ReBuzzCore>> actions = addMachineActions;
            actions.Add((scanner, reBuzz) => scanner.AddPrecompiledMachine(reBuzz, info, absoluteDirectoryPath));
        }

        private void AddDynamicMachine(IDynamicTestMachineInfo info, AbsoluteDirectoryPath targetPath)
        {
            addMachineActions.Add((scanner, reBuzz) => scanner.AddDynamicMachine(reBuzz, info, targetPath));
        }

        public void AddPrecompiledEffect(ITestMachineInfo info)
        {
            addMachineActions.Add((scanner, reBuzz) => scanner.AddPrecompiledMachine(reBuzz, info, gearEffectsDir));
        }

        public void AddDynamicEffect(IDynamicTestMachineInfo info)
        {
            AddDynamicMachine(info, gearEffectsDir);
        }

        public void AddDynamicGenerator(IDynamicTestMachineInfo info)
        {
            AddDynamicMachine(info, gearGeneratorsDir);
        }

        /// <summary>
        /// Enables crashing behavior for the specified effect, simulating a scenario where the effect causes a crash.
        /// </summary>
        /// <param name="crashingEffect">
        ///     The instance of <see cref="DynamicMachineController"/> representing the effect to be configured for crashing.
        /// </param>
        /// <param name="methodToCrashOn"></param>
        public void EnableEffectCrashingFor(DynamicMachineController crashingEffect, string methodToCrashOn)
        {
            MachineSpecificCrashFileName(crashEffectFilePath, crashingEffect).WriteAllText(methodToCrashOn);
        }

        public void SaveGeneratorConfigurationFileFor(DynamicMachineController generator, TestMachineConfig config) //bug move the def here
        {

        }

        /// <summary>
        /// Enables the crashing behavior for the specified generator, simulating a scenario where the generator causes a crash.
        /// </summary>
        /// <param name="crashingGenerator">
        /// The instance of <see cref="DynamicMachineController"/> representing the generator 
        /// for which crashing behavior should be enabled.
        /// </param>
        /// <param name="methodToCrashOn"></param>
        public void EnableGeneratorCrashingFor(DynamicMachineController crashingGenerator, string methodToCrashOn)
        {
            MachineSpecificCrashFileName(crashGeneratorFilePath, crashingGenerator).WriteAllText(methodToCrashOn);
        }

        private static AbsoluteFilePath MachineSpecificCrashFileName(
            AbsoluteFilePath crashingMachineDllPath, DynamicMachineController crashingGenerator)
        {
            return crashingMachineDllPath.ChangeFileNameTo(crashingMachineDllPath.FileName()
                .AppendBeforeExtension("_" + crashingGenerator.InstanceName));
        }
    }
}