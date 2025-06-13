using Buzz.MachineInterface;
using ReBuzzTests.Automation;
using ReBuzzTests.Automation.TestMachines;
using ReBuzzTests.Automation.TestMachinesControllers;

namespace ReBuzzTests
{
    public class NativeMachineCrashingTests
    {
        [Test]
        public void DoesNotOutputSamplesFromGeneratorCrashedOnLoadingNewInstance()
        {
            var sampleFromGenerator1 = new Sample(2, 3);
            var sampleFromGenerator2 = new Sample(7, 6);
            var sampleFromCrashedGenerator = new Sample(3, 2);
            using var driver = new Driver();
            var crashingGenerator = FakeNativeGeneratorController.NewInstance("crashingGen");
            var okGenerator1 = FakeNativeGeneratorController.NewInstance("okGen1");
            var okGenerator2 = FakeNativeGeneratorController.NewInstance("okGen2");
            driver.Gear.AddPrecompiledGenerator(FakeNativeGeneratorInfo.Instance);
            driver.Start();

            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(okGenerator1);
            
            driver.EnableGeneratorCrashingFor(crashingGenerator);
            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(crashingGenerator);
            
            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(okGenerator2);

            driver.MachineGraph.ExecuteMachineCommand(okGenerator1.SetStereoSampleValueTo(sampleFromGenerator1));
            driver.MachineGraph.ExecuteMachineCommand(okGenerator2.SetStereoSampleValueTo(sampleFromGenerator2));
            driver.MachineGraph.ExecuteMachineCommand(crashingGenerator.SetStereoSampleValueTo(sampleFromCrashedGenerator));

            var samples = driver.ReadStereoSamples(1);

            driver.AssertMachineIsCrashed(crashingGenerator);
            driver.ReBuzzLog.AssertLogContainsCannotAccessDisposedObjectMessage();
            driver.ReBuzzLog.AssertLogContainsInvalidPointerMessage();
            samples.AssertAreEqualTo([
                ExpectedSampleValue.From(sampleFromGenerator1 + sampleFromGenerator2)
            ]);
        }

        [Test]
        public void DoesNotOutputSamplesFromCrashedGenerators()
        {
            var sampleFromGenerator1 = new Sample(2, 3);
            var sampleFromGenerator2 = new Sample(7, 6);
            var sampleFromCrashedGenerator = new Sample(3, 2);
            using var driver = new Driver();
            var crashingGenerator = FakeNativeGeneratorController.NewInstance("crashingGen");
            var okGenerator1 = FakeNativeGeneratorController.NewInstance("okGen1");
            var okGenerator2 = FakeNativeGeneratorController.NewInstance("okGen2");
            driver.Gear.AddPrecompiledGenerator(FakeNativeGeneratorInfo.Instance);
            driver.Start();

            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(okGenerator1);
            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(crashingGenerator);
            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(okGenerator2);

            driver.MachineGraph.ExecuteMachineCommand(okGenerator1.SetStereoSampleValueTo(sampleFromGenerator1));
            driver.MachineGraph.ExecuteMachineCommand(okGenerator2.SetStereoSampleValueTo(sampleFromGenerator2));
            driver.MachineGraph.ExecuteMachineCommand(crashingGenerator.SetStereoSampleValueTo(sampleFromCrashedGenerator));

            driver.EnableGeneratorCrashingFor(crashingGenerator);
            var samples = driver.ReadStereoSamples(1);

            driver.AssertMachineIsCrashed(crashingGenerator);
            driver.ReBuzzLog.AssertLogContainsIndexOutsideArrayBoundsMessage();
            samples.AssertAreEqualTo([
                ExpectedSampleValue.From(sampleFromGenerator1 + sampleFromGenerator2)
            ]);

        }

        [Test]
        public void DoesNotRouteSoundThroughEffectsCrashedOnLoadingNewInstance()
        {
            using var driver = new Driver();
            var crashingEffect = FakeNativeEffectController.NewInstance("crashingEffect");
            var okGenerator = FakeNativeGeneratorController.NewInstance("okGen");
            driver.Gear.AddPrecompiledGenerator(FakeNativeGeneratorInfo.Instance);
            driver.Gear.AddPrecompiledEffect(FakeNativeEffectInfo.Instance);
            driver.Start();

            driver.MachineGraph.InsertMachineInstanceFor(okGenerator);
            driver.EnableEffectCrashingFor(crashingEffect);
            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(crashingEffect);
            driver.MachineGraph.Connect(okGenerator, crashingEffect);

            driver.MachineGraph.ExecuteMachineCommand(okGenerator.SetStereoSampleValueTo(new Sample(2, 3)));
            driver.MachineGraph.ExecuteMachineCommand(crashingEffect.SetStereoSampleMultiplier(2));

            var samples = driver.ReadStereoSamples(1);

            driver.AssertMachineIsCrashed(crashingEffect);
            driver.ReBuzzLog.AssertLogContainsCannotAccessDisposedObjectMessage();
            driver.ReBuzzLog.AssertLogContainsInvalidPointerMessage();
            samples.AssertAreEqualTo([ExpectedSampleValue.Zero()]);
        }

        [Test]
        public void IgnoresEffectsCrashDuringSampleGenerationInSignalRoute()
        {
            using var driver = new Driver();
            var crashingEffect = FakeNativeEffectController.NewInstance("crashingEffect");
            var okGenerator = FakeNativeGeneratorController.NewInstance("okGen");
            var sampleFromGenerator = new Sample(2, 3);
            driver.Gear.AddPrecompiledGenerator(FakeNativeGeneratorInfo.Instance);
            driver.Gear.AddPrecompiledEffect(FakeNativeEffectInfo.Instance);
            driver.Start();

            driver.MachineGraph.InsertMachineInstanceFor(okGenerator);
            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(crashingEffect);
            driver.MachineGraph.Connect(okGenerator, crashingEffect);

            driver.MachineGraph.ExecuteMachineCommand(okGenerator.SetStereoSampleValueTo(sampleFromGenerator));
            driver.MachineGraph.ExecuteMachineCommand(crashingEffect.SetStereoSampleMultiplier(2));

            driver.EnableEffectCrashingFor(crashingEffect);
            var samples = driver.ReadStereoSamples(1);

            driver.AssertMachineIsCrashed(crashingEffect);
            driver.ReBuzzLog.AssertLogContainsIndexOutsideArrayBoundsMessage();
            samples.AssertAreEqualTo([ExpectedSampleValue.From(sampleFromGenerator)]);
        }
    }
}