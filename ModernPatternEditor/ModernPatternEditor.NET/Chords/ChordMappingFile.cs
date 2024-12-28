using BuzzGUI.Common;
using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace WDE.ModernPatternEditor.Chords
{
    public class ChordMappingFile
    {
        public StepMapping[] StepMappings;
        //public ChordMapping[] Mappings;
        public ChordSet[] ChordSets;

        public ChordMappingFile() { }

        public void Save(Stream output)
        {
            var ws = new XmlWriterSettings() { NamespaceHandling = System.Xml.NamespaceHandling.OmitDuplicates, NewLineOnAttributes = false, Indent = true };
            var w = XmlWriter.Create(output, ws);
            var s = new XmlSerializer(GetType());
            s.Serialize(w, this);
            w.Close();
        }

        public static ChordMappingFile Load(Stream input)
        {
            var s = new XmlSerializer(typeof(ChordMappingFile));

            var r = XmlReader.Create(input);
            object o = s.Deserialize(r);
            r.Close();

            var t = o as ChordMappingFile;
            return t;
        }

        static ChordMappingFile _default;
        public static ChordMappingFile Default
        {
            get
            {
                if (_default == null)
                {
                    lock (typeof(ChordMappingFile))
                    {
                        try
                        {
                            var fn = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ReBuzz\\PEChords.xml");
                            if (File.Exists(fn))
                            {
                                _default = Load(File.OpenRead(fn));
                                Global.Buzz.DCWriteLine(string.Format("[PE] Loaded '{0}'", fn));
                            }
                        }
                        catch (Exception e)
                        {
                            System.Windows.MessageBox.Show(e.ToString(), "User PEChords.xml");
                        }

                        if (_default == null)
                        {
                            try
                            {
                                var fn = Path.Combine(Global.BuzzPath, "PEChords.xml");
                                _default = Load(File.OpenRead(fn));
                                Global.Buzz.DCWriteLine(string.Format("[PE] Loaded '{0}'", fn));
                            }
                            catch (Exception e)
                            {
                                System.Windows.MessageBox.Show(e.ToString(), "PEChords.xml");
                                return new ChordMappingFile();
                            }
                        }
                    }
                }
                return _default;
            }
        }

        //public ChordMapping DefaultMapping { get { return Mappings != null ? Mappings[0] : new ChordMapping(); } }
        public StepMapping DefaultStepMapping { get { return StepMappings != null ? StepMappings[0] : new StepMapping(); } }
    }
}
