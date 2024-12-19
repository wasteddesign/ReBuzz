using AtmaFileSystem;
using ReBuzz.Core;
using ReBuzz.FileOps;
using System.Collections.Generic;

namespace ReBuzzTests.Automation
{
    internal class FakeMachineDLLScanner(AbsoluteDirectoryPath gearPath) : IMachineDLLScanner
    {
        private readonly Dictionary<string, MachineDLL> machineDllsByName = new();

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
            Assert.Fail("Not called anywhere yet in the current tests");
        }

        public XMLMachineDLL ValidateDll(ReBuzzCore buzz, string libName, string path, string buzzPath)
        {
            Assert.Fail("Not called anywhere yet in the current tests");
            return null!;
        }
    }
}