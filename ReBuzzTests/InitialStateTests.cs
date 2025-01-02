using AtmaFileSystem;
using ReBuzzTests.Automation;
using System.Threading;

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
        public void DoesNotChangeStateWhenUserCancelsLoadingProject()
        {
            using var driver = new Driver();
            driver.Start();

            driver.SetupLoadedFileChoiceToUserCancel();

            driver.LoadFile();

            driver.AssertInitialStateAfterAppStart();
        }

        [Test]
        public void DoesNotChangeStateWhenUserPicksNonExistentFile()
        {
            var nonExistentFileName = @"sdfsjhdkfjhsdf";
            using var driver = new Driver();
            driver.Start();

            driver.SetupLoadedFileChoiceTo(nonExistentFileName);

            driver.LoadFile();

            driver.AssertInitialStateAfterNewFile();
            driver.AssertMessageReportedToUser(
                $"Error loading {nonExistentFileName}",
                $"Could not find file '{AbsoluteDirectoryPath.OfCurrentWorkingDirectory().AddFileName(nonExistentFileName)}'.");
        }

        [Test]
        public void Does___________________WhenLoadingEmptyProjectFile() //bug
        {
            var emptySongPath = AbsoluteDirectoryPath.OfThisFile().AddFileName("EmptySongBmx.bmx");
            using var driver = new Driver();
            driver.Start();

            driver.SetupLoadedFileChoiceTo(emptySongPath.ToString());

            driver.LoadFile();

            //bug assert recent file list
            driver.AssertNoErrorsReportedToUser();
            driver.AssertStateAfterLoadingAnEmptySong(emptySongPath);
            driver.AssertMessageReportedToUser(
                $"Error loading {emptySongPath}",
                $"Could not find file '{AbsoluteDirectoryPath.OfCurrentWorkingDirectory().AddFileName(emptySongPath.ToString())}'.");
        }

    }
}