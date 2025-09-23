using AwesomeAssertions;
using ReBuzz.Core;
using ReBuzz.ManagedMachine;
using ReBuzzTests.Automation.TestMachines;

namespace ReBuzzTests.Automation.Assertions
{
    public class ModernPatternEditorManagedDllAssertions : IModernPatternEditorManagedDllAssertions
    {
        void IModernPatternEditorManagedDllAssertions.Assert(MachineDLL modernPatternEditor)
        {
            modernPatternEditor.ManagedDLL.machineInfo.Should().Be(FakeModernPatternEditor.GetMachineDecl());
            modernPatternEditor.ManagedDLL.MachineInfo.Should().Be(modernPatternEditor.MachineInfo);
            modernPatternEditor.ManagedDLL.WorkFunctionType.Should().Be(ManagedMachineDLL.WorkFunctionTypes.Effect);
            modernPatternEditor.ManagedDLL.Assembly.Should().NotBeNull();
            modernPatternEditor.ManagedDLL.constructor.Should().NotBeNull();

            modernPatternEditor.ManagedDLL.globalParameters.Should().HaveCount(2);
            InitialStateAssertions.AssertGlobalParameters(modernPatternEditor.ManagedDLL.globalParameters[0],
                modernPatternEditor.ManagedDLL.globalParameters[1]);

            modernPatternEditor.ManagedDLL.trackParameters.Should().HaveCount(1);
            InitialStateAssertions.AssertParameter(modernPatternEditor.ManagedDLL.trackParameters[0], ExpectedMachineParameter.ATrackParam());

            modernPatternEditor.ManagedDLL.machineType.Name.Should().Be(nameof(FakeModernPatternEditor));
        }
    }
}