using BuzzGUI.Interfaces;
using ReBuzz.Core;

namespace ReBuzzTests.Automation
{
    public class ReBuzzCommandsDriverExtension(
        ReBuzzCore reBuzzCore,
        FakeFileNameChoice fileNameToSaveChoice,
        FakeFileNameChoice fileNameToLoadChoice)
    {
        public void SaveCurrentSong()
        {
            fileNameToSaveChoice.SetTo(DialogChoices.ThrowIfDialogInvoked());
            reBuzzCore.ExecuteCommand(BuzzCommand.SaveFile);
        }

        public void LoadSong(DialogChoices.FileNameSource source)
        {
            fileNameToLoadChoice.SetTo(source);
            reBuzzCore.ExecuteCommand(BuzzCommand.OpenFile);
        }

        public void SaveCurrentSongForTheFirstTime(DialogChoices.FileNameSource source)
        {
            fileNameToSaveChoice.SetTo(source);
            reBuzzCore.ExecuteCommand(BuzzCommand.SaveFile);
        }

        public void SaveCurrentSongAs(DialogChoices.FileNameSource source)
        {
            fileNameToSaveChoice.SetTo(source);
            reBuzzCore.ExecuteCommand(BuzzCommand.SaveFileAs);
        }
    }
}