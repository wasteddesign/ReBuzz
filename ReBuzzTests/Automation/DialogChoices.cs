using AtmaFileSystem;
using FluentAssertions;
using FluentAssertions.Execution;
using ReBuzz.Core;

namespace ReBuzzTests.Automation
{

    public static class DialogChoices
    {
        public delegate ChosenValue<string> FileNameSource();

        internal static FileNameSource Select(AbsoluteFilePath fileName) => () => ChosenValue<string>.Just(fileName.ToString());
        internal static FileNameSource Select(string fileName) => () => ChosenValue<string>.Just(fileName);
        internal static FileNameSource Cancel() => () => ChosenValue<string>.Nothing;
        internal static FileNameSource ThrowIfDialogInvoked()
        {
            return () =>
            {
                Execute.Assertion.FailWith("Did not expect to invoke the dialog here.");
                return ChosenValue<string>.Nothing;
            };
        }
    }
}