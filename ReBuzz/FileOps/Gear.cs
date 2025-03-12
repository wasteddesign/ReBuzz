using System;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Serialization;
using static System.Net.Mime.MediaTypeNames;

namespace ReBuzz.FileOps
{
    [XmlRoot(ElementName = "Gear")]
    public class Gear
    {
        [XmlElement(ElementName = "Machine")]
        public Machine[] Machine { get; set; }

        public static Gear LoadGearFile(string path)
        {
            FileStream f = null;
            if (File.Exists(path))
            {
                f = File.OpenRead(path);

                var s = new XmlSerializer(typeof(Gear));

                var r = XmlReader.Create(f);
                object o = null;
                try
                {
                    o = s.Deserialize(r);
                }
                catch (Exception)
                {
                }
                r.Close();
                f.Close();
                var t = o as Gear;
                return t;
            }

            return new Gear();
        }

        public static string SerializeObject<T>(T toSerialize)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(toSerialize.GetType());

            using (StringWriter textWriter = new StringWriter())
            {
                xmlSerializer.Serialize(textWriter, toSerialize);
                return textWriter.ToString();
            }
        }

        public static bool IsTrue(string str)
        {
            if (str == "True")
                return true;

            return false;
        }

        internal bool HasSameDataFormat(string lib1, string lib2)
        {
            var m1 = Machine.FirstOrDefault(m => m.Name == lib1);
            var m2 = Machine.FirstOrDefault(m => m.Name == lib2);

            if (m1 != null && m2 != null)
            {
                if (m1.DataFormat == m2.DataFormat)
                {
                    return true;
                }
            }

            return false;
        }

        internal void Merge(Gear moreGear)
        {
            var thisMachineList = Machine.ToList();
            foreach (var moreMachine in moreGear.Machine)
            {
                var baseMachine = thisMachineList.FirstOrDefault(m => m.Name == moreMachine.Name);
                if (baseMachine != null)
                {
                    thisMachineList.Remove(baseMachine);
                }
                thisMachineList.Add(moreMachine);
            }

            Machine = thisMachineList.ToArray();
        }

        internal bool IsBlacklisted(XMLMachineDLL xmac)
        {
            var m = Machine.FirstOrDefault((m) =>
            {
                if (FileSystemName.MatchesSimpleExpression(m.Name, xmac.Name))
                {
                    if (m.Blacklist == "True")
                        return true;
                    else if (xmac.MachineInfo.Version < m.MinimumMIVersion)
                        return true;
                }
                return false;
            });
            return m != null ? true : false;
        }
    }

    public class Machine
    {
        [XmlAttribute]
        public string Name { get; set; }
        [XmlAttribute]
        public string LoadAtStartup { get; set; }
        [XmlAttribute]
        public string Multithreading { get; set; }
        [XmlAttribute]
        public string DataFormat { get; set; }
        [XmlAttribute]
        public int MinimumMIVersion { get; set; }
        [XmlAttribute]
        public string Blacklist { get; set; }
        [XmlAttribute]
        public int OversampleFactor { get; set; }
        [XmlAttribute]
        public int MIDIInputChannel { get; set; }

        public Attribute[] Attribute { get; set; }
    }

    public class Attribute
    {
        [XmlAttribute]
        public string Key { get; set; }
        [XmlAttribute]
        public string Value { get; set; }
    }
}
