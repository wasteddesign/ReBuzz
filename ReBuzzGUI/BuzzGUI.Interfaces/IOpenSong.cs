using System.IO;

namespace BuzzGUI.Interfaces
{
    public interface IOpenSong
    {
        ISong Song { get; }

        /// <summary>Gets a subsection of section 'BGUI' of the file being opened. Returns null if the subsection is not found in the file.</summary>
        Stream GetSubSection(string name);

    }
}
