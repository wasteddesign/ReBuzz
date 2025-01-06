using System.Collections.Generic;
using System.Linq;

namespace BuzzGUI.Common
{
    public static class MIDI
    {
        public const int NoteOn = 0x90;
        public const int NoteOff = 0x80;
        public const int ControlChange = 0xB0;
        public const int PitchWheel = 0xE0;

        public const int CCBankSelect = 0;
        public const int CCModWheel = 1;
        public const int CCBreathController = 2;
        public const int CCFootController = 4;
        public const int CCPortamentoTime = 5;
        public const int CCChannelVolume = 7;
        public const int CCBalance = 8;
        public const int CCPan = 10;
        public const int CCExpression = 11;
        public const int CCEffect1 = 12;
        public const int CCEffect2 = 13;
        public const int CCSustain = 64;

        public const int CMMAllSoundOff = 120;
        public const int CMMAllNotesOff = 123;

        public static int Encode(int status, int data1, int data2) { return status | (data1 << 8) | (data2 << 16); }

        public static int EncodeNoteOn(int note, int velocity) { return Encode(NoteOn, note, velocity); }
        public static int EncodeNoteOff(int note) { return Encode(NoteOff, note, 64); }

        public static int DecodeStatus(int midi) { return midi & 0xff; }
        public static int DecodeData1(int midi) { return (midi >> 8) & 0xff; }
        public static int DecodeData2(int midi) { return (midi >> 16) & 0xff; }

        public static bool IsNoteOn(int midi, int note) { return (DecodeStatus(midi) & 0xF0) == NoteOn && DecodeData1(midi) == note; }
        public static bool IsNoteOff(int midi, int note) { return (DecodeStatus(midi) & 0xF0) == NoteOff && DecodeData1(midi) == note; }

        public static bool IsNoteOn(int midi, IEnumerable<int> notes) { return (DecodeStatus(midi) & 0xF0) == NoteOn && notes.Contains(DecodeData1(midi)); }
        public static bool IsNoteOff(int midi, IEnumerable<int> notes) { return (DecodeStatus(midi) & 0xF0) == NoteOff && notes.Contains(DecodeData1(midi)); }


    }
}
