namespace Pianoroll.GUI
{
    public class PatternDimensions
    {
        public int BeatHeight = 96;
        public int BeatCount = 64 * 3;
        public int NoteCount = 12 * 10;
        public int BeatsPerBar = 3;
        public int NoteFontSize = 5;
        public int FirstMidiNote = 12;

        public double Width { get { return NoteCount * NoteWidth; } }
        public double Height { get { return BeatCount * BeatHeight; } }

        public double NoteWidth
        {
            get { return noteWidth; }
            set
            {
                noteWidth = value;

                if (noteWidth >= 24) NoteFontSize = 11;
                else if (noteWidth >= 22) NoteFontSize = 10;
                else if (noteWidth >= 20) NoteFontSize = 9;
                else if (noteWidth >= 18) NoteFontSize = 8;
                else if (noteWidth >= 16) NoteFontSize = 7;
                else if (noteWidth >= 12) NoteFontSize = 6;
                else NoteFontSize = 5;
            }
        }

        double noteWidth = 16;
    }
}
