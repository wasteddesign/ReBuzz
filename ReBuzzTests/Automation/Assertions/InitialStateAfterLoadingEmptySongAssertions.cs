using AtmaFileSystem;
using BuzzGUI.Interfaces;
using ReBuzz.Core;
using ReBuzz.MachineManagement;

namespace ReBuzzTests.Automation.Assertions
{
    /// <summary>
    /// Assertions for the initial state of ReBuzz specific to the state after loading an empty song.
    /// </summary>
    public class InitialStateAfterLoadingEmptySongAssertions : IAdditionalInitialStateAssertions
    {
        public void AssertStateOfSongCore(
            SongCore songCore, AbsoluteDirectoryPath gearDir, ReBuzzCore reBuzzCore)
        {
            InitialStateAfterAppStartAssertions.StaticAssertStateOfSongCore(songCore, gearDir, reBuzzCore, this);
        }

        public void AssertInitialStateOfPatternEditor(
            ReBuzzCore reBuzzCore, AbsoluteDirectoryPath gearDir, IMachine machine)
        {
            InitialStateAfterNewFileAssertions.StaticAssertInitialStateOfPatternEditor(machine);
        }

        public void AssertInitialStateOfMachineManager(
            ReBuzzCore reBuzzCore, AbsoluteDirectoryPath gearDir, MachineManager machineManager)
        {
            InitialStateAfterAppStartAssertions.StaticAssertInitialStateOfMachineManager(
                reBuzzCore,
                gearDir,
                machineManager,
                this);
        }
    }
}