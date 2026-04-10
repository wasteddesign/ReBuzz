using Buzz.MachineInterface;
using BuzzGUI.Common.Settings;
using ReBuzzTests.Automation;
using ReBuzzTests.Automation.TestMachinesControllers;

namespace ReBuzzTests;

public class SubTickResolutionAudioTests
{
    [TestCase(SubTickResolution.Normal)]
    [TestCase(SubTickResolution.Lower)]
    [TestCase(SubTickResolution.Low)]
    public void ChangesAtSubTickBoundaryForEachSubTickResolution(SubTickResolution resolution)
    {
        // GIVEN
        var boundarySampleIndex = SubtickResolutionCalculations.GetBoundarySampleIndex(resolution);
        var sampleValue = new Sample(2, 3);
        var expectedSampleValue = ExpectedSampleValue.From(sampleValue);
        var sampleAfterBoundaryValue = ExpectedSampleValue.From(new Sample(1, 1));
        var firstReadExpectedSamples = expectedSampleValue.RepeatTimes(boundarySampleIndex);
        var secondReadExpectedSamples = sampleAfterBoundaryValue.RepeatTimes(1);

        using var driver = new Driver();
        var generator = FakeNativeGeneratorStereoController.NewInstance();
        driver.Gear.AddPrecompiledGenerator(FakeNativeGeneratorStereoController.Info);
        driver.SetSubTickResolutionTo(resolution);
        driver.Start();
        
        driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(generator);
        driver.MachineGraph.ExecuteMachineCommand(generator.SetStereoSampleValueTo(sampleValue));

        var samplesBeforeBoundary = driver.ReadStereoSamples(boundarySampleIndex);
        var samplesAfterBoundary = driver.ReadStereoSamples(1);

        samplesBeforeBoundary.AssertAreEqualTo(firstReadExpectedSamples);
        samplesAfterBoundary.AssertAreEqualTo(secondReadExpectedSamples);
    }
}
