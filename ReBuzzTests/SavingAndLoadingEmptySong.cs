using AtmaFileSystem;
using ReBuzzTests.Automation;
using System.Diagnostics.CodeAnalysis;

namespace ReBuzzTests
{
    internal class SavingAndLoadingEmptySong
    {
        [Test]
        public void DoesNotChangeStateWhenUserCancelsLoadingProject()
        {
            using var driver = new Driver();
            driver.Start();

            driver.SetupLoadedFileChoiceToUserCancel();

            driver.LoadSong();

            driver.AssertInitialStateAfterAppStart();
        }

        [Test]
        public void DoesNotChangeStateWhenUserPicksNonExistentFile()
        {
            var nonExistentFileName = @"sdfsjhdkfjhsdf";
            using var driver = new Driver();
            driver.Start();

            driver.SetupLoadedFileChoiceTo(nonExistentFileName);

            driver.LoadSong();

            driver.AssertInitialStateAfterNewFile();
            driver.AssertMessageReportedToUser(
                $"Error loading {nonExistentFileName}",
                $"Could not find file '{AbsoluteDirectoryPath.OfCurrentWorkingDirectory().AddFileName(nonExistentFileName)}'.");
        }

        //bug make more tests for exception scenarios
        [Test]
        public void MaintainsCleanStateAfterSavingEmptySong()
        {
            var emptySongPath = AbsoluteDirectoryPath.OfThisFile().AddFileName("EmptySongBmx.bmx"); //bug use a unique location
            using var driver = new Driver();
            driver.Start();

            driver.SetupSavedFileChoiceTo(emptySongPath.ToString());

            driver.SaveCurrentSong();

            driver.AssertNoErrorsReportedToUser();
            driver.AssertInitialStateAfterSavingEmptySong(emptySongPath);
            driver.AssertRecentFileListHasEntry(0, emptySongPath);
        }

        [Test]
        public void AllowsSavingASongSecondTimeWithoutProvidingNewName()
        {
            using var driver = new Driver();
            var emptySongPath = driver.RandomSongPath();
            driver.Start();

            driver.SetupSavedFileChoiceTo(emptySongPath.ToString());

            driver.SaveCurrentSong();
            driver.SetupSavedFileChoiceTo("????");
            driver.SaveCurrentSong();

            driver.AssertNoErrorsReportedToUser();
            driver.AssertInitialStateAfterSavingEmptySong(emptySongPath);
            driver.AssertRecentFileListHasEntry(0, emptySongPath);
        }

        [Test]
        public void MaintainsCleanStateAfterSavingAndLoadingEmptySong()
        {
            using var driver = new Driver();
            var emptySongPath = driver.RandomSongPath();
            driver.Start();

            driver.SetupSavedFileChoiceTo(emptySongPath);
            driver.SetupLoadedFileChoiceTo(emptySongPath);

            driver.SaveCurrentSong();
            driver.LoadSong();

            driver.AssertNoErrorsReportedToUser();
            driver.AssertStateAfterLoadingAnEmptySong(emptySongPath);
            driver.AssertRecentFileListHasEntry(0, emptySongPath);
        }

        [Test]
        public void MaintainsCleanStateAfterSavingAndLoadingASongTwice()
        {
            using var driver = new Driver();
            var emptySongPath = driver.RandomSongPath();
            driver.Start();

            driver.SetupSavedFileChoiceTo(emptySongPath);
            driver.SetupLoadedFileChoiceTo(emptySongPath);

            driver.SaveCurrentSong();
            driver.LoadSong();
            driver.LoadSong();

            driver.AssertNoErrorsReportedToUser();
            driver.AssertStateAfterLoadingAnEmptySong(emptySongPath);
            driver.AssertRecentFileListHasEntry(0, emptySongPath);
        }
    }
}