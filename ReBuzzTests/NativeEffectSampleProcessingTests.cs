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
            driver.AddPrecompiledGeneratorToGear(FakeNativeGeneratorInfo.Instance);
            driver.AddPrecompiledEffectToGear(FakeNativeEffectInfo.Instance);
            driver.Start();

            driver.InsertMachineInstanceConnectedToMasterFor(nativeEffect);
            driver.InsertMachineInstanceFor(nativeGenerator);
            driver.Connect(nativeGenerator, nativeEffect);
            driver.ExecuteMachineCommand(nativeGenerator.SetStereoSampleValueTo(sampleFromGenerator));
            driver.ExecuteMachineCommand(nativeEffect.SetStereoSampleMultiplier(effectMultiplier));

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
            driver.AddPrecompiledGeneratorToGear(FakeNativeGeneratorInfo.Instance);
            driver.AddPrecompiledEffectToGear(FakeNativeEffectInfo.Instance);
            driver.Start();

            driver.InsertMachineInstanceConnectedToMasterFor(nativeEffect);
            driver.InsertMachineInstanceFor(nativeGenerator);
            driver.Connect(nativeGenerator, nativeEffect);
            driver.ExecuteMachineCommand(nativeGenerator.SetStereoSampleValueTo(sampleFromGenerator));
            driver.ExecuteMachineCommand(nativeEffect.SetStereoSampleMultiplier(effectMultiplier));

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
            var effectMultiplier = 2;
            using var driver = new Driver();
            var gen1Controller = FakeNativeGeneratorController.NewInstance("s1");
            var gen2Controller = FakeNativeGeneratorController.NewInstance("s2");
            var effectController = FakeNativeEffectController.NewInstance();
            driver.AddPrecompiledGeneratorToGear(FakeNativeGeneratorController.Info);
            driver.AddPrecompiledEffectToGear(FakeNativeEffectController.Info);

            driver.Start();

            driver.InsertMachineInstanceConnectedToMasterFor(effectController);
            driver.InsertMachineInstanceFor(gen1Controller);
            driver.InsertMachineInstanceFor(gen2Controller);
            driver.Connect(gen1Controller, effectController);
            driver.Connect(gen2Controller, effectController);
            driver.ExecuteMachineCommand(gen1Controller.SetStereoSampleValueTo(gen1Sample));
            driver.ExecuteMachineCommand(gen2Controller.SetStereoSampleValueTo(gen2Sample));
            driver.ExecuteMachineCommand(effectController.SetStereoSampleMultiplier(effectMultiplier));

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
            var effectMultiplier = 2;
            using var driver = new Driver();
            var gen1Controller = FakeNativeGeneratorController.NewInstance("s1");
            var gen2Controller = FakeNativeGeneratorController.NewInstance("s2");
            var effectController = FakeNativeEffectController.NewInstance();
            driver.AddPrecompiledGeneratorToGear(FakeNativeGeneratorController.Info);
            driver.AddPrecompiledEffectToGear(FakeNativeEffectController.Info);

            driver.Start();

            driver.InsertMachineInstanceConnectedToMasterFor(effectController);
            driver.InsertMachineInstanceFor(gen1Controller);
            driver.InsertMachineInstanceFor(gen2Controller);
            driver.Connect(gen1Controller, effectController);
            driver.Connect(gen2Controller, effectController);
            driver.ExecuteMachineCommand(gen1Controller.SetStereoSampleValueTo(gen1Sample));
            driver.ExecuteMachineCommand(gen2Controller.SetStereoSampleValueTo(gen2Sample));
            driver.ExecuteMachineCommand(effectController.SetStereoSampleMultiplier(effectMultiplier));

            driver.SetMasterVolumeTo(masterVolume1);
            var samples1 = driver.ReadStereoSamples(1);
            samples1.AssertAreEqualTo([ExpectedSampleValue.From(gen1Sample + gen2Sample, masterVolume1) * effectMultiplier]);

            driver.SetMasterVolumeTo(masterVolume2);
            var samples2 = driver.ReadStereoSamples(1);
            samples2.AssertAreEqualTo([ExpectedSampleValue.From(gen1Sample + gen2Sample, masterVolume2) * effectMultiplier]);
        }

        [Test]
        public void OutputsASampleFromNativeGeneratorThroughMultipleNativeEffectsConnectedInAChain()
        {
            var genSample = new Sample(5, 10);
            var effect1Multiplier = 2;
            var effect2Multiplier = 3;
            using var driver = new Driver();
            var genController = FakeNativeGeneratorController.NewInstance("s1");
            var effect1Controller = FakeNativeEffectController.NewInstance("e1");
            var effect2Controller = FakeNativeEffectController.NewInstance("e2");
            driver.AddPrecompiledGeneratorToGear(FakeNativeGeneratorController.Info);
            driver.AddPrecompiledEffectToGear(FakeNativeEffectController.Info);

            driver.Start();

            driver.InsertMachineInstanceConnectedToMasterFor(effect2Controller);
            driver.InsertMachineInstanceFor(effect1Controller);
            driver.InsertMachineInstanceFor(genController);
            driver.Connect(genController, effect1Controller);
            driver.Connect(effect1Controller, effect2Controller);
            driver.ExecuteMachineCommand(genController.SetStereoSampleValueTo(genSample));
            driver.ExecuteMachineCommand(effect1Controller.SetStereoSampleMultiplier(effect1Multiplier));
            driver.ExecuteMachineCommand(effect2Controller.SetStereoSampleMultiplier(effect2Multiplier));

            var samples = driver.ReadStereoSamples(1);

            samples.AssertAreEqualTo([ExpectedSampleValue.From(genSample) * effect1Multiplier * effect2Multiplier]);
        }

        [Test]
        public void DoesNotOutputSamplesFromCrashedGenerators() //bug move to generators or a new suite
        {
            var sampleFromGenerator = new Sample(2, 3);
            var sampleFromCrashedGenerator = new Sample(3, 2);
            using var driver = new Driver();
            var crashingGenerator = FakeNativeGeneratorController.NewInstance("crashingGen");
            var okGenerator1 = FakeNativeGeneratorController.NewInstance("okGen1");
            var okGenerator2 = FakeNativeGeneratorController.NewInstance("okGen2");
            driver.AddPrecompiledGeneratorToGear(FakeNativeGeneratorInfo.Instance);
            driver.Start();

            driver.InsertMachineInstanceConnectedToMasterFor(okGenerator1);
            driver.EnableGeneratorCrashing();
            driver.InsertMachineInstanceConnectedToMasterFor(crashingGenerator);
            driver.DisableGeneratorCrashing();
            driver.InsertMachineInstanceConnectedToMasterFor(okGenerator2);

            driver.ExecuteMachineCommand(okGenerator1.SetStereoSampleValueTo(sampleFromGenerator));
            driver.ExecuteMachineCommand(okGenerator2.SetStereoSampleValueTo(sampleFromGenerator));
            driver.ExecuteMachineCommand(crashingGenerator.SetStereoSampleValueTo(sampleFromCrashedGenerator));

            var samples = driver.ReadStereoSamples(1);

            samples.AssertAreEqualTo([
                ExpectedSampleValue.From(sampleFromGenerator + sampleFromGenerator)
            ]);

            driver.AssertIsCrashed(crashingGenerator);
            //bug assert crash is logged (maybe even in the assertion above)
        }
    }
}