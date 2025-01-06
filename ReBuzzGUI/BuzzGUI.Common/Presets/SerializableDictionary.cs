using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace BuzzGUI.Common.Presets
{
    [XmlRoot("SerializableDictionary")]
    public class SerializableDictionary<V> : SortedDictionary<string, V>, IXmlSerializable where V : class
    {
        internal Boolean _ReadOnly = false;
        public Boolean ReadOnly
        {
            get
            {
                return this._ReadOnly;
            }

            set
            {
                this.CheckReadOnly();
                this._ReadOnly = value;
            }
        }

        public new V this[string key]
        {
            get
            {
                V value;

                return this.TryGetValue(key, out value) ? value : null;
            }

            set
            {
                this.CheckReadOnly();

                if (value != null)
                {
                    base[key] = value;
                }
                else
                {
                    this.Remove(key);
                }
            }
        }

        internal void CheckReadOnly()
        {
            if (this._ReadOnly)
            {
                throw new Exception("Collection is read only");
            }
        }

        public new void Clear()
        {
            this.CheckReadOnly();

            base.Clear();
        }

        public new void Add(string key, V value)
        {
            this.CheckReadOnly();

            base.Add(key, value);
        }

        public new void Remove(string key)
        {
            this.CheckReadOnly();

            base.Remove(key);
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            Boolean wasEmpty = reader.IsEmptyElement;

            reader.Read();

            if (wasEmpty)
            {
                return;
            }

            while (reader.NodeType != XmlNodeType.EndElement && reader.NodeType != XmlNodeType.None)
            {
                if (reader.Name == "Item")
                {
                    string key = reader.GetAttribute("Key");
                    //Type type = Type.GetType(reader.GetAttribute("TypeName"));
                    var type = typeof(V);

                    reader.Read();
                    if (type != null)
                    {
                        this.Add(key, new XmlSerializer(type).Deserialize(reader) as V);
                    }
                    else
                    {
                        reader.Skip();
                    }
                    reader.ReadEndElement();

                    reader.MoveToContent();
                }
                else
                {
                    reader.Skip();
                }
            }

            reader.ReadEndElement();
        }

        public void WriteXml(XmlWriter writer)
        {
            foreach (KeyValuePair<string, V> item in this)
            {
                writer.WriteStartElement("Item");
                writer.WriteAttributeString("Key", item.Key);
                //writer.WriteAttributeString("TypeName", item.Value.GetType().AssemblyQualifiedName);

                var ns = new XmlSerializerNamespaces();
                ns.Add("", "");
                new XmlSerializer(item.Value.GetType()).Serialize(writer, item.Value, ns);

                writer.WriteEndElement();
            }
        }

    }
}
