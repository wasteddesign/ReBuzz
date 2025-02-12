using AtmaFileSystem;
using BuzzGUI.Interfaces;
using FluentAssertions.Execution;
using ReBuzz.Core;
using ReBuzz.FileOps;
using ReBuzzTests.Automation.TestMachines;
using System.Collections.Generic;

namespace ReBuzzTests.Automation
{
    /// <summary>
    /// The real DLLScanner class starts the ReBuzzEngine processes.
    /// For now, this is too much trouble to set up, so we just use a fake class.
    ///
    /// For the future, we may consider to have fake execs of the ReBuzzEngine or even somehow use the real thing,
    /// but for now, this will do.
    ///
    /// This test-only implementation compiles C# files into assemblies and places them in the supplied path
    /// </summary>
    internal class FakeMachineDLLScanner(AbsoluteDirectoryPath gearPath) : IMachineDLLScanner
    {
        private readonly Dictionary<string, MachineDLL> machineDllsByName = new();

        /// <summary>
        /// Used to compile and save C# code to test-only DLLs and to update the internal machine dictionary.
        /// </summary>
        public void AddFakeModernPatternEditor(ReBuzzCore buzz)
        {
            var assemblyLocation = gearPath.AddFileName(FakeModernPatternEditorInfo.Instance.DllName);
            DynamicCompiler.CompileAndSave(FakeModernPatternEditor.GetSourceCode(), assemblyLocation);

            var modernPatternEditorDll = FakeModernPatternEditorInfo.Instance.GetMachineDll(buzz, assemblyLocation);
            machineDllsByName[modernPatternEditorDll.Name] = modernPatternEditorDll;
        }

        public void AddDynamicMachine(
            ReBuzzCore buzz,
            ITestMachineInfo machineInfo,
            AbsoluteDirectoryPath targetDir)
        {
            var assemblyLocation = targetDir.AddFileName(machineInfo.DllName);
            var machineDll = machineInfo.GetMachineDll(buzz, assemblyLocation);
            DynamicCompiler.CompileAndSave(machineInfo.SourceCode, assemblyLocation);

            machineDllsByName[machineDll.Name] = machineDll;
        }

        public Dictionary<string, MachineDLL> GetMachineDLLs(ReBuzzCore buzz, string buzzPath)
        {
            return machineDllsByName;
        }

        public IMachineDLL GetMachineDLL(string name)
        {
            return machineDllsByName[name];
        }

        public void AddMachineDllsToDictionary(ReBuzzCore buzz, XMLMachineDLL[] xMLMachineDLLs, Dictionary<string, MachineDLL> md)
        {
            Execute.Assertion.FailWith("Not called anywhere yet in the current tests");
        }

        public XMLMachineDLL ValidateDll(ReBuzzCore buzz, string libName, string path, string buzzPath)
        {
            Execute.Assertion.FailWith("Not called anywhere yet in the current tests");
            return null!;
        }
    }
}