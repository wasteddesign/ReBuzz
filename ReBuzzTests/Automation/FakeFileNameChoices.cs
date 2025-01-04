using Core.Maybe;
using Microsoft.Win32;
using ReBuzz.Core;

namespace ReBuzzTests.Automation
{
    public class FakeFileNameChoice : IFileNameChoice //bug move
    {
        private Maybe<string> fileName = Maybe<string>.Nothing;

        public Maybe<string> SelectFileName()
        {
            return fileName;
        }

        public void SetToUserCancel()
        {
            fileName = Maybe<string>.Nothing;
        }

        public void SetTo(string newFileName)
        {
            fileName = newFileName.Just();
        }
    }
}