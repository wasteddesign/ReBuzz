using AtmaFileSystem;
using ReBuzz.Core;

namespace ReBuzzTests.Automation
{

    public static class DialogChoices //bug this is probably redundant
    {
        public delegate ChosenValue<string> FileNameSource(); //bug move?

        internal static FileNameSource Select(AbsoluteFilePath fileName) => () => ChosenValue<string>.Just(fileName.ToString());
        internal static FileNameSource Select(string fileName) => () => ChosenValue<string>.Just(fileName);
        internal static FileNameSource Cancel() => () => ChosenValue<string>.Nothing;
        internal static FileNameSource ThrowIfDialogInvoked()
        {
            return () =>
            {
                Assert.Fail("Did not expect to invoke the dialog here.");
                return ChosenValue<string>.Nothing;
            };
        }
    }
}