using AtmaFileSystem;
using BuzzGUI.Interfaces;
using ReBuzz.Core;
using ReBuzz.MachineManagement;

namespace ReBuzzTests.Automation
{
    public interface IAdditionalInitialStateAssertions
    {
        void AssertSongCore(SongCore songCore, AbsoluteDirectoryPath gearDir, ReBuzzCore reBuzzCore);

        void AssertPatternEditor(ReBuzzCore reBuzzCore, AbsoluteDirectoryPath gearDir, IMachine machine);

        void AssertMachineManager(ReBuzzCore reBuzzCore, AbsoluteDirectoryPath gearDir, MachineManager machineManager);
    }
}