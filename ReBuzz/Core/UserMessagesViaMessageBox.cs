using System.Windows;

namespace ReBuzz.Core
{
    public interface IUserMessages
    {
        void Show(string message, string caption);
    }

    public class UserMessagesViaMessageBox : IUserMessages
    {
        public void Show(string message, string caption)
        {
            MessageBox.Show(message, caption);
        }
    }
}