using BuzzGUI.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace WDE.ModernPatternEditor.Chords
{
    public class ChordMapping : INotifyPropertyChanged
    {
        [XmlAttribute]
        public string Name = "";

        [XmlAttribute]
        public string Notes = "";

        [XmlIgnore]
        public string DisplayName { get { return Name; } }

        public ChordMapping() { }

        public int[] GetNotes(int transpose)
        {
            string[] sNotes = Notes.Split(' ');
            IList<int> buzzNotes = new List<int>();
            foreach (string sNote in sNotes)
            {
                try
                {
                    int bNote = BuzzNote.Parse(sNote);
                    int mNote = BuzzNote.ToMIDINote(bNote);
                    bNote = BuzzNote.FromMIDINote(mNote + transpose);

                    buzzNotes.Add(bNote);
                }
                catch (Exception e)
                {
                    System.Windows.MessageBox.Show(e.ToString(), "PEChords.xml");
                }
            }

            return buzzNotes.ToArray();
        }
        public string[] GetNoteNames()
        {
            string[] sNotes = Notes.Split(' ');
            return sNotes;
        }

        #region INotifyPropertyChanged Members

#pragma warning disable 67
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
