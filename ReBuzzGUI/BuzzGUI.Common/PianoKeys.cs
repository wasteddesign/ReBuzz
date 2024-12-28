using System;
using System.Windows.Input;

namespace BuzzGUI.Common
{
    public static class PianoKeys
    {
        static readonly int[] NoteScanCodes = { 44, 31, 45, 32, 46, 47, 34, 48, 35, 49, 36, 50, 16, 3, 17, 4, 18, 19, 6, 20, 7, 21, 8, 22, 23, 10, 24, 11, 25 };

        public static int GetPianoKeyIndex(KeyEventArgs e)
        {
            var scancode = Win32.GetScanCode(e);
            return Array.IndexOf(NoteScanCodes, scancode);
        }

    }
}
