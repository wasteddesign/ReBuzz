using Microsoft.Win32;

namespace ReBuzz.Core
{
    internal class FileNameToSaveChoiceThroughSaveFileDialog : IFileNameChoice
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