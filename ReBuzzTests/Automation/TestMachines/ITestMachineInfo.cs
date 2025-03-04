using AtmaFileSystem;
using ReBuzz.Core;

namespace ReBuzzTests.Automation.TestMachines
{
    /// <summary>
    /// Interface for a test machine info.
    /// Introduced to keep some of the test code work for both generators and effects.
    /// </summary>
    public interface ITestMachineInfo
    {
        internal MachineDLL GetMachineDll(ReBuzzCore buzz, AbsoluteFilePath location);
        internal string DllName { get; }
    }
}