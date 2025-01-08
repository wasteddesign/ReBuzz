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

    }
}