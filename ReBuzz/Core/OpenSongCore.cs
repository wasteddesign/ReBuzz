using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.IO;

namespace ReBuzz.Core
{
    internal class OpenSongCore : IOpenSong
    {
        public ISong Song { get; set; }

        readonly Dictionary<string, MemoryStream> streamsDictionary = new Dictionary<string, MemoryStream>();

        public Stream GetSubSection(string name)
        {
            if (streamsDictionary.ContainsKey(name))
            {
                // Rewind
                var ms = streamsDictionary[name];
                ms.Position = 0;
                return ms;
            }
            else return null;
        }

        public void AddStream(string name, MemoryStream stream)
        {
            streamsDictionary[name] = stream;

        }
    }
}
