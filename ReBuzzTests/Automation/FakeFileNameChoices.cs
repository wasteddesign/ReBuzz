using ReBuzz.Core;

namespace ReBuzzTests.Automation
{
    public class FakeFileNameChoice : IFileNameChoice
    {
        private ChosenValue<string> fileName = ChosenValue<string>.Nothing;

        public ChosenValue<string> SelectFileName()
        {
            return fileName;
        }

        public void SetToUserCancel()
        {
            fileName = ChosenValue<string>.Nothing;
        }

        public void SetTo(string newFileName)
        {
            fileName = ChosenValue<string>.Just(newFileName);
        }
    }
}