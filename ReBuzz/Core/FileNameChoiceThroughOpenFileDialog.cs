using Microsoft.Win32;

namespace ReBuzz.Core
{
    internal interface IFileNameChoice
    {
        ChosenValue<string> SelectFileName();
    }

    internal class FileNameToLoadChoiceThroughOpenFileDialog : IFileNameChoice
    {
        public ChosenValue<string> SelectFileName()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "All songs (*.bmw, *.bmx, *bmxml)|*.bmw;*.bmx;*.bmxml|Songs with waves (*.bmx)|*.bmx|Songs without waves (*.bmw)|*.bmw|ReBuzz XML (*.bmxml)|*.bmxml";
            ChosenValue<string> fileName = ChosenValue<string>.Nothing;
            if (openFileDialog.ShowDialog() == true)
            {
                fileName = ChosenValue<string>.Just(openFileDialog.FileName);
            }

            return fileName;
        }
    }

    internal class FileNameToSaveChoiceThroughSaveFileDialog : IFileNameChoice //bug move and the interface as well
    {
        public ChosenValue<string> SelectFileName()
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Songs with waves (*.bmx)|*.bmx|Songs without waves (*.bmw)|*.bmw|ReBuzz XML (*.bmxml)|*.bmxml";
            ChosenValue<string> saveFileName = ChosenValue<string>.Nothing;
            if (saveFileDialog.ShowDialog() == true)
            {
                saveFileName = ChosenValue<string>.Just(saveFileDialog.FileName);
            }

            return saveFileName;
        }
    }
}