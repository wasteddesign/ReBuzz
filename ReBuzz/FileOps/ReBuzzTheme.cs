using BuzzGUI.Common;
using ReBuzz.Core;
using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using BuzzGUI.Interfaces;

namespace ReBuzz.FileOps
{
    [XmlRoot(ElementName = "Theme")]
    public class ReBuzzTheme
    {
        [XmlElement(ElementName = "MainWindow")]
        public ThemeMainWindow MainWindow { get; set; }

        [XmlElement(ElementName = "MachineView")]
        public ThemeMachineView MachineView { get; set; }

        [XmlElement(ElementName = "ToolBar")]
        public ThemeToolBar ToolBar { get; set; }

        [XmlElement(ElementName = "ParameterWindow")]
        public ThemeParameterWindow ParameterWindow { get; set; }

        [XmlElement(ElementName = "WavetableView")]
        public ThemeWavetableView WavetableView { get; set; }

        [XmlElement(ElementName = "SequenceEditor")]
        public ThemeSequenceEditor SequenceEditor { get; set; }

        [XmlElement(ElementName = "InfoView")]
        public ThemeInfoView InfoView { get; set; }

        public static ReBuzzTheme LoadCurrentTheme(IBuzz buzz)
        {
            string selectedTheme = buzz.SelectedTheme == "<default>" ? "Default" : buzz.SelectedTheme;
            string skinPath = Global.BuzzPath + "\\Themes\\" + selectedTheme + "\\theme.xml";
            return LoadThemeFile(skinPath);
        }
        public static ReBuzzTheme LoadThemeFile(string path)
        {
            FileStream f = null;
            if (File.Exists(path))
            {
                f = File.OpenRead(path);

                var s = new XmlSerializer(typeof(ReBuzzTheme));

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
                var t = o as ReBuzzTheme;
                return t;
            }

            return new ReBuzzTheme();
        }
    }

    public class ThemeMainWindow
    {
        [XmlAttribute]
        public string Source { get; set; }
    }

    public class ThemeMachineView
    {
        [XmlAttribute]
        public string Source { get; set; }

        [XmlElement(ElementName = "Machine")]
        public ThemeMachine[] Machine { get; set; }
    }

    public class ThemeMachine
    {
        [XmlAttribute]
        public string Type { get; set; }
        [XmlAttribute]
        public string ShadowBitmap { get; set; }
    }

    public class ThemeToolBar
    {
        [XmlAttribute]
        public string Source { get; set; }
    }

    public class ThemeParameterWindow
    {
        [XmlAttribute]
        public string Source { get; set; }
    }

    public class ThemeWavetableView
    {
        [XmlAttribute]
        public string Source { get; set; }
    }
    public class ThemeSequenceEditor
    {
        [XmlAttribute]
        public string Source { get; set; }
    }

    public class ThemeInfoView
    {
        [XmlAttribute]
        public string Source { get; set; }
    }
}
