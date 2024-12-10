using System;
using System.Collections.Generic;
using AtmaFileSystem;
using ReBuzz.Core;
using ReBuzz.FileOps;

namespace ReBuzzTests;

internal class FakeMachineDLLScanner(AbsoluteDirectoryPath gearPath) : IMachineDLLScanner //bug move
{
    //bug breaks CQS
    public Dictionary<string, MachineDLL> GetMachineDLLs(ReBuzzCore buzz, string buzzPath)
    {
        var assemblyLocation = gearPath.AddFileName("StubMachine.dll"); //bug
                                                                        //bug delete the test machines after the test
                                                                        //bug set the buzz path correctly
        DynamicCompiler.CompileAndSave(FakeModernPatternEditor.GetSourceCode(), assemblyLocation);

        var modernPatternEditorDll = FakeModernPatternEditorInfo.GetMachineDll(buzz, assemblyLocation);
        return new Dictionary<string, MachineDLL>() //bug fill some of this stuff from machine decl
        {
            [modernPatternEditorDll.Name] = modernPatternEditorDll,
        };
    }

    public void AddMachineDllsToDictionary(XMLMachineDLL[] xMLMachineDLLs, Dictionary<string, MachineDLL> md)
    {
        throw new NotImplementedException("Not called anywhere in the current tests");
    }

    public XMLMachineDLL ValidateDll(ReBuzzCore buzz, string libName, string path, string buzzPath)
    {
        throw new NotImplementedException("Not called anywhere in the current tests");
    }
}