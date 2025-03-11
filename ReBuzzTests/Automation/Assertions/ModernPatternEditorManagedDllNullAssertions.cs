using FluentAssertions;
using ReBuzz.Core;

namespace ReBuzzTests.Automation.Assertions
{
    internal class ModernPatternEditorManagedDllNullAssertions : IModernPatternEditorManagedDllAssertions
    {
        public void Assert(MachineDLL modernPatternEditor)
        {
            modernPatternEditor.ManagedDLL.Should().BeNull();
        }
    }
}