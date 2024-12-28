using BuzzGUI.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace BuzzGUI.Common.Presets
{
    public class PresetDictionary : SerializableDictionary<Preset>
    {
        [XmlIgnore]
        public string Filename { get; set; }

        public PresetDictionary()
        {
        }

        public static void Init()
        {
            Task.Factory.StartNew(() =>
            {
                XmlSerializer.FromTypes(new[] { typeof(PresetDictionary), typeof(Preset), typeof(Preset.Parameter), typeof(Preset.Attribute) });
            });
        }

        public static PresetDictionary Load(IMachine machine, string filename)
        {
            if (Path.GetExtension(filename) == ".prs")
                return new PresetDictionary(machine, filename);
            else if (Path.GetExtension(filename) == ".xml")
                return LoadXML(machine, filename);

            return null;
        }

        PresetDictionary(IMachine machine, string filename)
        {
            var br = new BinaryReader(File.OpenRead(filename));

            int ver = br.ReadInt32();
            if (ver != 1) throw new Exception("wrong preset file version");

            string libname = br.ReadASCIIStringWithInt32Length();
            //if (libname.Replace("(Debug build)", "").Trim() != machine.DLL.Name)
            if (libname.Replace("(Debug build)", "").Trim() != machine.DLL.Info.Name.Replace("(Debug build)", "").Trim())
                throw new Exception("preset file is for a different machine");

            int count = br.ReadInt32();
            if (count < 0 || count > 65536) throw new Exception("invalid preset count");

            for (int i = 0; i < count; i++)
            {
                string name = br.ReadASCIIStringWithInt32Length();
                this[name] = new Preset(machine, br);
            }

        }

        static PresetDictionary LoadXML(IMachine machine, string filename)
        {
            var s = new XmlSerializer(typeof(PresetDictionary));

            var r = XmlReader.Create(filename);
            object o = s.Deserialize(r);
            r.Close();

            var pd = o as PresetDictionary;
            if (pd != null)
            {
                pd.Filename = filename;

                foreach (var x in pd)
                {
                    if (x.Value.Machine == null)
                        x.Value.Machine = machine.DLL.Name;
                }

            }
            return pd;
        }



        public void Save()
        {
            if (Filename == null) return;

            var ws = new XmlWriterSettings() { NamespaceHandling = System.Xml.NamespaceHandling.OmitDuplicates, NewLineOnAttributes = false, Indent = true };
            var w = XmlWriter.Create(Filename, ws);

            var s = new XmlSerializer(GetType());
            s.Serialize(w, this);


            w.Close();

        }

        public void Merge(PresetDictionary a)
        {
            foreach (var p in a)
            {
                if (!this.ContainsKey(p.Key))
                    this[p.Key] = p.Value;
            }
        }
    }
}
