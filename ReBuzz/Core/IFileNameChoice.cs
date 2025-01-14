namespace ReBuzz.Core
{
    internal interface IFileNameChoice
    {
        ChosenValue<string> SelectFileName();
    }
}