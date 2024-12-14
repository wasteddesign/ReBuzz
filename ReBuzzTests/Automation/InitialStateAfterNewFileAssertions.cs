using AtmaFileSystem;
using BuzzGUI.Interfaces;
using FluentAssertions;
using ReBuzz.Core;
using ReBuzz.MachineManagement;
using System.Linq;

namespace ReBuzzTests.Automation;

public class InitialStateAfterNewFileAssertions : IAdditionalInitialStateAssertions
{
    public void AssertSongCore(SongCore songCore, AbsoluteDirectoryPath gearDir, ReBuzzCore reBuzzCore)
    {
        songCore.MachinesList.Should().Equal(songCore.Machines.Cast<MachineCore>());
    }

    public void AssertPatternEditor(ReBuzzCore reBuzzCore, AbsoluteDirectoryPath gearDir, IMachine machine)
    {
        machine.PatternEditorDLL.Should().BeNull(); //bug investigate!!
    }

    public void AssertMachineManager(
        ReBuzzCore reBuzzCore, AbsoluteDirectoryPath gearDir, MachineManager machineManager)
    {
        machineManager.ManagedMachines.Should().BeEmpty();
    }
}