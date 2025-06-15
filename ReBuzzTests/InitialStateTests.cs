using ReBuzzTests.Automation;

namespace ReBuzzTests
{
    public class InitialStateTests
    {
        [Test]
        public void InitializesStateAfterStart()
        {
            using var driver = new Driver();
            driver.Start();

            driver.AssertInitialStateAfterAppStart();
        }

        [Test]
        public void ReInitializesStateAfterNewFileCommand()
        {
            using var driver = new Driver();
            driver.Start();

            driver.NewFile();

            driver.AssertInitialStateAfterNewFile();
        }

        [Test]
        public void OutputsSilenceWheNoMachineInstances()
        {
            using var driver = new Driver();
            driver.Start();

            var samples = driver.ReadStereoSamples(5);

            samples.AssertAreEqualTo([
                ExpectedSampleValue.Zero(),
                ExpectedSampleValue.Zero(),
                ExpectedSampleValue.Zero(),
                ExpectedSampleValue.Zero(),
                ExpectedSampleValue.Zero(),
            ]);
        }
    }
}