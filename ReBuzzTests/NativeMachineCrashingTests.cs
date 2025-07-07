using AwesomeAssertions;
using Buzz.MachineInterface;
using ReBuzzTests.Automation;
using ReBuzzTests.Automation.TestMachines;
using ReBuzzTests.Automation.TestMachinesControllers;
using System;
using System.Xaml.Schema;

namespace ReBuzzTests
{
    public class NativeMachineCrashingTests
    {
        [TestCase(MethodsToCrashOn.Constructor)]
        [TestCase(MethodsToCrashOn.Init)]
        [TestCase(MethodsToCrashOn.AttributesChanged)]
        [TestCase(MethodsToCrashOn.SetNumTracks)]
        [TestCase(MethodsToCrashOn.Tick)]
        public void DoesNotOutputSamplesFromGeneratorCrashedDuringInitialization(string methodToCrash)
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

            driver.Gear.EnableGeneratorCrashingFor(crashingGenerator, methodToCrash);
            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(okGenerator1);
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
        public void ThrowsExceptionWhenGeneratorCrashesDuringGetEnvelopeInfos()
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

            driver.Gear.EnableGeneratorCrashingFor(crashingGenerator, MethodsToCrashOn.GetEnvelopeInfos);
            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(okGenerator1);
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
        public void DoesNotOutputSamplesFromGeneratorsCrashedWhileExecutingWorkMonoToStereo()
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

            driver.Gear.EnableGeneratorCrashingFor(crashingGenerator, MethodsToCrashOn.WorkMonoToStereo);
            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(okGenerator1);
            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(crashingGenerator);
            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(okGenerator2);

            driver.MachineGraph.ExecuteMachineCommand(okGenerator1.SetStereoSampleValueTo(sampleFromGenerator1));
            driver.MachineGraph.ExecuteMachineCommand(okGenerator2.SetStereoSampleValueTo(sampleFromGenerator2));
            driver.MachineGraph.ExecuteMachineCommand(crashingGenerator.SetStereoSampleValueTo(sampleFromCrashedGenerator));

            var samples = driver.ReadStereoSamples(1);

            driver.AssertMachineIsCrashed(crashingGenerator);
            driver.ReBuzzLog.AssertLogContainsIndexOutsideArrayBoundsMessage();
            samples.AssertAreEqualTo([
                ExpectedSampleValue.From(sampleFromGenerator1 + sampleFromGenerator2)
            ]);

        }

        //bug finish adjusting these tests
        [Test]
        public void DoesNotRouteSoundThroughEffectsCrashedOnLoadingNewInstance()
        {
            using var driver = new Driver();
            var crashingEffect = FakeNativeEffectController.NewInstance("crashingEffect");
            var okGenerator = FakeNativeGeneratorController.NewInstance("okGen");
            driver.Gear.AddPrecompiledGenerator(FakeNativeGeneratorInfo.Instance);
            driver.Gear.AddPrecompiledEffect(FakeNativeEffectInfo.Instance);
            driver.Start();

            driver.Gear.EnableEffectCrashingFor(crashingEffect, MethodsToCrashOn.Constructor);
            driver.MachineGraph.InsertMachineInstanceFor(okGenerator);
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

            driver.Gear.EnableEffectCrashingFor(crashingEffect, MethodsToCrashOn.Work);
            driver.MachineGraph.InsertMachineInstanceFor(okGenerator);
            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(crashingEffect);
            driver.MachineGraph.Connect(okGenerator, crashingEffect);

            driver.MachineGraph.ExecuteMachineCommand(okGenerator.SetStereoSampleValueTo(sampleFromGenerator));
            driver.MachineGraph.ExecuteMachineCommand(crashingEffect.SetStereoSampleMultiplier(2));

            var samples = driver.ReadStereoSamples(1);

            driver.AssertMachineIsCrashed(crashingEffect);
            driver.ReBuzzLog.AssertLogContainsIndexOutsideArrayBoundsMessage();
            samples.AssertAreEqualTo([ExpectedSampleValue.From(sampleFromGenerator)]);
        }
    }
}