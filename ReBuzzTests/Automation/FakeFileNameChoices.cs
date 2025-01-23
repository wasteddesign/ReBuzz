using ReBuzz.Core;

namespace ReBuzzTests.Automation
{
    public class FakeFileNameChoice : IFileNameChoice
    {
        private static readonly DialogChoices.FileNameSource userCancel = () => ChosenValue<string>.Nothing;
        private DialogChoices.FileNameSource fileNameSource = userCancel;

        public ChosenValue<string> SelectFileName()
        {
            return fileNameSource();
        }

        public void SetTo(DialogChoices.FileNameSource newFileNameSource)
        {
            fileNameSource = newFileNameSource;
        }
    }
}