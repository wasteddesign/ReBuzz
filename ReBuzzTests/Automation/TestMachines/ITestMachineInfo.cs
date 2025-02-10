using AtmaFileSystem;
using ReBuzz.Core;

namespace ReBuzzTests.Automation.TestMachines
{
    public interface ITestMachineInfo
    {
        internal MachineDLL GetMachineDll(ReBuzzCore buzz, AbsoluteFilePath location);
        internal string DllName { get; }
        internal string SourceCode { get; }
    }
}