using System.IO;

namespace BuzzGUI.Interfaces
{
    public interface ISaveSong
    {
        ISong Song { get; }

        /// <summary>Creates a subsection in section 'BGUI' of the file being saved. Returns null if the subsection already exists.</summary>
        Stream CreateSubSection(string name);
    }
}
