using Buzz.MachineInterface;
using ReBuzzTests.Automation;
using ReBuzzTests.Automation.TestMachinesControllers;

namespace ReBuzzTests
{
    public class ManagedEffectSampleProcessingTests
    {
        [Test]
        public void OutputsSilenceWhenEffectWithNoInputIsConnectedToMaster()
        {
            using var driver = new Driver();
            var controller = EffectController.NewInstance();
            driver.AddDynamicEffectToGear(EffectController.Info);
            driver.Start();

            driver.InsertMachineInstanceConnectedToMasterFor(controller);
            driver.ExecuteMachineCommand(controller.SetStereoSampleValueToInputValue());

            var samples = driver.ReadStereoSamples(1);

            samples.AssertContainStereoSilence(1);
        }

        [Test]
        public void RoutesOutputOfInstrumentsViaEffects()
        {
            using var driver = new Driver();
            var sampleToReturn = new Sample(3,4);
            var effectController = EffectController.NewInstance();
            var synthController = SynthController.NewInstance();
            driver.AddDynamicEffectToGear(EffectController.Info);
            driver.AddDynamicGeneratorToGear(SynthController.Info);
            driver.Start();

            driver.InsertMachineInstanceConnectedToMasterFor(effectController);
            driver.InsertMachineInstanceFor(synthController);
            driver.Connect(synthController, effectController);

            driver.ExecuteMachineCommand(effectController.SetStereoSampleValueToInputValue());
            driver.ExecuteMachineCommand(synthController.SetStereoSampleValueTo(sampleToReturn));

            var samples = driver.ReadStereoSamples(1);

            samples.AssertAreEqualTo([ExpectedSampleValue.From(sampleToReturn)]);
        }

        [Test]
        public void IsAbleToAlterOutputOfInstrumentsViaEffects()
        {
            using var driver = new Driver();
            var sampleToReturn = new Sample(3,4);
            var effectController = EffectController.NewInstance();
            var synthController = SynthController.NewInstance();
            driver.AddDynamicEffectToGear(EffectController.Info);
            driver.AddDynamicGeneratorToGear(SynthController.Info);
            driver.Start();

            driver.InsertMachineInstanceConnectedToMasterFor(effectController);
            driver.InsertMachineInstanceFor(synthController);
            driver.Connect(synthController, effectController);

            driver.ExecuteMachineCommand(synthController.SetStereoSampleValueTo(sampleToReturn));
            driver.ExecuteMachineCommand(effectController.SetStereoSampleValueToInputValueMultipliedBy((float l, float r) => (l * 2, r * 2)));

            var samples = driver.ReadStereoSamples(1);

            samples.AssertAreEqualTo([ExpectedSampleValue.From(sampleToReturn * 2)]);
        }
    }
}