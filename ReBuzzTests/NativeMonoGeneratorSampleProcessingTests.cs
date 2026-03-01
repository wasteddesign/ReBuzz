using Buzz.MachineInterface;
using ReBuzzTests.Automation;
using ReBuzzTests.Automation.TestMachinesControllers;

namespace ReBuzzTests;

public class NativeMonoGeneratorSampleProcessingTests
{
    [Test]
    public void OutputsSamplesFromASingleNativeMonoMachine()
    {
        using var driver = new Driver();
        var monoGenerator = FakeNativeMonoGeneratorController.NewInstance();
        driver.Gear.AddPrecompiledGenerator(FakeNativeMonoGeneratorController.Info);
        driver.Start();

        driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(monoGenerator);
        driver.MachineGraph.ExecuteMachineCommand(monoGenerator.SetMonoSampleValueTo(5));

        var samples = driver.ReadStereoSamples(1);

        samples.AssertAreEqualTo([ExpectedSampleValue.From(new Sample(5, 5))]);
    }

    [Test]
    public void OutputsMultipleSamplesFromASingleNativeMonoMachine()
    {
        using var driver = new Driver();
        var monoGenerator = FakeNativeMonoGeneratorController.NewInstance();
        driver.Gear.AddPrecompiledGenerator(FakeNativeMonoGeneratorController.Info);
        driver.Start();

        driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(monoGenerator);
        driver.MachineGraph.ExecuteMachineCommand(monoGenerator.SetMonoSampleValueTo(5));

        var samples = driver.ReadStereoSamples(2);

        samples.AssertAreEqualTo([
            ExpectedSampleValue.From(new Sample(5, 5)),
            ExpectedSampleValue.From(new Sample(5, 5))
        ]);
    }

    [Test]
    public void OutputsFloatingPointSamplesFromASingleNativeMonoMachine()
    {
        using var driver = new Driver();
        var monoGenerator = FakeNativeMonoGeneratorController.NewInstance();
        driver.Gear.AddPrecompiledGenerator(FakeNativeMonoGeneratorController.Info);
        driver.Start();

        driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(monoGenerator);
        driver.MachineGraph.ExecuteMachineCommand(monoGenerator.SetMonoSampleValueTo(2, 10));

        var samples = driver.ReadStereoSamples(1);

        samples.AssertAreEqualTo([ExpectedSampleValue.From(new Sample(0.2f, 0.2f))]);
    }

    [Test]
    public void OutputsASampleWhichIsASumFromAllMachines()
    {
        var gen1Value = 5f;
        var gen2Value = 2f;
        using var driver = new Driver();
        var monoGenerator1Controller = FakeNativeMonoGeneratorController.NewInstance("s1");
        var monoGenerator2Controller = FakeNativeMonoGeneratorController.NewInstance("s2");
        driver.Gear.AddPrecompiledGenerator(FakeNativeMonoGeneratorController.Info);

        driver.Start();

        driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(monoGenerator1Controller);
        driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(monoGenerator2Controller);
        driver.MachineGraph.ExecuteMachineCommand(monoGenerator1Controller.SetMonoSampleValueTo(gen1Value));
        driver.MachineGraph.ExecuteMachineCommand(monoGenerator2Controller.SetMonoSampleValueTo(gen2Value));

        var samples = driver.ReadStereoSamples(1);

        var expectedSum = gen1Value + gen2Value;
        samples.AssertAreEqualTo([ExpectedSampleValue.From(new Sample(expectedSum, expectedSum))]);
    }

    [Test]
    public void OutputsASampleWhichIsASumFromAllMachinesTimesMasterVolume()
    {
        const double masterVolume1 = 0.5;
        const double masterVolume2 = 0.25;
        var gen1Value = 5f;
        var gen2Value = 2f;
        using var driver = new Driver();
        var monoGenerator1Controller = FakeNativeMonoGeneratorController.NewInstance("s1");
        var monoGenerator2Controller = FakeNativeMonoGeneratorController.NewInstance("s2");
        driver.Gear.AddPrecompiledGenerator(FakeNativeMonoGeneratorController.Info);

        driver.Start();

        driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(monoGenerator1Controller);
        driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(monoGenerator2Controller);
        driver.MachineGraph.ExecuteMachineCommand(monoGenerator1Controller.SetMonoSampleValueTo(gen1Value));
        driver.MachineGraph.ExecuteMachineCommand(monoGenerator2Controller.SetMonoSampleValueTo(gen2Value));

        var expectedSum = gen1Value + gen2Value;

        driver.SetMasterVolumeTo(masterVolume1);
        var samples1 = driver.ReadStereoSamples(1);
        samples1.AssertAreEqualTo([ExpectedSampleValue.From(new Sample(expectedSum, expectedSum), masterVolume1)]);

        driver.SetMasterVolumeTo(masterVolume2);
        var samples2 = driver.ReadStereoSamples(1);
        samples2.AssertAreEqualTo([ExpectedSampleValue.From(new Sample(expectedSum, expectedSum), masterVolume2)]);
    }
}
