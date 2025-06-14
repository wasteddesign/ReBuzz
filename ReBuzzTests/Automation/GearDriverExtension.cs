using AtmaFileSystem;
using ReBuzz.Core;
using ReBuzzTests.Automation.TestMachines;
using System;
using System.Collections.Generic;

namespace ReBuzzTests.Automation
{
    public class GearDriverExtension
    {
        private AbsoluteDirectoryPath gearGeneratorsDir;
        private readonly AbsoluteDirectoryPath gearEffectsDir;
        private List<Action<FakeMachineDLLScanner, ReBuzzCore>> addMachineActions;

        internal GearDriverExtension(
            AbsoluteDirectoryPath gearGeneratorsDir, AbsoluteDirectoryPath gearEffectsDir,
            List<Action<FakeMachineDLLScanner, ReBuzzCore>> addMachineActions)
        {
            this.gearGeneratorsDir = gearGeneratorsDir;
            this.gearEffectsDir = gearEffectsDir;
            this.addMachineActions = addMachineActions;
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
    }
}