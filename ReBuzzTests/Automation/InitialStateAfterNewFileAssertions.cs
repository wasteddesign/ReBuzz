using AtmaFileSystem;
using BuzzGUI.Interfaces;
using FluentAssertions;
using ReBuzz.Core;
using ReBuzz.MachineManagement;
using System.Linq;

namespace ReBuzzTests.Automation
{
    public class InitialStateAfterNewFileAssertions : IAdditionalInitialStateAssertions
    {
        public void AssertInitialStateOfSongCore(
            SongCore songCore, AbsoluteDirectoryPath gearDir, ReBuzzCore reBuzzCore)
        {
            songCore.MachinesList.Should().Equal(songCore.Machines.Cast<MachineCore>());
        }

        public void AssertInitialStateOfPatternEditor(
            ReBuzzCore reBuzzCore, AbsoluteDirectoryPath gearDir, IMachine machine)
        {
            machine.PatternEditorDLL.Should().BeNull();
        }

        public void AssertInitialStateOfMachineManager(
            ReBuzzCore reBuzzCore, AbsoluteDirectoryPath gearDir, MachineManager machineManager)
        {
            machineManager.ManagedMachines.Should().BeEmpty();
        }
    }
}