using Microsoft.Win32;

namespace ReBuzz.Core
{
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
}