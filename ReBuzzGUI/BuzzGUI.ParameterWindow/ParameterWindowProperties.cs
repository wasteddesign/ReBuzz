using BuzzGUI.Common.Templates;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace BuzzGUI.ParameterWindow
{
    public class ParameterWindowProperties
    {
        public List<Machine> Machines;

        [XmlIgnore]
        private static ParameterWindowProperties? properties;

        [XmlIgnore]
        private static readonly string propertiesFileName = "ParameterWindowProperties.xml";

        public static bool IsParameterVisible(IParameter param)
        {
            if (properties == null)
                Load();

            var mac = param.Group.Machine;

            var machine = properties.Machines.FirstOrDefault(m => m.Dll == mac.DLL.Name);
            if (machine == null)
                return true;

            var group = machine.ParameterGroups.FirstOrDefault(g => g.Type == param.Group.Type);
            if (group == null)
                return true;

            var parameter = group.Parameters.FirstOrDefault(p => p.Name == param.Name);
            if (parameter == null)
                return true;

            return parameter.IsVisible;
        }

        public static void SetParameterHidden(IParameter param, bool visible)
        {
            if (param.Group.Type == ParameterGroupType.Input)
                return;
            if (properties == null)
                Load();

            var mac = param.Group.Machine;

            var machine = properties.Machines.FirstOrDefault(m => m.Dll == mac.DLL.Name);
            if (machine == null)
            {
                properties.Machines.Add(machine = new Machine() { Dll = mac.DLL.Name, ParameterGroups = new List<ParameterGroup>() });
            }

            var group = machine.ParameterGroups.FirstOrDefault(g => g.Type == param.Group.Type);
            if (group == null)
            {
                machine.ParameterGroups.Add(group = new ParameterGroup() { Type = param.Group.Type, Parameters = new List<Parameter>() });
            }

            var parameter = group.Parameters.FirstOrDefault(p => p.Name == param.Name);
            if (parameter == null && !visible)
            {
                group.Parameters.Add(parameter = new Parameter() { Name = param.Name, IsVisible = visible });
            }
            else if (parameter != null && visible)
            {
                group.Parameters.Remove(parameter);
            }

            Save();
        }

        private static string GetFullFileName()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ReBuzz");

            // Check if the directory exists
            if (!Directory.Exists(dir))
            {
                // Create the directory
                Directory.CreateDirectory(dir);
            }

            return Path.Combine(dir, propertiesFileName);
        }

        private void Save(Stream output)
        {
            var ws = new XmlWriterSettings() { NamespaceHandling = System.Xml.NamespaceHandling.OmitDuplicates, NewLineOnAttributes = false, Indent = true };
            var w = XmlWriter.Create(output, ws);
            var s = new XmlSerializer(GetType());
            s.Serialize(w, this);
            w.Close();
        }

        private static void Save()
        {
            var path = GetFullFileName();
            
            using (var fs = File.Create(path))
            {
                properties.Save(fs);
            }
        }

        private static ParameterWindowProperties Load(Stream input)
        {
            var s = new XmlSerializer(typeof(ParameterWindowProperties));

            var r = XmlReader.Create(input);
            object o = s.Deserialize(r);
            r.Close();

            var t = o as ParameterWindowProperties;
            return t;
        }

        private static ParameterWindowProperties Load(string path)
        {
            ParameterWindowProperties? p = null;
            try
            {
                using (var fs = File.OpenRead(path))
                {
                    if (System.IO.Path.GetExtension(path).Equals(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        p = Load(fs);
                    }
                }
            }
            catch
            {
                p = new ParameterWindowProperties() { Machines = new List<Machine>() };
            }

            return p;
        }

        private static void Load()
        {
            properties = Load(GetFullFileName());
            if (properties == null)
            {
                properties = new ParameterWindowProperties() { Machines = new List<Machine>() };
            }
        }   

        public class Machine
        {
            [XmlAttribute]
            public string Dll;
            public List<ParameterGroup> ParameterGroups;
        }

        public class ParameterGroup
        {
            [XmlAttribute]
            public ParameterGroupType Type;

            public List<Parameter> Parameters;
        }

        public class Parameter
        {
            [XmlAttribute]
            public string Name;
            [XmlAttribute]
            public bool IsVisible;
        }
    }
}
