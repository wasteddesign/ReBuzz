using System.Windows;

namespace ReBuzz.Core
{
    public interface IUserMessages
    {
        void Error(string message, string caption);
    }

    public class UserMessagesViaMessageBox : IUserMessages
    {
        public void Error(string message, string caption)
        {
            MessageBox.Show(message, caption);
        }
    }
}