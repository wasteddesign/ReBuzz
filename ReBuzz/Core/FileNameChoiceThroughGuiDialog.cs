using Core.Maybe;
using Microsoft.Win32;

namespace ReBuzz.Core
{
    internal interface IFileNameChoice
    {
        Maybe<string> SelectFileNameToLoad();
    }

    internal class FileNameChoiceThroughGuiDialog : IFileNameChoice
    {
        public Maybe<string> SelectFileNameToLoad()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "All songs (*.bmw, *.bmx, *bmxml)|*.bmw;*.bmx;*.bmxml|Songs with waves (*.bmx)|*.bmx|Songs without waves (*.bmw)|*.bmw|ReBuzz XML (*.bmxml)|*.bmxml";
            Maybe<string> fileName = Maybe<string>.Nothing;
            if (openFileDialog.ShowDialog() == true)
            {
                fileName = openFileDialog.FileName.Just();
            }

            return fileName;
        }
    }
}