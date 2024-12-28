using System;
using System.Collections.Generic;

namespace BuzzGUI.Common
{
    public static class BuzzNote
    {
        public const int Min = 1;
        public const int Max = ((16 * 9) + 12);
        public const int Off = 255;

        public const int MinOctave = 0;
        public const int MaxOctave = 9;

        public static int ToMIDINote(int x)
        {
            int n = (x & 15) - 1;
            if (n < 0 || n > 11) throw new ArgumentOutOfRangeException();

            int o = x >> 4;
            if (o < MinOctave || o > MaxOctave) throw new ArgumentOutOfRangeException();

            return o * 12 + n;
        }

        public static int FromMIDINote(int x)
        {
            if (x < 0 || x > 119) throw new ArgumentOutOfRangeException();

            return ((x / 12) << 4) + (x % 12) + 1;
        }


        static readonly string[] noteNames = { "C-", "C#", "D-", "D#", "E-", "F-", "F#", "G-", "G#", "A-", "A#", "B-" };

        public static string[] NamesWithoutOctave { get { return noteNames; } }

        public static IEnumerable<string> Names
        {
            get
            {
                for (int o = MinOctave; o <= MaxOctave; o++)
                    for (int n = 0; n < 12; n++)
                        yield return noteNames[n] + o.ToString();
            }
        }


        public static int Parse(string s)
        {
            int n;
            for (n = 0; n < 12; n++)
                if (s[0] == noteNames[n][0] && s[1] == noteNames[n][1])
                    break;

            if (n == 12) throw new ArgumentOutOfRangeException();

            int o = s[2] - '0';
            if (o < MinOctave || o > MaxOctave) throw new ArgumentOutOfRangeException();

            return o * 16 + n + 1;
        }

        public static string ToString(int x)
        {
            if (x == Off) return "off";

            int n = (x & 15) - 1;
            if (n < 0 || n > 11) throw new ArgumentOutOfRangeException();

            int o = x >> 4;
            if (o < MinOctave || o > MaxOctave) throw new ArgumentOutOfRangeException();

            return noteNames[n] + o.ToString();
        }

        public static string TryToString(int x)
        {
            if (x == Off) return "off";

            int n = (x & 15) - 1;
            if (n < 0 || n > 11) return null;

            int o = x >> 4;
            if (o < MinOctave || o > MaxOctave) return null;

            return noteNames[n] + o.ToString();
        }

    }
}
