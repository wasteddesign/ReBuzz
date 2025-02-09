using Buzz.MachineInterface;
using ReBuzzTests.Automation;

namespace ReBuzzTests
{
    public class AddMachineTests //bug
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
            driver.AddDynamicGeneratorToGear(SynthController.Definition); //bug move the type out
            driver.Start();

            driver.InsertGeneratorInstanceFor(SynthController.NewInstance());

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
            driver.AddDynamicGeneratorToGear(SynthController.Definition);
        
            driver.Start();

            driver.InsertGeneratorInstanceFor(synth1Controller);
            driver.InsertGeneratorInstanceFor(synth2Controller);
            driver.ExecuteMachineCommand(synth1Controller.SetStereoSampleValueTo(synth1Sample));
            driver.ExecuteMachineCommand(synth2Controller.SetStereoSampleValueTo(synth2Sample));

            var samples = driver.ReadStereoSamples(1);

            samples.AssertAreEqualTo([ExpectedSampleValue.From(synth1Sample + synth2Sample)]);
        }
        
        [Test]
        public void OutputsASampleWhichIsASumFromAllMachinesTimesMasterVolume()
        {
            var masterVolume = 100;
            var synth1Sample = new Sample(5,10);
            var synth2Sample = new Sample(2,5);
            using var driver = new Driver();
            var synth1Controller = SynthController.NewInstance("s1");
            var synth2Controller = SynthController.NewInstance("s2");
            driver.AddDynamicGeneratorToGear(SynthController.Definition);
        
            driver.Start();

            driver.SetMasterVolumeTo(masterVolume);

            driver.InsertGeneratorInstanceFor(synth1Controller);
            driver.InsertGeneratorInstanceFor(synth2Controller);
            driver.ExecuteMachineCommand(synth1Controller.SetStereoSampleValueTo(synth1Sample));
            driver.ExecuteMachineCommand(synth2Controller.SetStereoSampleValueTo(synth2Sample));

            var samples = driver.ReadStereoSamples(1);

            samples.AssertAreEqualTo([ExpectedSampleValue.From(synth1Sample + synth2Sample, masterVolume)]);
        }

        //bug two reads one after another
        //bug connecting additional machine
        //bug disconnecting a machine
        //bug removing a machine
        //bug connecting through an effect
    }
}