using AtmaFileSystem;
using BuzzGUI.Interfaces;
using ReBuzz.Core;
using ReBuzz.MachineManagement;

namespace ReBuzzTests.Automation.Assertions
{
    /// <summary>
    /// Strangely, the state after starting ReBuzz and after issuing a new file command is slightly different.
    /// This interface allows customizing the generic "initial state" assertion
    /// for each of these cases.
    /// </summary>
    public interface IAdditionalInitialStateAssertions
    {
        void AssertStateOfSongCore(SongCore songCore, AbsoluteDirectoryPath gearDir, ReBuzzCore reBuzzCore);

        void AssertInitialStateOfPatternEditor(ReBuzzCore reBuzzCore, AbsoluteDirectoryPath gearDir, IMachine machine);

        void AssertInitialStateOfMachineManager(
            ReBuzzCore reBuzzCore, AbsoluteDirectoryPath gearDir, MachineManager machineManager);
    }
}