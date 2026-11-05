using AwesomeAssertions;
using Buzz.MachineInterface;
using ReBuzz.Core;
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
            var nativeGenerator1 = FakeNativeGeneratorStereoController.NewInstance("s1");
            var nativeGenerator2 = FakeNativeGeneratorStereoController.NewInstance("s2");
            var nativeGenerator3 = FakeNativeGeneratorStereoController.NewInstance("s3");
            var nativeEffect1 = FakeNativeEffectController.NewInstance("e1");
            var nativeEffect2 = FakeNativeEffectController.NewInstance("e2");
            var nativeEffect3 = FakeNativeEffectController.NewInstance("e3");
            driver.Gear.AddPrecompiledGenerator(FakeNativeGeneratorInfo.StereoGeneratorInstance);
            driver.Gear.AddPrecompiledEffect(FakeNativeEffectInfo.Instance);
            driver.Start();

            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(nativeEffect1, new TestMachineConfig { Latency = 0 });
            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(nativeEffect2, new TestMachineConfig { Latency = 1 });
            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(nativeEffect3, new TestMachineConfig { Latency = 2 });
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
        public void CompensatesLatencyAfterLoadingASong()
        {
            using var driver = new Driver();
            var nativeEffect1 = FakeNativeEffectController.NewInstance("e1");
            var nativeEffect2 = FakeNativeEffectController.NewInstance("e2");
            driver.Gear.AddPrecompiledEffect(FakeNativeEffectInfo.Instance);
            driver.Start();

            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(nativeEffect1);
            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(nativeEffect2);
            driver.MachineGraph.ExecuteMachineCommand(new SetOverrideLatencyCommand(nativeEffect2.InstanceName, 1));
            TotalLatency(driver).Should().Be(1);

            // The override is saved with the machine's properties. Delay compensation is
            // calculated once, after the whole song is loaded.
            var songPath = driver.RandomSongPath();
            driver.DawCommands.SaveCurrentSongForTheFirstTime(DialogChoices.Select(songPath));
            driver.DawCommands.LoadSong(DialogChoices.Select(songPath));

            driver.AssertNoErrorsReportedToUser();
            TotalLatency(driver).Should().Be(1);
        }

        [Test]
        public void SumsOutputsFromParallelEffectsImmediatelyWhenAllHaveTheSameLatency()
        {
            const int effect1Multiplier = 2;
            const int effect2Multiplier = 3;
            var sampleFromGenerator = new Sample(2, 3);
            using var driver = new Driver();
            var nativeGenerator = FakeNativeGeneratorStereoController.NewInstance();
            var nativeEffect1 = FakeNativeEffectController.NewInstance("e1");
            var nativeEffect2 = FakeNativeEffectController.NewInstance("e2");
            driver.Gear.AddPrecompiledGenerator(FakeNativeGeneratorInfo.StereoGeneratorInstance);
            driver.Gear.AddPrecompiledEffect(FakeNativeEffectInfo.Instance);
            driver.Start();

            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(nativeEffect1, new TestMachineConfig { Latency = 2000 });
            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(nativeEffect2, new TestMachineConfig { Latency = 2000 });
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
            var nativeGenerator1 = FakeNativeGeneratorStereoController.NewInstance("s1");
            var nativeGenerator2 = FakeNativeGeneratorStereoController.NewInstance("s2");
            var nativeEffect1 = FakeNativeEffectController.NewInstance("e1");
            var nativeEffect2 = FakeNativeEffectController.NewInstance("e2");
            var nativeEffect3 = FakeNativeEffectController.NewInstance("e3");
            driver.Gear.AddPrecompiledGenerator(FakeNativeGeneratorInfo.StereoGeneratorInstance);
            driver.Gear.AddPrecompiledEffect(FakeNativeEffectInfo.Instance);
            driver.Start();

            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(nativeEffect3, new TestMachineConfig { Latency = 2 });
            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(nativeGenerator2);
            driver.MachineGraph.InsertMachineInstanceFor(nativeEffect2, new TestMachineConfig { Latency = 1 });
            driver.MachineGraph.InsertMachineInstanceFor(nativeEffect1, new TestMachineConfig { Latency = 0 });
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

        private static int TotalLatency(Driver driver)
        {
            var command = new ReadTotalLatencyCommand();
            driver.MachineGraph.ExecuteMachineCommand(command);
            return command.TotalLatency;
        }

        private class SetOverrideLatencyCommand(string instanceName, int latency) : ITestMachineInstanceCommand
        {
            public void Execute(ReBuzzCore buzzCore, ReBuzzMachines machineCores)
            {
                machineCores.GetSongCoreMachineInstance(instanceName).OverrideLatency = latency;
            }
        }

        private class ReadTotalLatencyCommand : ITestMachineInstanceCommand
        {
            public int TotalLatency { get; private set; }

            public void Execute(ReBuzzCore buzzCore, ReBuzzMachines machineCores)
            {
                TotalLatency = buzzCore.totalLatency;
            }
        }
    }
}