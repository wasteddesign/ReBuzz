using System;
using System.Windows;

namespace ReBuzz.Core
{
    public interface IUserMessages
    {
        void Error(string message, string caption, Exception exception);
    }

    public class UserMessagesViaMessageBox : IUserMessages
    {
        public void Error(string message, string caption, Exception exception)
        {
            MessageBox.Show(message, caption);
        }
    }
}