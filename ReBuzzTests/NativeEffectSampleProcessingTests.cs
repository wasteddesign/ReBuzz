using Buzz.MachineInterface;
using ReBuzzTests.Automation;
using ReBuzzTests.Automation.TestMachines;
using ReBuzzTests.Automation.TestMachinesControllers;

namespace ReBuzzTests
{
    public class NativeEffectSampleProcessingTests
    {
        [Test]
        public void OutputsSingleSampleFromAStereoGeneratorProcessedByStereoEffect()
        {
            const int effectMultiplier = 2;
            var sampleFromGenerator = new Sample(2, 3);
            using var driver = new Driver();
            var nativeGenerator = FakeNativeGeneratorController.NewInstance();
            var nativeEffect = FakeNativeEffectController.NewInstance();
            driver.Gear.AddPrecompiledGenerator(FakeNativeGeneratorInfo.Instance);
            driver.Gear.AddPrecompiledEffect(FakeNativeEffectInfo.Instance);
            driver.Start();

            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(nativeEffect);
            driver.MachineGraph.InsertMachineInstanceFor(nativeGenerator);
            driver.MachineGraph.Connect(nativeGenerator, nativeEffect);
            driver.MachineGraph.ExecuteMachineCommand(nativeGenerator.SetStereoSampleValueTo(sampleFromGenerator));
            driver.MachineGraph.ExecuteMachineCommand(nativeEffect.SetStereoSampleMultiplier(effectMultiplier));

            var samples = driver.ReadStereoSamples(1);

            samples.AssertAreEqualTo([ExpectedSampleValue.From(sampleFromGenerator) * effectMultiplier]);
        }

        [Test]
        public void OutputsMultipleSamplesFromAStereoGeneratorProcessedByStereoEffect()
        {
            const int effectMultiplier = 2;
            var sampleFromGenerator = new Sample(2, 3);
            using var driver = new Driver();
            var nativeGenerator = FakeNativeGeneratorController.NewInstance();
            var nativeEffect = FakeNativeEffectController.NewInstance();
            driver.Gear.AddPrecompiledGenerator(FakeNativeGeneratorInfo.Instance);
            driver.Gear.AddPrecompiledEffect(FakeNativeEffectInfo.Instance);
            driver.Start();

            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(nativeEffect);
            driver.MachineGraph.InsertMachineInstanceFor(nativeGenerator);
            driver.MachineGraph.Connect(nativeGenerator, nativeEffect);
            driver.MachineGraph.ExecuteMachineCommand(nativeGenerator.SetStereoSampleValueTo(sampleFromGenerator));
            driver.MachineGraph.ExecuteMachineCommand(nativeEffect.SetStereoSampleMultiplier(effectMultiplier));

            var samples = driver.ReadStereoSamples(2);

            samples.AssertAreEqualTo([
                ExpectedSampleValue.From(sampleFromGenerator) * effectMultiplier,
                ExpectedSampleValue.From(sampleFromGenerator) * effectMultiplier
            ]);
        }

        [Test]
        public void OutputsASampleWhichIsASumFromAllMachinesConnectedToAnEffectMultipliedByTheEffect()
        {
            var gen1Sample = new Sample(5, 10);
            var gen2Sample = new Sample(2, 5);
            const int effectMultiplier = 2;
            using var driver = new Driver();
            var gen1Controller = FakeNativeGeneratorController.NewInstance("s1");
            var gen2Controller = FakeNativeGeneratorController.NewInstance("s2");
            var effectController = FakeNativeEffectController.NewInstance();
            driver.Gear.AddPrecompiledGenerator(FakeNativeGeneratorController.Info);
            driver.Gear.AddPrecompiledEffect(FakeNativeEffectController.Info);

            driver.Start();

            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(effectController);
            driver.MachineGraph.InsertMachineInstanceFor(gen1Controller);
            driver.MachineGraph.InsertMachineInstanceFor(gen2Controller);
            driver.MachineGraph.Connect(gen1Controller, effectController);
            driver.MachineGraph.Connect(gen2Controller, effectController);
            driver.MachineGraph.ExecuteMachineCommand(gen1Controller.SetStereoSampleValueTo(gen1Sample));
            driver.MachineGraph.ExecuteMachineCommand(gen2Controller.SetStereoSampleValueTo(gen2Sample));
            driver.MachineGraph.ExecuteMachineCommand(effectController.SetStereoSampleMultiplier(effectMultiplier));

            var samples = driver.ReadStereoSamples(1);

            samples.AssertAreEqualTo([ExpectedSampleValue.From(gen1Sample + gen2Sample) * effectMultiplier]);
        }

        [Test]
        public void OutputsASampleWhichIsASumFromAllMachinesTimesMasterVolume()
        {
            const double masterVolume1 = 0.5;
            const double masterVolume2 = 0.25;
            var gen1Sample = new Sample(5, 10);
            var gen2Sample = new Sample(2, 5);
            const int effectMultiplier = 2;
            using var driver = new Driver();
            var gen1Controller = FakeNativeGeneratorController.NewInstance("s1");
            var gen2Controller = FakeNativeGeneratorController.NewInstance("s2");
            var effectController = FakeNativeEffectController.NewInstance();
            driver.Gear.AddPrecompiledGenerator(FakeNativeGeneratorController.Info);
            driver.Gear.AddPrecompiledEffect(FakeNativeEffectController.Info);

            driver.Start();

            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(effectController);
            driver.MachineGraph.InsertMachineInstanceFor(gen1Controller);
            driver.MachineGraph.InsertMachineInstanceFor(gen2Controller);
            driver.MachineGraph.Connect(gen1Controller, effectController);
            driver.MachineGraph.Connect(gen2Controller, effectController);
            driver.MachineGraph.ExecuteMachineCommand(gen1Controller.SetStereoSampleValueTo(gen1Sample));
            driver.MachineGraph.ExecuteMachineCommand(gen2Controller.SetStereoSampleValueTo(gen2Sample));
            driver.MachineGraph.ExecuteMachineCommand(effectController.SetStereoSampleMultiplier(effectMultiplier));

            driver.SetMasterVolumeTo(masterVolume1);
            var samples1 = driver.ReadStereoSamples(1);
            samples1.AssertAreEqualTo([ExpectedSampleValue.From(gen1Sample + gen2Sample, masterVolume1) * effectMultiplier]);

            driver.SetMasterVolumeTo(masterVolume2);
            var samples2 = driver.ReadStereoSamples(1);
            samples2.AssertAreEqualTo([ExpectedSampleValue.From(gen1Sample + gen2Sample, masterVolume2) * effectMultiplier]);
        }

        [Test]
        public void OutputsASampleTwice()
        {
            var gen1Sample = new Sample(5, 10);
            using var driver = new Driver();
            var gen1Controller = FakeNativeGeneratorController.NewInstance("s1");
            driver.Gear.AddPrecompiledGenerator(FakeNativeGeneratorController.Info);
            driver.Gear.AddPrecompiledEffect(FakeNativeEffectController.Info);

            driver.Start();

            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(gen1Controller);
            driver.MachineGraph.ExecuteMachineCommand(gen1Controller.SetStereoSampleValueTo(gen1Sample));

            var samples1 = driver.ReadStereoSamples(1);
            samples1.AssertAreEqualTo([ExpectedSampleValue.From(gen1Sample)]);

            var samples2 = driver.ReadStereoSamples(1);
            samples2.AssertAreEqualTo([ExpectedSampleValue.From(gen1Sample)]);
        }

        [Test]
        public void OutputsASampleFromNativeGeneratorThroughMultipleNativeEffectsConnectedInAChain()
        {
            var genSample = new Sample(5, 10);
            const int effect1Multiplier = 2;
            const int effect2Multiplier = 3;
            using var driver = new Driver();
            var genController = FakeNativeGeneratorController.NewInstance("s1");
            var effect1Controller = FakeNativeEffectController.NewInstance("e1");
            var effect2Controller = FakeNativeEffectController.NewInstance("e2");
            driver.Gear.AddPrecompiledGenerator(FakeNativeGeneratorController.Info);
            driver.Gear.AddPrecompiledEffect(FakeNativeEffectController.Info);

            driver.Start();

            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(effect2Controller);
            driver.MachineGraph.InsertMachineInstanceFor(effect1Controller);
            driver.MachineGraph.InsertMachineInstanceFor(genController);
            driver.MachineGraph.Connect(genController, effect1Controller);
            driver.MachineGraph.Connect(effect1Controller, effect2Controller);
            driver.MachineGraph.ExecuteMachineCommand(genController.SetStereoSampleValueTo(genSample));
            driver.MachineGraph.ExecuteMachineCommand(effect1Controller.SetStereoSampleMultiplier(effect1Multiplier));
            driver.MachineGraph.ExecuteMachineCommand(effect2Controller.SetStereoSampleMultiplier(effect2Multiplier));

            var samples = driver.ReadStereoSamples(1);

            samples.AssertAreEqualTo([ExpectedSampleValue.From(genSample) * effect1Multiplier * effect2Multiplier]);
        }
    }
}