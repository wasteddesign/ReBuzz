using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace WDE.ModernPatternEditor
{
    public class KeyboardMapping : INotifyPropertyChanged
    {
        [XmlAttribute]
        public string Name = "";

        [XmlAttribute]
        public string Notes = "";

        [XmlAttribute]
        public string Keys = "";

        [XmlAttribute]
        public string Off = "";

        [XmlAttribute]
        public string Play = "";

        [XmlAttribute]
        public string PlayAll = "";

        [XmlIgnore]
        public string DisplayName { get { return Name; } }

        public KeyboardMapping() { }

        public int GetNoteByScancode(int scancode)
        {
            int i;
            if (!scancodeToIndex.TryGetValue(scancode, out i)) return -1;
            return noteOffsets[i % noteOffsets.Length] + 12 * (i / noteOffsets.Length);
        }

        public bool IsOffScancode(int scancode) { return scancode == StringToScancode(Off); }
        public bool IsPlayScancode(int scancode) { return scancode == StringToScancode(Play); }
        public bool IsPlayAllScancode(int scancode) { return scancode == StringToScancode(PlayAll); }

        Dictionary<int, int> _scancodeToIndex;
        Dictionary<int, int> scancodeToIndex
        {
            get
            {
                if (_scancodeToIndex == null) _scancodeToIndex = Enumerable.Range(0, Keys.Length).ToDictionary(i => CharToScancode(Keys[i]), i => i);
                return _scancodeToIndex;
            }
        }

        int[] _noteOffsets;
        int[] noteOffsets
        {
            get
            {
                if (_noteOffsets == null) _noteOffsets = Notes.Split(' ').Select(n => NoteToSemitones(n)).ToArray();
                return _noteOffsets;
            }
        }

        static int StringToScancode(string s)
        {
            if (s.Length != 1) return -1;
            return CharToScancode(s[0]);
        }

        static int CharToScancode(char ch)
        {
            switch (char.ToLowerInvariant(ch))
            {
                case 'z': return 44;
                case 'x': return 45;
                case 'c': return 46;
                case 'v': return 47;
                case 'b': return 48;
                case 'n': return 49;
                case 'm': return 50;
                case 'a': return 30;
                case 's': return 31;
                case 'd': return 32;
                case 'f': return 33;
                case 'g': return 34;
                case 'h': return 35;
                case 'j': return 36;
                case 'k': return 37;
                case 'l': return 38;
                case 'q': return 16;
                case 'w': return 17;
                case 'e': return 18;
                case 'r': return 19;
                case 't': return 20;
                case 'y': return 21;
                case 'u': return 22;
                case 'i': return 23;
                case 'o': return 24;
                case 'p': return 25;
                case '1': return 2;
                case '2': return 3;
                case '3': return 4;
                case '4': return 5;
                case '5': return 6;
                case '6': return 7;
                case '7': return 8;
                case '8': return 9;
                case '9': return 10;
                case '0': return 11;
                default: throw new ArgumentException();
            }
        }

        static int NoteToSemitones(string note)
        {
            int i;
            if (int.TryParse(note, out i)) return i;

            switch (note.ToLowerInvariant())
            {
                case "c": return 0;
                case "c#":
                case "db": return 1;
                case "d": return 2;
                case "d#":
                case "eb": return 3;
                case "e": return 4;
                case "f": return 5;
                case "f#":
                case "gb": return 6;
                case "g": return 7;
                case "g#":
                case "ab": return 8;
                case "a": return 9;
                case "a#":
                case "bb": return 10;
                case "b": return 11;
                default: throw new ArgumentException();
            }
        }



        #region INotifyPropertyChanged Members

#pragma warning disable 67
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
