using System;
using System.IO;
using System.Windows.Markup;


namespace BuzzGUI.Common
{
    public static class XamlReaderEx
    {
        public static object LoadHack(string path)
        {
            var pc = new ParserContext() { BaseUri = new Uri(Path.GetDirectoryName(path) + Path.DirectorySeparatorChar) };

            var ms = new MemoryStream(File.ReadAllBytes(path));
            return XamlReader.Load(ms, pc);
        }
    }
}
