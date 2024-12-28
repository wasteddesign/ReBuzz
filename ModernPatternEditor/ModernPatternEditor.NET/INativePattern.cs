using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WDE.ModernPatternEditor
{
    public interface INativePattern
    {
        void SetHostLength(int nbuzzticks);
        void AddTrack();
        void AddNote(int track, NoteEvent note);
		void DeleteNotes(int track, IEnumerable<NoteEvent> notes);
		void MoveNotes(int track, IEnumerable<NoteEvent> notes, int dx, int dy);
        void SetVelocity(int track, IEnumerable<NoteEvent> notes, int velocity);
        void SetLength(int track, IEnumerable<NoteEvent> notes, int length);
        List<NoteEvent> GetAllNotes(int track);
        NoteEvent GetAdjacentNote(int track, NoteEvent note, bool next);
        void BeginAction(string name);
        string GetPatternName();
    }
}
