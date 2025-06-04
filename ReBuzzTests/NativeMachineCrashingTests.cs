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
            driver.AddPrecompiledGeneratorToGear(FakeNativeGeneratorInfo.Instance);
            driver.Start();

            driver.InsertMachineInstanceConnectedToMasterFor(okGenerator1);
            
            driver.EnableGeneratorCrashingFor(crashingGenerator);
            driver.InsertMachineInstanceConnectedToMasterFor(crashingGenerator);
            
            driver.InsertMachineInstanceConnectedToMasterFor(okGenerator2);

            driver.ExecuteMachineCommand(okGenerator1.SetStereoSampleValueTo(sampleFromGenerator1));
            driver.ExecuteMachineCommand(okGenerator2.SetStereoSampleValueTo(sampleFromGenerator2));
            driver.ExecuteMachineCommand(crashingGenerator.SetStereoSampleValueTo(sampleFromCrashedGenerator));

            var samples = driver.ReadStereoSamples(1);

            driver.AssertMachineIsCrashed(crashingGenerator);
            driver.AssertLogContainsCannotAccessDisposedObjectMessage();
            driver.AssertLogContainsInvalidPointerMessage();
            
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
            driver.AddPrecompiledGeneratorToGear(FakeNativeGeneratorInfo.Instance);
            driver.Start();

            driver.InsertMachineInstanceConnectedToMasterFor(okGenerator1);
            driver.InsertMachineInstanceConnectedToMasterFor(crashingGenerator);
            driver.InsertMachineInstanceConnectedToMasterFor(okGenerator2);

            driver.ExecuteMachineCommand(okGenerator1.SetStereoSampleValueTo(sampleFromGenerator1));
            driver.ExecuteMachineCommand(okGenerator2.SetStereoSampleValueTo(sampleFromGenerator2));
            driver.ExecuteMachineCommand(crashingGenerator.SetStereoSampleValueTo(sampleFromCrashedGenerator));

            driver.EnableGeneratorCrashingFor(crashingGenerator);
            var samples = driver.ReadStereoSamples(1);

            driver.AssertMachineIsCrashed(crashingGenerator);
            driver.AssertLogContainsIndexOutsideArrayBoundsMessage();
            
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
            driver.AddPrecompiledGeneratorToGear(FakeNativeGeneratorInfo.Instance);
            driver.AddPrecompiledEffectToGear(FakeNativeEffectInfo.Instance);
            driver.Start();

            driver.InsertMachineInstanceFor(okGenerator);
            driver.EnableEffectCrashingFor(crashingEffect);
            driver.InsertMachineInstanceConnectedToMasterFor(crashingEffect);
            driver.Connect(okGenerator, crashingEffect);

            driver.ExecuteMachineCommand(okGenerator.SetStereoSampleValueTo(new Sample(2, 3)));
            driver.ExecuteMachineCommand(crashingEffect.SetStereoSampleMultiplier(2));

            var samples = driver.ReadStereoSamples(1);

            driver.AssertMachineIsCrashed(crashingEffect);
            driver.AssertLogContainsCannotAccessDisposedObjectMessage();
            driver.AssertLogContainsInvalidPointerMessage();
            
            samples.AssertAreEqualTo([ExpectedSampleValue.Zero()]);
        }

        [Test]
        public void DoesNotOutputSamplesThroughCrashedEffects()
        {
            using var driver = new Driver();
            var crashingEffect = FakeNativeEffectController.NewInstance("crashingEffect");
            var okGenerator = FakeNativeGeneratorController.NewInstance("okGen");
            var sampleFromGenerator = new Sample(2, 3);
            driver.AddPrecompiledGeneratorToGear(FakeNativeGeneratorInfo.Instance);
            driver.AddPrecompiledEffectToGear(FakeNativeEffectInfo.Instance);
            driver.Start();

            driver.InsertMachineInstanceFor(okGenerator);
            driver.InsertMachineInstanceConnectedToMasterFor(crashingEffect);
            driver.Connect(okGenerator, crashingEffect);

            driver.ExecuteMachineCommand(okGenerator.SetStereoSampleValueTo(sampleFromGenerator));
            driver.ExecuteMachineCommand(crashingEffect.SetStereoSampleMultiplier(2));

            driver.EnableEffectCrashingFor(crashingEffect);
            var samples = driver.ReadStereoSamples(1);

            driver.AssertMachineIsCrashed(crashingEffect);
            driver.AssertLogContainsIndexOutsideArrayBoundsMessage();

            samples.AssertAreEqualTo([ExpectedSampleValue.From(sampleFromGenerator)]);
        }
    }
}