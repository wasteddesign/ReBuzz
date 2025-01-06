using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace WDE.ModernPatternEditor.Chords
{
    public class StepMapping : INotifyPropertyChanged
    {
        [XmlAttribute]
        public string Name = "";

        [XmlAttribute]
        public string Steps = "";

        [XmlIgnore]
        public string DisplayName { get { return Name == "" ? Steps : Name; } }

        public StepMapping() { }

        public int[] GetSteps()
        {
            string[] sSteps = Steps.Split(' ');
            IList<int> buzzNotes = new List<int>();
            foreach (string sStep in sSteps)
            {
                try
                {
                    buzzNotes.Add(int.Parse(sStep));
                }
                catch (Exception e)
                {
                    System.Windows.MessageBox.Show(e.ToString(), "PEChords.xml");
                }
            }

            return buzzNotes.ToArray();
        }


        #region INotifyPropertyChanged Members

#pragma warning disable 67
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
