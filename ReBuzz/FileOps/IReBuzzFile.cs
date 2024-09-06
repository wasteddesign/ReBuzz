using ReBuzz.Core;
using System;
using System.Collections.Generic;
using System.IO;

namespace ReBuzz.FileOps
{
    internal interface IReBuzzFile
    {
        event Action<FileEventType, string> FileEvent;

        Dictionary<string, MemoryStream> GetSubSections();
        void Load(string bmxmlFile, float x = 0, float y = 0, bool import = false);
        void EndFileOperation();
        void Save(string filename);
        void SetSubSections(SaveSongCore ss);
    }
}
