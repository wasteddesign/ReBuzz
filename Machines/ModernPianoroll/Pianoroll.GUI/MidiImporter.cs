using Sanford.Multimedia.Midi;

namespace Pianoroll.GUI
{
    internal class MidiImporter
    {
        public static NoteSet Import(string fn)
        {
            Sequence s = new Sanford.Multimedia.Midi.Sequence();
            s.Load(fn);

            for (int i = 0; i < s.Count; i++)
            {
                Track t = s[i];
                int c = NoteCount(t);

                if (c > 0)
                {
                    return GetNotes(t, s.Division);
                }
            }


            return null;
        }

        static int NoteCount(Track t)
        {
            int count = 0;

            foreach (MidiEvent e in t.Iterator())
            {
                if (e.MidiMessage is ChannelMessage)
                {
                    ChannelMessage cm = (ChannelMessage)e.MidiMessage;
                    if (cm.Command == ChannelCommand.NoteOn)
                        count++;
                }
            }

            return count;
        }

        static NoteSet GetNotes(Track t, int division)
        {
            NoteSet ns = new NoteSet();

            Dictionary<int, NoteEvent> p = new Dictionary<int, NoteEvent>();

            foreach (MidiEvent e in t.Iterator())
            {
                if (e.MidiMessage is ChannelMessage)
                {
                    int time = e.AbsoluteTicks * PianorollGlobal.TicksPerBeat / division;

                    ChannelMessage cm = (ChannelMessage)e.MidiMessage;
                    if (cm.Command == ChannelCommand.NoteOn && cm.Data2 > 0)
                    {
                        p[cm.Data1] = new NoteEvent(time, 0, cm.Data1, cm.Data2);
                    }
                    else if ((cm.Command == ChannelCommand.NoteOn && cm.Data2 == 0) || cm.Command == ChannelCommand.NoteOff)
                    {
                        if (p.ContainsKey(cm.Data1))
                        {
                            NoteEvent ne = p[cm.Data1];
                            p.Remove(cm.Data1);
                            ne.Length = time - ne.Time;
                            if (ne.Length > 0)
                                ns.Add(ne);
                        }
                    }
                }
            }

            return ns;
        }

    }
}
