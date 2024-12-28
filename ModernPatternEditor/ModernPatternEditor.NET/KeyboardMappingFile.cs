using BuzzGUI.Common;
using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace WDE.ModernPatternEditor
{
    public class KeyboardMappingFile
    {
        public KeyboardMapping[] Mappings;

        public KeyboardMappingFile() { }

        public void Save(Stream output)
        {
            var ws = new XmlWriterSettings() { NamespaceHandling = System.Xml.NamespaceHandling.OmitDuplicates, NewLineOnAttributes = false, Indent = true };
            var w = XmlWriter.Create(output, ws);
            var s = new XmlSerializer(GetType());
            s.Serialize(w, this);
            w.Close();
        }

        public static KeyboardMappingFile Load(Stream input)
        {
            var s = new XmlSerializer(typeof(KeyboardMappingFile));

            var r = XmlReader.Create(input);
            object o = s.Deserialize(r);
            r.Close();

            var t = o as KeyboardMappingFile;
            return t;
        }

        static KeyboardMappingFile _default;
        public static KeyboardMappingFile Default
        {
            get
            {
                if (_default == null)
                {

                    lock (typeof(KeyboardMappingFile))
                    {
                        try
                        {
                            var fn = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ReBuzz\\PEKeys.xml");
                            if (File.Exists(fn))
                            {
                                _default = Load(File.OpenRead(fn));
                                Global.Buzz.DCWriteLine(string.Format("[PE] Loaded '{0}'", fn));
                            }
                        }
                        catch (Exception e)
                        {
                            System.Windows.MessageBox.Show(e.ToString(), "User PEKeys.xml");
                        }

                        if (_default == null)
                        {
                            try
                            {
                                var fn = Path.Combine(Global.BuzzPath, "PEKeys.xml");
                                _default = Load(File.OpenRead(fn));
                                Global.Buzz.DCWriteLine(string.Format("[PE] Loaded '{0}'", fn));
                            }
                            catch (Exception e)
                            {
                                System.Windows.MessageBox.Show(e.ToString(), "PEKeys.xml");
                                return new KeyboardMappingFile();
                            }
                        }

                    }
                }

                return _default;
            }
        }

        public KeyboardMapping DefaultMapping { get { return Mappings != null ? Mappings[0] : new KeyboardMapping(); } }

    }
}
