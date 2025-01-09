using AtmaFileSystem;
using AtmaFileSystem.IO;
using FluentAssertions;
using ReBuzzTests.Automation;

namespace ReBuzzTests
{
    internal class SavingAndLoadingEmptySongTests
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
            driver.AssertErrorReportedToUser(
                $"Error loading {nonExistentFileName}",
                $"Could not find file '{AbsoluteDirectoryPath.OfCurrentWorkingDirectory().AddFileName(nonExistentFileName)}'.");
        }

        [Test]
        public void MaintainsCleanStateAfterSavingEmptySong()
        {
            using var driver = new Driver();
            var emptySongPath = driver.RandomSongPath();

            driver.Start();

            driver.SetupSavedFileChoiceTo(emptySongPath);

            driver.SaveCurrentSong();

            driver.AssertNoErrorsReportedToUser();
            driver.AssertInitialStateAfterSavingEmptySong(emptySongPath);
            driver.AssertRecentFileListHasEntry(0, emptySongPath);
        }
        
        [Test]
        public void MaintainsCleanStateAfterCancelingSavingEmptySong()
        {
            using var driver = new Driver();
            var emptySongPath = driver.RandomSongPath();

            driver.Start();

            driver.SetupSavedFileChoiceToUserCancel();

            driver.SaveCurrentSong();

            driver.AssertNoErrorsReportedToUser();
            driver.AssertInitialStateAfterAppStart();
            driver.AssertRecentFileListHasNoEntryFor(emptySongPath);
            emptySongPath.Exists().Should().BeFalse();
        }

        [Test]
        public void AllowsSavingASongSecondTimeWithoutProvidingNewName()
        {
            using var driver = new Driver();
            var emptySongPath = driver.RandomSongPath();
            driver.Start();

            driver.SetupSavedFileChoiceTo(emptySongPath);

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