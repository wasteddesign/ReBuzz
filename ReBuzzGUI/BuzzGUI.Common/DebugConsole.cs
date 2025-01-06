namespace BuzzGUI.Common
{
    public static class DebugConsole
    {
        public static void WriteLine(string s)
        {
#if DEBUG
			Global.Buzz.DCWriteLine(s);
#endif
        }

        public static void WriteLine(string format, params object[] arg)
        {
#if DEBUG
			Global.Buzz.DCWriteLine(string.Format(format, arg));
#endif
        }
    }
}
