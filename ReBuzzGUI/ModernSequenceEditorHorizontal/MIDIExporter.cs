using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using Microsoft.Win32;
using Sanford.Multimedia.Midi;
using System;
using System.Collections.Generic;
using System.Windows;

namespace WDE.ModernSequenceEditorHorizontal
{
    internal class MIDIExporter
    {
        const int TimeBase = 960;   // used by IPattern.PatternEditorMachineMIDIEvents

        public static void ExportMIDI(ISong s)
        {
            var fn = GetFilename();
            if (fn == null) return;
            ExportMIDI(s.Sequences, fn);
        }

        public static void ExportMIDI(ISequence s)
        {
            var fn = GetFilename();
            if (fn == null) return;
            ExportMIDI(LinqExtensions.Return(s), fn);
        }

        static string GetFilename()
        {
            var dlg = new SaveFileDialog();
            dlg.Filter = "Standard MIDI file|*.mid";
            dlg.DefaultExt = ".mid";

            if ((bool)dlg.ShowDialog())
                return dlg.FileName;
            else
                return null;
        }


        static void ExportMIDI(IEnumerable<ISequence> seqs, string path)
        {
            try
            {
                var ms = new Sequence(4 * TimeBase);

                foreach (var s in seqs)
                    ms.Add(ExportTrack(s));

                ms.Save(path);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }

        static Track ExportTrack(ISequence s)
        {
            var mt = new Track();
            var tname = new MetaTextBuilder(MetaType.TrackName, s.Machine.Name);
            tname.Build();
            mt.Insert(0, tname.Result);

            foreach (var se in s.Events)
            {
                int setime = se.Key * TimeBase;

                if (se.Value.Type == SequenceEventType.PlayPattern)
                {
                    var p = se.Value.Pattern;
                    var me = p.PatternEditorMachineMIDIEvents;

                    for (int i = 0; i < me.Length / 2; i++)
                        mt.Insert(setime + me[2 * i + 0], new ChannelMessage(me[2 * i + 1]));
                }
            }

            return mt;
        }
    }
}
