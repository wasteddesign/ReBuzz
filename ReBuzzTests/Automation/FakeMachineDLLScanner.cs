using AtmaFileSystem;
using FluentAssertions.Execution;
using ReBuzz.Core;
using ReBuzz.FileOps;
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
            AbsoluteFilePath? assemblyLocation = gearPath.AddFileName(FakeModernPatternEditorInfo.DllName);
            DynamicCompiler.CompileAndSave(FakeModernPatternEditor.GetSourceCode(), assemblyLocation);

            MachineDLL? modernPatternEditorDll = FakeModernPatternEditorInfo.GetMachineDll(buzz, assemblyLocation);
            machineDllsByName[modernPatternEditorDll.Name] = modernPatternEditorDll;
        }

        public Dictionary<string, MachineDLL> GetMachineDLLs(ReBuzzCore buzz, string buzzPath)
        {
            return machineDllsByName;
        }

        public void AddMachineDllsToDictionary(XMLMachineDLL[] xMLMachineDLLs, Dictionary<string, MachineDLL> md)
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