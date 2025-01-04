using Core.Maybe;
using Microsoft.Win32;

namespace ReBuzz.Core
{
    internal interface IFileNameChoice
    {
        Maybe<string> SelectFileName();
    }

    internal class FileNameToLoadChoiceThroughOpenFileDialog : IFileNameChoice
    {
        public Maybe<string> SelectFileName()
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

    internal class FileNameToSaveChoiceThroughSaveFileDialog : IFileNameChoice //bug move and the interface as well
    {
        public Maybe<string> SelectFileName()
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Songs with waves (*.bmx)|*.bmx|Songs without waves (*.bmw)|*.bmw|ReBuzz XML (*.bmxml)|*.bmxml";
            Maybe<string> saveFileName = Maybe<string>.Nothing;
            if (saveFileDialog.ShowDialog() == true)
            {
                saveFileName = saveFileDialog.FileName.Just();
            }

            return saveFileName;
        }
    }
}