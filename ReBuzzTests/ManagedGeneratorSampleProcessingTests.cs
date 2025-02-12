using Buzz.MachineInterface;
using ReBuzzTests.Automation;
using ReBuzzTests.Automation.TestMachinesControllers;

namespace ReBuzzTests
{
    public class ManagedGeneratorSampleProcessingTests
    {
        [Test]
        public void OutputsSilenceWhenMachinePlays()
        {
            using var driver = new Driver();
            driver.Start();

            var samples = driver.ReadStereoSamples(1);

            samples.AssertContainStereoSilence(1);
        }

        [Test]
        public void OutputsSilenceWhenMachineOutputsSilence()
        {
            using var driver = new Driver();
            driver.AddDynamicGeneratorToGear(SynthController.Info);
            driver.Start();

            driver.InsertMachineInstanceConnectedToMasterFor(SynthController.NewInstance());

            var samples = driver.ReadStereoSamples(1);

            samples.AssertContainStereoSilence(1);
        }

        [Test]
        public void OutputsASampleWhichIsASumFromAllMachines()
        {
            var synth1Sample = new Sample(5,10);
            var synth2Sample = new Sample(2,5);
            using var driver = new Driver();
            var synth1Controller = SynthController.NewInstance("s1");
            var synth2Controller = SynthController.NewInstance("s2");
            driver.AddDynamicGeneratorToGear(SynthController.Info);
        
            driver.Start();

            driver.InsertMachineInstanceConnectedToMasterFor(synth1Controller);
            driver.InsertMachineInstanceConnectedToMasterFor(synth2Controller);
            driver.ExecuteMachineCommand(synth1Controller.SetStereoSampleValueTo(synth1Sample));
            driver.ExecuteMachineCommand(synth2Controller.SetStereoSampleValueTo(synth2Sample));

            var samples = driver.ReadStereoSamples(1);

            samples.AssertAreEqualTo([ExpectedSampleValue.From(synth1Sample + synth2Sample)]);
        }
        
        [Test]
        public void OutputsASampleWhichIsASumFromAllMachinesTimesMasterVolume()
        {
            const int masterVolume1 = 100;
            const int masterVolume2 = 50;
            var synth1Sample = new Sample(5,10);
            var synth2Sample = new Sample(2,5);
            using var driver = new Driver();
            var synth1Controller = SynthController.NewInstance("s1");
            var synth2Controller = SynthController.NewInstance("s2");
            driver.AddDynamicGeneratorToGear(SynthController.Info);
        
            driver.Start();

            driver.InsertMachineInstanceConnectedToMasterFor(synth1Controller);
            driver.InsertMachineInstanceConnectedToMasterFor(synth2Controller);
            driver.ExecuteMachineCommand(synth1Controller.SetStereoSampleValueTo(synth1Sample));
            driver.ExecuteMachineCommand(synth2Controller.SetStereoSampleValueTo(synth2Sample));
            
            driver.SetMasterVolumeTo(masterVolume1);
            var samples1 = driver.ReadStereoSamples(1);
            samples1.AssertAreEqualTo([ExpectedSampleValue.From(synth1Sample + synth2Sample, masterVolume1)]);

            driver.SetMasterVolumeTo(masterVolume2);
            var samples2 = driver.ReadStereoSamples(1);
            samples2.AssertAreEqualTo([ExpectedSampleValue.From(synth1Sample + synth2Sample, masterVolume2)]);
        }
    }
}