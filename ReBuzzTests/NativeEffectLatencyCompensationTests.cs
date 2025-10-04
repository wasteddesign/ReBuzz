using Buzz.MachineInterface;
using ReBuzzTests.Automation;
using ReBuzzTests.Automation.TestMachines;
using ReBuzzTests.Automation.TestMachinesControllers;

namespace ReBuzzTests
{
    public class NativeEffectLatencyCompensationTests
    {
        [Test]
        public void OutputsFinalSamplesBasedOnIndividualEffectLatencies()
        {
            var sampleFromGenerator1 = new Sample(2, 3);
            var sampleFromGenerator2 = new Sample(5, 4);
            var sampleFromGenerator3 = new Sample(7, 8);
            using var driver = new Driver();
            var nativeGenerator1 = FakeNativeGeneratorController.NewInstance("s1");
            var nativeGenerator2 = FakeNativeGeneratorController.NewInstance("s2");
            var nativeGenerator3 = FakeNativeGeneratorController.NewInstance("s3");
            var nativeEffect1 = FakeNativeEffectController.NewInstance("e1");
            var nativeEffect2 = FakeNativeEffectController.NewInstance("e2");
            var nativeEffect3 = FakeNativeEffectController.NewInstance("e3");
            driver.Gear.AddPrecompiledGenerator(FakeNativeGeneratorInfo.Instance);
            driver.Gear.AddPrecompiledEffect(FakeNativeEffectInfo.Instance);
            driver.Start();

            driver.MachineGraph.SaveMachineInitialConfiguration(nativeEffect1, new TestMachineConfig { Latency = 0 });
            driver.MachineGraph.SaveMachineInitialConfiguration(nativeEffect2, new TestMachineConfig { Latency = 1 });
            driver.MachineGraph.SaveMachineInitialConfiguration(nativeEffect3, new TestMachineConfig { Latency = 2 });
            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(nativeEffect1);
            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(nativeEffect2);
            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(nativeEffect3);
            driver.MachineGraph.InsertMachineInstanceFor(nativeGenerator1);
            driver.MachineGraph.InsertMachineInstanceFor(nativeGenerator2);
            driver.MachineGraph.InsertMachineInstanceFor(nativeGenerator3);
            driver.MachineGraph.Connect(nativeGenerator1, nativeEffect1);
            driver.MachineGraph.Connect(nativeGenerator2, nativeEffect2);
            driver.MachineGraph.Connect(nativeGenerator3, nativeEffect3);
            driver.MachineGraph.ExecuteMachineCommand(nativeGenerator1.SetStereoSampleValueTo(sampleFromGenerator1));
            driver.MachineGraph.ExecuteMachineCommand(nativeGenerator2.SetStereoSampleValueTo(sampleFromGenerator2));
            driver.MachineGraph.ExecuteMachineCommand(nativeGenerator3.SetStereoSampleValueTo(sampleFromGenerator3));
            driver.MachineGraph.ExecuteMachineCommand(nativeEffect1.SetStereoSampleMultiplier(1));
            driver.MachineGraph.ExecuteMachineCommand(nativeEffect2.SetStereoSampleMultiplier(1));
            driver.MachineGraph.ExecuteMachineCommand(nativeEffect3.SetStereoSampleMultiplier(1));

            var samples = driver.ReadStereoSamples(4);

            samples.AssertAreEqualTo(
            [
                ExpectedSampleValue.From(sampleFromGenerator3) + ExpectedSampleValue.Zero() + ExpectedSampleValue.Zero(),
                ExpectedSampleValue.From(sampleFromGenerator3) + ExpectedSampleValue.From(sampleFromGenerator2) + ExpectedSampleValue.Zero(),
                ExpectedSampleValue.From(sampleFromGenerator3) + ExpectedSampleValue.From(sampleFromGenerator2) + ExpectedSampleValue.From(sampleFromGenerator1),
                ExpectedSampleValue.From(sampleFromGenerator3) + ExpectedSampleValue.From(sampleFromGenerator2) + ExpectedSampleValue.From(sampleFromGenerator1)
            ]);
        }

        [Test]
        public void SumsOutputsFromParallelEffectsImmediatelyWhenAllHaveTheSameLatency()
        {
            const int effect1Multiplier = 2;
            const int effect2Multiplier = 3;
            var sampleFromGenerator = new Sample(2, 3);
            using var driver = new Driver();
            var nativeGenerator = FakeNativeGeneratorController.NewInstance();
            var nativeEffect1 = FakeNativeEffectController.NewInstance("e1");
            var nativeEffect2 = FakeNativeEffectController.NewInstance("e2");
            driver.Gear.AddPrecompiledGenerator(FakeNativeGeneratorInfo.Instance);
            driver.Gear.AddPrecompiledEffect(FakeNativeEffectInfo.Instance);
            driver.Start();

            driver.MachineGraph.SaveMachineInitialConfiguration(nativeEffect1, new TestMachineConfig { Latency = 2000 });
            driver.MachineGraph.SaveMachineInitialConfiguration(nativeEffect2, new TestMachineConfig { Latency = 2000 });
            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(nativeEffect1);
            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(nativeEffect2);
            driver.MachineGraph.InsertMachineInstanceFor(nativeGenerator);
            driver.MachineGraph.Connect(nativeGenerator, nativeEffect1);
            driver.MachineGraph.Connect(nativeGenerator, nativeEffect2);
            driver.MachineGraph.ExecuteMachineCommand(nativeGenerator.SetStereoSampleValueTo(sampleFromGenerator));
            driver.MachineGraph.ExecuteMachineCommand(nativeEffect1.SetStereoSampleMultiplier(effect1Multiplier));
            driver.MachineGraph.ExecuteMachineCommand(nativeEffect2.SetStereoSampleMultiplier(effect2Multiplier));

            var samples = driver.ReadStereoSamples(1);

            samples.AssertAreEqualTo([ExpectedSampleValue.From(sampleFromGenerator) * (effect1Multiplier + effect2Multiplier)]);
        }

        [Test]
        public void AggregatesLatencyAcrossChainedEffects()
        {
            var sampleFromGenerator1 = new Sample(2, 3);
            var sampleFromGenerator2 = new Sample(5, 7);
            using var driver = new Driver();
            var nativeGenerator1 = FakeNativeGeneratorController.NewInstance("s1");
            var nativeGenerator2 = FakeNativeGeneratorController.NewInstance("s2");
            var nativeEffect1 = FakeNativeEffectController.NewInstance("e1");
            var nativeEffect2 = FakeNativeEffectController.NewInstance("e2");
            var nativeEffect3 = FakeNativeEffectController.NewInstance("e3");
            driver.Gear.AddPrecompiledGenerator(FakeNativeGeneratorInfo.Instance);
            driver.Gear.AddPrecompiledEffect(FakeNativeEffectInfo.Instance);
            driver.Start();

            driver.MachineGraph.SaveMachineInitialConfiguration(nativeEffect1, new TestMachineConfig { Latency = 0 });
            driver.MachineGraph.SaveMachineInitialConfiguration(nativeEffect2, new TestMachineConfig { Latency = 1 });
            driver.MachineGraph.SaveMachineInitialConfiguration(nativeEffect3, new TestMachineConfig { Latency = 2 });
            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(nativeEffect3);
            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(nativeGenerator2);
            driver.MachineGraph.InsertMachineInstanceFor(nativeEffect2);
            driver.MachineGraph.InsertMachineInstanceFor(nativeEffect1);
            driver.MachineGraph.InsertMachineInstanceFor(nativeGenerator1);
            driver.MachineGraph.Connect(nativeGenerator1, nativeEffect1);
            driver.MachineGraph.Connect(nativeEffect1, nativeEffect2);
            driver.MachineGraph.Connect(nativeEffect2, nativeEffect3);
            driver.MachineGraph.ExecuteMachineCommand(nativeGenerator1.SetStereoSampleValueTo(sampleFromGenerator1));
            driver.MachineGraph.ExecuteMachineCommand(nativeGenerator2.SetStereoSampleValueTo(sampleFromGenerator2));
            driver.MachineGraph.ExecuteMachineCommand(nativeEffect1.SetStereoSampleMultiplier(1));
            driver.MachineGraph.ExecuteMachineCommand(nativeEffect2.SetStereoSampleMultiplier(1));
            driver.MachineGraph.ExecuteMachineCommand(nativeEffect3.SetStereoSampleMultiplier(1));

            var samples = driver.ReadStereoSamples(4);

            samples.AssertAreEqualTo(
            [
                ExpectedSampleValue.From(sampleFromGenerator1),
                ExpectedSampleValue.From(sampleFromGenerator1),
                ExpectedSampleValue.From(sampleFromGenerator1),
                ExpectedSampleValue.From(sampleFromGenerator1) + ExpectedSampleValue.From(sampleFromGenerator2)
            ]);
        }
    }
}