using System.Windows;

namespace BuzzGUI.Common
{
    public static class ClipboardEx
    {
        public static void SetText(string s)
        {
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    Clipboard.SetData(DataFormats.Text, s);
                    break;
                }
                catch { }
                System.Threading.Thread.Sleep(100);
            }
        }

        public static string GetText()
        {
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    object s = Clipboard.GetData(DataFormats.Text);
                    if (s == null) return null;
                    return s as string;
                }
                catch { }
                System.Threading.Thread.Sleep(100);
            }

            return null;
        }

    }
}
