using BuzzGUI.Common.Templates;
using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;
using BuzzGUI.Common;
using WDE.ModernPatternEditor.Actions;

namespace WDE.ModernPatternEditor.MPEStructures
{
    internal class PatternXMLImportExport
    {
        public static void Save(Stream output, object obj)
        {
            var ws = new XmlWriterSettings() { NamespaceHandling = System.Xml.NamespaceHandling.OmitDuplicates, NewLineOnAttributes = false, Indent = true };
            var w = XmlWriter.Create(output, ws);
            try
            {
                var s = new XmlSerializer(obj.GetType());
                s.Serialize(w, obj);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString(), "Save Error", MessageBoxButton.OK);
            }

            w.Close();
            output.Close();
        }

        public static XMLPatterns LoadPatterns(Stream input)
        {
            var s = new XmlSerializer(typeof(XMLPatterns));

            var r = XmlReader.Create(input);
            object o = null;
            try
            {
                o = s.Deserialize(r);
            }
            catch (Exception e)
            {
                MessageBox.Show("Error loading all patterns:\n\n" + e.ToString(), "Multi Pattern Load Error", MessageBoxButton.OK);
            }
            r.Close();
            input.Close();
            var t = o as XMLPatterns;
            return t;
        }

        public static void ExportPattern(MPEPattern pattern)
        {
            Microsoft.Win32.SaveFileDialog saveFileDlg = new Microsoft.Win32.SaveFileDialog();

            saveFileDlg.DefaultExt = ".xml";
            saveFileDlg.Filter = "XML documents (.xml)|*.xml";

            // saveFileDlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            Nullable<bool> result = saveFileDlg.ShowDialog();

            if (result == true)
            {
                XMLPatterns patterns = new XMLPatterns();
                XMLPattern xPattern = GetExportPattern(pattern);
                patterns.Machine = pattern.Pattern.Machine.Name;
                List<XMLPattern> patList = new List<XMLPattern>();
                patList.Add(xPattern);
                patterns.Patterns = patList.ToArray();
                Save(File.Create(saveFileDlg.FileName), patterns);
            }
        }

        public static void ExportPatterns(MPEPatternDatabase patternDB)
        {
            Microsoft.Win32.SaveFileDialog saveFileDlg = new Microsoft.Win32.SaveFileDialog();

            saveFileDlg.DefaultExt = ".xml";
            saveFileDlg.Filter = "XML documents (.xml)|*.xml";

            // saveFileDlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            Nullable<bool> result = saveFileDlg.ShowDialog();

            if (result == true)
            {
                XMLPatterns patterns = new XMLPatterns();
                patterns.Machine = patternDB.Machine.Name;
                List<XMLPattern> patList = new List<XMLPattern>();
                foreach (var pattern in patternDB.GetPatterns())
                {
                    XMLPattern xPattern = GetExportPattern(pattern);
                    patList.Add(xPattern);
                }
                patterns.Patterns = patList.ToArray();
                Save(File.Create(saveFileDlg.FileName), patterns);
            }
        }

        public static void ImportPatterns(PatternEditor editor)
        {
            // Create OpenFileDialog
            Microsoft.Win32.OpenFileDialog openFileDlg = new Microsoft.Win32.OpenFileDialog();

            openFileDlg.DefaultExt = ".xml";
            openFileDlg.Filter = "XML documents (.xml)|*.xml";
            openFileDlg.Multiselect = true;

            // openFileDlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            Nullable<bool> result = openFileDlg.ShowDialog();

            if (result == true)
            {
                foreach (var fi in openFileDlg.FileNames)
                {
                    using (new ActionGroup(editor.EditContext.ActionStack))
                    {
                        var xPatterns = LoadPatterns(File.OpenRead(fi));
                        if (xPatterns != null && xPatterns.Machine == editor.SelectedMachine.Machine.Name)
                        {
                            foreach (var xPattern in xPatterns.Patterns)
                            {
                                editor.DoAction(new ImportPatternAction(editor, xPattern));
                            }
                        }
                    }
                }
            }
        }
        
        public static XMLPattern GetExportPattern(MPEPattern pattern)
        {
            XMLPattern xPattern = new XMLPattern();
            xPattern.Machine = pattern.Pattern.Machine.Name;
            xPattern.Pattern = pattern.PatternName;
            xPattern.RowsPerBeat = pattern.RowsPerBeat;
            xPattern.LenghtInBeats = pattern.Pattern.Length / PatternControl.BUZZ_TICKS_PER_BEAT;

            List<XMLColumn> columns = new List<XMLColumn>();
            foreach(var mpeColumn in pattern.MPEPatternColumns)
            {
                var column = new XMLColumn();
                column.Track = mpeColumn.ParamTrack;
                column.Parameter = mpeColumn.Parameter.Name;
                column.Machine = mpeColumn.Machine.Name;
                column.Events = mpeColumn.GetEvents(0, int.MaxValue).ToArray();
                column.Beats = mpeColumn.BeatRowsList.ToArray();
                columns.Add(column);
            }

            xPattern.Columns = columns.ToArray();
            return xPattern;
        }
    }


    public class XMLPatterns
    {
        [XmlAttribute]
        public string Machine;
        public XMLPattern[] Patterns { get; set; }
    }

    public class XMLPattern
    {
        [XmlAttribute]
        public string Machine;
        [XmlAttribute]
        public string Pattern;
        [XmlAttribute]
        public int RowsPerBeat;
        [XmlAttribute]
        public int LenghtInBeats;
        public XMLColumn[] Columns { get; set; }
    }

    public class XMLColumn
    {
        [XmlAttribute]
        public string Machine;
        [XmlAttribute]
        public string Parameter;
        [XmlAttribute]
        public int Track;
        public PatternEvent[] Events;
        public int[] Beats;
    }
}
