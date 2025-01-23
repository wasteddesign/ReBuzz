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

            driver.LoadSong(DialogChoices.Cancel());

            driver.AssertInitialStateAfterAppStart();
        }

        [Test]
        public void DoesNotChangeStateWhenUserPicksNonExistentFile()
        {
            var nonExistentFileName = @"sdfsjhdkfjhsdf";
            var potentialSavedFileLocation =
                AbsoluteDirectoryPath.OfCurrentWorkingDirectory().AddFileName(nonExistentFileName);
            using var driver = new Driver();
            driver.Start();

            driver.LoadSong(DialogChoices.Select(nonExistentFileName));

            driver.AssertInitialStateAfterNewFile();
            driver.AssertErrorReportedToUser(
                $"Error loading {nonExistentFileName}",
                $"Could not find file '{potentialSavedFileLocation}'.");
            potentialSavedFileLocation.Exists().Should().BeFalse();
        }

        [Test]
        public void MaintainsCleanStateAfterSavingEmptySong()
        {
            using var driver = new Driver();
            var emptySongPath = driver.RandomSongPath();

            driver.Start();

            driver.SaveCurrentSongForTheFirstTime(DialogChoices.Select(emptySongPath));

            driver.AssertNoErrorsReportedToUser();
            driver.AssertInitialStateAfterSavingEmptySong(emptySongPath);
            driver.AssertRecentFileListHasEntry(0, emptySongPath);
            emptySongPath.Exists().Should().BeTrue();
        }

        [Test]
        public void MaintainsCleanStateAfterCancelingSavingEmptySong()
        {
            using var driver = new Driver();
            var emptySongPath = driver.RandomSongPath();

            driver.Start();

            driver.SaveCurrentSongForTheFirstTime(DialogChoices.Cancel());

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

            driver.SaveCurrentSongForTheFirstTime(DialogChoices.Select(emptySongPath));
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

            driver.SaveCurrentSongForTheFirstTime(DialogChoices.Select(emptySongPath));
            driver.LoadSong(DialogChoices.Select(emptySongPath));

            driver.AssertNoErrorsReportedToUser();
            driver.AssertStateAfterLoadingAnEmptySong(emptySongPath);
            driver.AssertRecentFileListHasEntry(0, emptySongPath);
        }

        [Test]
        public void MaintainsCleanStateAfterSavingAnEmptySongAndLoadingItTwice()
        {
            using var driver = new Driver();
            var emptySongPath = driver.RandomSongPath();
            driver.Start();

            driver.SaveCurrentSongForTheFirstTime(DialogChoices.Select(emptySongPath));
            driver.LoadSong(DialogChoices.Select(emptySongPath));
            driver.LoadSong(DialogChoices.Select(emptySongPath));

            driver.AssertNoErrorsReportedToUser();
            driver.AssertStateAfterLoadingAnEmptySong(emptySongPath);
            driver.AssertRecentFileListHasEntry(0, emptySongPath);
        }
    }
}