using System;

namespace Buzz.MachineInterface
{
    public struct Note
    {
        public const int Min = 1;
        public const int Max = ((16 * 9) + 12);
        public const int Off = 255;

        public const int MinOctave = 0;
        public const int MaxOctave = 9;

        public byte Value;

        public Note(int v)
        {
            Value = (byte)v;
        }

        public int ToMIDINote()
        {
            int n = (Value & 15) - 1;
            if (n < 0 || n > 11) throw new ArgumentOutOfRangeException();

            int o = Value >> 4;
            if (o < MinOctave || o > MaxOctave) throw new ArgumentOutOfRangeException();

            return o * 12 + n;
        }

        public static Note FromMIDINote(int x)
        {
            if (x < 0 || x > 119) throw new ArgumentOutOfRangeException();
            return new Note(((x / 12) << 4) + (x % 12) + 1);
        }

    }
}
