using Buzz.MachineInterface;
using ReBuzzTests.Automation;
using ReBuzzTests.Automation.TestMachinesControllers;

namespace ReBuzzTests;

public class NativeGeneratorSampleProcessingTests
{
    [Test]
    public void OutputsSamplesFromASingleNativeStereoMachine()
    {
        using var driver = new Driver();
        var nativeGenerator = FakeNativeGeneratorController.NewInstance();
        driver.AddPrecompiledGeneratorToGear(FakeNativeGeneratorController.Info);
        driver.Start();

        driver.InsertMachineInstanceConnectedToMasterFor(nativeGenerator);
        driver.ExecuteMachineCommand(nativeGenerator.SetStereoSampleValueTo(new Sample(2, 3)));

        var samples = driver.ReadStereoSamples(1);

        samples.AssertAreEqualTo([ExpectedSampleValue.From(new Sample(2,3))]);
    }

    [Test]
    public void OutputsMultipleSamplesFromASingleNativeStereoMachine()
    {
        using var driver = new Driver();
        var nativeGenerator = FakeNativeGeneratorController.NewInstance();
        driver.AddPrecompiledGeneratorToGear(FakeNativeGeneratorController.Info);
        driver.Start();

        driver.InsertMachineInstanceConnectedToMasterFor(nativeGenerator);
        driver.ExecuteMachineCommand(nativeGenerator.SetStereoSampleValueTo(new Sample(2, 3)));

        var samples = driver.ReadStereoSamples(2);

        samples.AssertAreEqualTo([
            ExpectedSampleValue.From(new Sample(2, 3)),
            ExpectedSampleValue.From(new Sample(2, 3))
        ]);
    }

    [Test]
    public void OutputsFloatingPointSamplesFromASingleNativeStereoMachine()
    {
        using var driver = new Driver();
        var nativeGenerator = FakeNativeGeneratorController.NewInstance();
        driver.AddPrecompiledGeneratorToGear(FakeNativeGeneratorController.Info);
        driver.Start();

        driver.InsertMachineInstanceConnectedToMasterFor(nativeGenerator);
        driver.ExecuteMachineCommand(nativeGenerator.SetStereoSampleValueTo(new Sample(2, 3), 10, 100));

        var samples = driver.ReadStereoSamples(1);

        samples.AssertAreEqualTo([ExpectedSampleValue.From(new Sample(0.2f, 0.03f))]);
    }

    [Test]
    public void OutputsASampleWhichIsASumFromAllMachines()
    {
        var gen1Sample = new Sample(5, 10);
        var gen2Sample = new Sample(2, 5);
        using var driver = new Driver();
        var gen1Controller = FakeNativeGeneratorController.NewInstance("s1");
        var gen2Controller = FakeNativeGeneratorController.NewInstance("s2");
        driver.AddPrecompiledGeneratorToGear(FakeNativeGeneratorController.Info);

        driver.Start();

        driver.InsertMachineInstanceConnectedToMasterFor(gen1Controller);
        driver.InsertMachineInstanceConnectedToMasterFor(gen2Controller);
        driver.ExecuteMachineCommand(gen1Controller.SetStereoSampleValueTo(gen1Sample));
        driver.ExecuteMachineCommand(gen2Controller.SetStereoSampleValueTo(gen2Sample));

        var samples = driver.ReadStereoSamples(1);

        samples.AssertAreEqualTo([ExpectedSampleValue.From(gen1Sample + gen2Sample)]);
    }

    [Test]
    public void OutputsASampleWhichIsASumFromAllMachinesTimesMasterVolume()
    {
        const double masterVolume1 = 0.5;
        const double masterVolume2 = 0.25;
        var gen1Sample = new Sample(5, 1);
        var gen2Sample = new Sample(2, 5);
        using var driver = new Driver();
        var gen1Controller = FakeNativeGeneratorController.NewInstance("s1");
        var gen2Controller = FakeNativeGeneratorController.NewInstance("s2");
        driver.AddPrecompiledGeneratorToGear(FakeNativeGeneratorController.Info);

        driver.Start();

        driver.InsertMachineInstanceConnectedToMasterFor(gen1Controller);
        driver.InsertMachineInstanceConnectedToMasterFor(gen2Controller);
        driver.ExecuteMachineCommand(gen1Controller.SetStereoSampleValueTo(gen1Sample));
        driver.ExecuteMachineCommand(gen2Controller.SetStereoSampleValueTo(gen2Sample));

        driver.SetMasterVolumeTo(masterVolume1);
        var samples1 = driver.ReadStereoSamples(1);
        samples1.AssertAreEqualTo([ExpectedSampleValue.From(gen1Sample + gen2Sample, masterVolume1)]);

        driver.SetMasterVolumeTo(masterVolume2);
        var samples2 = driver.ReadStereoSamples(1);
        samples2.AssertAreEqualTo([ExpectedSampleValue.From(gen1Sample + gen2Sample, masterVolume2)]);
    }
}