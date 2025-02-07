using AtmaFileSystem.IO;
using Buzz.MachineInterface;
using FluentAssertions;
using ReBuzzTests.Automation;
using ReBuzzTests.Automation.TestMachines;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

public class AddMachineTests //bug
{
    [Test]
    public void ShouldOutputSilenceWhenNoNotesOnTheSong()
    {
        using var driver = new Driver();
        driver.AddDynamicGenerator(DynamicGeneratorDefinition.Synth);
        driver.Start();

        driver.InsertGeneratorInstance(DynamicGeneratorDefinition.Synth);

        var samples = driver.ReadStereoSamples(100000);

        samples.AssertContainStereoSilence(100000);
    }

    [Test]
    public void ShouldXXXX()
    {
        var synth1Sample = new Sample(5,10);
        var synth2Sample = new Sample(2,5);
        using var driver = new Driver();
        driver.AddDynamicGenerator(DynamicGeneratorDefinition.Synth);
        driver.AddDynamicGenerator(DynamicGeneratorDefinition.Synth);
        driver.Start();

        driver.InsertGeneratorInstance(DynamicGeneratorDefinition.Synth, "s1");
        driver.InsertGeneratorInstance(DynamicGeneratorDefinition.Synth, "s2");
        driver.SetupConstantReturnedStereoSampleValue(DynamicGeneratorDefinition.Synth, synth1Sample, "s1");
        driver.SetupConstantReturnedStereoSampleValue(DynamicGeneratorDefinition.Synth, synth2Sample, "s2");

        var samples = driver.ReadStereoSamples(1);

        samples.AssertSamples([ExpectedSampleValue.From(synth1Sample + synth2Sample)]);
    }
}