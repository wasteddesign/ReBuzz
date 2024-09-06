using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.IO;

namespace ReBuzz.Core
{
    internal class SaveSongCore : ISaveSong
    {
        public ISong Song { get; set; }

        readonly Dictionary<string, MemoryStream> streamsDictionary = new Dictionary<string, MemoryStream>();

        public Dictionary<string, MemoryStream> GetStreamsDict() { return streamsDictionary; }

        public Stream CreateSubSection(string name)
        {
            MemoryStream stream = new MemoryStream();
            streamsDictionary[name] = stream;
            return stream;
        }
    }
}
