using AtmaFileSystem.IO;
using AwesomeAssertions;
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

        driver.DawCommands.SaveCurrentSongAs(DialogChoices.Cancel());

        driver.AssertNoErrorsReportedToUser();
        driver.AssertInitialStateAfterAppStart();
        driver.RecentFiles.AssertHasNoEntryFor(emptySongPath);
        emptySongPath.Exists().Should().BeFalse();
    }

    [Test]
    public void AllowsSavingEmptySongUsingSaveAsCommand()
    {
        using var driver = new Driver();
        var emptySongPath = driver.RandomSongPath();
        driver.Start();

        driver.DawCommands.SaveCurrentSongAs(DialogChoices.Select(emptySongPath));
        driver.DawCommands.LoadSong(DialogChoices.Select(emptySongPath));

        driver.AssertNoErrorsReportedToUser();
        driver.AssertStateAfterLoadingAnEmptySong(emptySongPath);
        driver.RecentFiles.AssertHasEntry(0, emptySongPath);
        emptySongPath.Exists().Should().BeTrue();
    }

    [Test]
    public void AllowsOverwritingEmptySongFileUsingSaveAsCommand()
    {
        using var driver = new Driver();
        var emptySongPath = driver.RandomSongPath();
        driver.Start();

        driver.DawCommands.SaveCurrentSongForTheFirstTime(DialogChoices.Select(emptySongPath));
        driver.DawCommands.SaveCurrentSongAs(DialogChoices.Select(emptySongPath));
        driver.DawCommands.LoadSong(DialogChoices.Select(emptySongPath));

        driver.AssertNoErrorsReportedToUser();
        driver.AssertStateAfterLoadingAnEmptySong(emptySongPath);
        driver.RecentFiles.AssertHasEntry(0, emptySongPath);
    }

    [Test]
    public void AllowsSavingEmptySongAsMultipleFiles()
    {
        using var driver = new Driver();
        var emptySongPath1 = driver.RandomSongPath();
        var emptySongPath2 = driver.RandomSongPath();
        var emptySongPath3 = driver.RandomSongPath();
        driver.Start();

        driver.DawCommands.SaveCurrentSongAs(DialogChoices.Select(emptySongPath1));
        driver.DawCommands.SaveCurrentSongAs(DialogChoices.Select(emptySongPath2));
        driver.DawCommands.SaveCurrentSongAs(DialogChoices.Select(emptySongPath3));

        driver.AssertNoErrorsReportedToUser();
        driver.AssertInitialStateAfterSavingEmptySong(emptySongPath3);
        driver.RecentFiles.AssertHasEntry(0, emptySongPath3);
        driver.RecentFiles.AssertHasEntry(1, emptySongPath2);
        driver.RecentFiles.AssertHasEntry(2, emptySongPath1);
        emptySongPath1.Exists().Should().BeTrue();
        emptySongPath2.Exists().Should().BeTrue();
        emptySongPath3.Exists().Should().BeTrue();
        emptySongPath3.ReadAllBytes().Should().Equal(emptySongPath1.ReadAllBytes());
        emptySongPath2.ReadAllBytes().Should().Equal(emptySongPath1.ReadAllBytes());
    }
}
