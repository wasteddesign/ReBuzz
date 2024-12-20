using AtmaFileSystem;
using BuzzGUI.Interfaces;
using ReBuzz.Core;
using ReBuzz.MachineManagement;

namespace ReBuzzTests.Automation
{
    public interface IAdditionalInitialStateAssertions
    {
        void AssertInitialStateOfSongCore(SongCore songCore, AbsoluteDirectoryPath gearDir, ReBuzzCore reBuzzCore);

        void AssertInitialStateOfPatternEditor(ReBuzzCore reBuzzCore, AbsoluteDirectoryPath gearDir, IMachine machine);

        void AssertInitialStateOfMachineManager(
            ReBuzzCore reBuzzCore, AbsoluteDirectoryPath gearDir, MachineManager machineManager);
    }
}