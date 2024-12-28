using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using Microsoft.Win32;
using Sanford.Multimedia.Midi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace BuzzGUI.SequenceEditor
{
    internal class MIDIImporter
    {
        const int TimeBase = 960;	
        public static void ImportMIDISequence(ISequence s, IPattern pattern)
        {
            var fn = GetFilename();
            if (fn == null) return;
            ImportMIDISeuqence(fn, s, pattern);
        }

        static string GetFilename()
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "Standard MIDI file|*.mid";
            dlg.DefaultExt = ".mid";

            if ((bool)dlg.ShowDialog())
                return dlg.FileName;
            else
                return null;
        }

        static void ImportMIDISeuqence(string path, ISequence s, IPattern pattern)
        {
            
            //int midiTime = (e.Time / PatternEvent.TimeBase) * MidiTimeBase;

            try
            {
                var ms = new Sequence(path);

                int maxLength = 16;

                foreach (var track in ms)
                {
                    maxLength = Math.Max(track.Length / TimeBase, maxLength);
                }

                //s.Machine.CreatePattern(Path.GetFileNameWithoutExtension(path), maxLength);
                pattern.Length = maxLength;

                List<int> midiData = new List<int>();
                foreach (var track in ms)
                {   
                    foreach (var t in track.Iterator())
                    {
                        if (t.MidiMessage.MessageType == MessageType.Channel)
                        {
                            midiData.Add(t.AbsoluteTicks);
                            midiData.Add(BitConverter.ToInt32(t.MidiMessage.GetBytes(), 0));
                        }
                    }
                }
                pattern.PatternEditorMachineMIDIEvents = midiData.ToArray();

            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }
    }
}
