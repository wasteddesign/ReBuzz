using AtmaFileSystem;
using BuzzGUI.Interfaces;
using ReBuzz.Core;

namespace ReBuzzTests.Automation.Assertions
{
    public interface ISongStateAssertions
    {
        void AssertStateOfSongAndSongCore(
            SongCore songCore,
            ISong song,
            ReBuzzCore reBuzzCore,
            AbsoluteDirectoryPath gearDir,
            IAdditionalInitialStateAssertions additionalAssertions);
    }
}