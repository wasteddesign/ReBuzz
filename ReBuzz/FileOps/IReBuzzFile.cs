using ReBuzz.Core;
using ReBuzz.Core.Actions.GraphActions;
using System;
using System.Collections.Generic;
using System.IO;

namespace ReBuzz.FileOps
{
    internal interface IReBuzzFile
    {
        event Action<FileEventType, string, object> FileEvent;

        Dictionary<string, MemoryStream> GetSubSections();
        void Load(string bmxmlFile, float x = 0, float y = 0, ImportSongAction importAction = null);
        void EndFileOperation(bool import);
        void Save(string filename);
        void SetSubSections(SaveSongCore ss);
    }
}
