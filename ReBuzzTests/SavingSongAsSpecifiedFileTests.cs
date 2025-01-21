using AtmaFileSystem.IO;
using FluentAssertions;
using ReBuzzTests.Automation;

namespace ReBuzzTests;

internal class SavingSongAsSpecifiedFileTests
{
    [Test]
    public void MaintainsCleanStateAfterCancelingSavingEmptySongAsUsingSaveAsCommand()
    {
        using var driver = new Driver();
        var emptySongPath = driver.RandomSongPath();

        driver.Start();

        driver.SaveCurrentSongAs(DialogChoices.Cancel());

        driver.AssertNoErrorsReportedToUser();
        driver.AssertInitialStateAfterAppStart();
        driver.AssertRecentFileListHasNoEntryFor(emptySongPath);
        emptySongPath.Exists().Should().BeFalse();
    }

    [Test]
    public void AllowsSavingEmptySongUsingSaveAsCommand()
    {
        using var driver = new Driver();
        var emptySongPath = driver.RandomSongPath();
        driver.Start();

        driver.SaveCurrentSongAs(DialogChoices.Select(emptySongPath));
        driver.LoadSong(DialogChoices.Select(emptySongPath));

        driver.AssertNoErrorsReportedToUser();
        driver.AssertStateAfterLoadingAnEmptySong(emptySongPath);
        driver.AssertRecentFileListHasEntry(0, emptySongPath);
        emptySongPath.Exists().Should().BeTrue();
    }

    [Test]
    public void AllowsOverwritingEmptySongFileUsingSaveAsCommand()
    {
        using var driver = new Driver();
        var emptySongPath = driver.RandomSongPath();
        driver.Start();

        driver.SaveCurrentSongForTheFirstTime(DialogChoices.Select(emptySongPath));
        driver.SaveCurrentSongAs(DialogChoices.Select(emptySongPath));
        driver.LoadSong(DialogChoices.Select(emptySongPath));

        driver.AssertNoErrorsReportedToUser();
        driver.AssertStateAfterLoadingAnEmptySong(emptySongPath);
        driver.AssertRecentFileListHasEntry(0, emptySongPath);
    }

    [Test]
    public void AllowsSavingEmptySongAsMultipleFiles()
    {
        using var driver = new Driver();
        var emptySongPath1 = driver.RandomSongPath();
        var emptySongPath2 = driver.RandomSongPath();
        var emptySongPath3 = driver.RandomSongPath();
        driver.Start();

        driver.SaveCurrentSongAs(DialogChoices.Select(emptySongPath1));
        driver.SaveCurrentSongAs(DialogChoices.Select(emptySongPath2));
        driver.SaveCurrentSongAs(DialogChoices.Select(emptySongPath3));

        driver.AssertNoErrorsReportedToUser();
        driver.AssertInitialStateAfterSavingEmptySong(emptySongPath3);
        driver.AssertRecentFileListHasEntry(0, emptySongPath3);
        driver.AssertRecentFileListHasEntry(1, emptySongPath2);
        driver.AssertRecentFileListHasEntry(2, emptySongPath1);
        emptySongPath1.Exists().Should().BeTrue();
        emptySongPath2.Exists().Should().BeTrue();
        emptySongPath3.Exists().Should().BeTrue();
        emptySongPath3.ReadAllBytes().Should().Equal(emptySongPath1.ReadAllBytes());
        emptySongPath2.ReadAllBytes().Should().Equal(emptySongPath1.ReadAllBytes());
    }
}