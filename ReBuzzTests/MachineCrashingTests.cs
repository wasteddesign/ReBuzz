using Buzz.MachineInterface;
using ReBuzzTests.Automation;
using ReBuzzTests.Automation.TestMachines;
using ReBuzzTests.Automation.TestMachinesControllers;

namespace ReBuzzTests
{
    public class MachineCrashingTests
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

            samples.AssertAreEqualTo([
                ExpectedSampleValue.From(sampleFromGenerator1 + sampleFromGenerator2)
            ]);

            driver.AssertMachineIsCrashed(crashingGenerator);
            driver.AssertLogContainsCannotAccessDisposedObjectMessage();
            driver.AssertLogContainsInvalidPointerMessage();
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

            samples.AssertAreEqualTo([
                ExpectedSampleValue.From(sampleFromGenerator1 + sampleFromGenerator2)
            ]);

            driver.AssertMachineIsCrashed(crashingGenerator);
            driver.AssertLogContainsIndexOutsideArrayBoundsMessage();
        }
    }
}