namespace ReBuzzTests.Automation
{
    public class DynamicGeneratorDefinition(string dllName, string sourceCode)
    {
        public string DllName { get; } = dllName;
        public string SourceCode { get; } = sourceCode;
    }
}