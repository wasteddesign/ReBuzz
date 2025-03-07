namespace ReBuzzTests.Automation.TestMachines
{
    /// <summary>
    /// Interface for a dynamic test machine info.
    /// Introduced to keep some of the test code work for both dynamic generators and effects.
    /// </summary>
    public interface IDynamicTestMachineInfo : ITestMachineInfo
    {
        internal string SourceCode { get; }
    }
}