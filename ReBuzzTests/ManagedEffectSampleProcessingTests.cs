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
            driver.Gear.AddDynamicEffect(EffectController.Info);
            driver.Start();

            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(controller);
            driver.MachineGraph.ExecuteMachineCommand(controller.SetStereoSampleValueToInputValue());

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
            driver.Gear.AddDynamicEffect(EffectController.Info);
            driver.Gear.AddDynamicGenerator(SynthController.Info);
            driver.Start();

            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(effectController);
            driver.MachineGraph.InsertMachineInstanceFor(synthController);
            driver.MachineGraph.Connect(synthController, effectController);

            driver.MachineGraph.ExecuteMachineCommand(effectController.SetStereoSampleValueToInputValue());
            driver.MachineGraph.ExecuteMachineCommand(synthController.SetStereoSampleValueTo(sampleToReturn));

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
            driver.Gear.AddDynamicEffect(EffectController.Info);
            driver.Gear.AddDynamicGenerator(SynthController.Info);
            driver.Start();

            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(effectController);
            driver.MachineGraph.InsertMachineInstanceFor(synthController);
            driver.MachineGraph.Connect(synthController, effectController);

            driver.MachineGraph.ExecuteMachineCommand(synthController.SetStereoSampleValueTo(sampleToReturn));
            driver.MachineGraph.ExecuteMachineCommand(effectController.SetStereoSampleValueToInputValueMultipliedBy(2));

            var samples = driver.ReadStereoSamples(1);

            samples.AssertAreEqualTo([ExpectedSampleValue.From(sampleToReturn * 2)]);
        }

        [Test]
        public void OutputsSilenceWhenDisconnectedFromMaster()
        {
            using var driver = new Driver();
            var effectController = EffectController.NewInstance();
            var synthController = SynthController.NewInstance();
            driver.Gear.AddDynamicEffect(EffectController.Info);
            driver.Gear.AddDynamicGenerator(SynthController.Info);
            driver.Start();

            driver.MachineGraph.InsertMachineInstanceConnectedToMasterFor(effectController);
            driver.MachineGraph.InsertMachineInstanceFor(synthController);
            driver.MachineGraph.Connect(synthController, effectController);
            driver.MachineGraph.DisconnectFromMaster(effectController);

            driver.MachineGraph.ExecuteMachineCommand(synthController.SetStereoSampleValueTo(new Sample(3,4)));
            driver.MachineGraph.ExecuteMachineCommand(effectController.SetStereoSampleValueToInputValue());

            var samples = driver.ReadStereoSamples(1);

            samples.AssertContainStereoSilence(1);
        }
    }
}