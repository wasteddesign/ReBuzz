using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using BuzzGUI.MachineView;
using ReBuzz.ManagedMachine;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace ReBuzz.Core
{
    internal class MachineDLL : IMachineDLL
    {
        readonly ManagedMachineDLL managed;

        public IBuzz Buzz { get; set; }

        public string Name { get; set; }

        public string Path { get; set; }

        public string SHA1Hash { get; set; }

        public IntPtr ModuleHandle { get; set; }

        public MachineInfo MachineInfo { get; set; }
        public IMachineInfo Info { get => MachineInfo; }

        public ReadOnlyCollection<string> Presets { get; set; }

        public bool IsLoaded { get; set; }

        public bool IsMissing { get; set; }

        bool isCrashed = false;

        // ToDo: IsCrashed should be moved to Machine.
        public bool IsCrashed { get => isCrashed; set { isCrashed = value; PropertyChanged.Raise(this, "IsCrashed"); } }

        public bool IsOutOfProcess { get; set; }

        public bool IsManaged { get; set; }

        public ImageSource Skin { get; set; }

        public ImageSource SkinLED { get; set; }

        public Size SkinLEDSize { get; set; }

        public Point SkinLEDPosition { get; set; }

        public Color TextColor
        {
            get
            {
                if (MachineView.StaticSettings.EnableSkins && Skin != null)
                {
                    return SkinTextColor;
                }
                else
                {
                    return Global.Buzz.ThemeColors["MV Machine Text"] != null ? Global.Buzz.ThemeColors["MV Machine Text"] : Colors.White;
                }
            }
        }

        public ManagedMachineDLL ManagedDLL { get; set; }

        bool guiFactoryCached = false;
        IMachineGUIFactory guiFactory;
        private MachineGUIFactoryDecl guiFactoryDecl;

        public IMachineGUIFactory GUIFactory
        {
            get
            {
                if (guiFactoryCached)
                {
                    return guiFactory;
                }
                guiFactoryCached = true;
                string path = System.IO.Path.GetDirectoryName(Path);
                string text = ((Info.Type != MachineType.Generator) ? "Effects\\" : "Generators\\");
                // path = string.Concat(path, string.Concat(string.Concat("\\Gear\\" + text, Name), ".GUI.dll"));
                path = string.Concat(path, "\\" + Name, ".GUI.dll");
                try
                {
                    Assembly assembly = null;
                    ManagedMachineDLL machineDLL = ManagedDLL;
                    if (machineDLL != null)
                    {
                        assembly = machineDLL.Assembly;
                    }
                    else
                    {
                        if (!File.Exists(path))
                        {
                            return null;
                        }
                        assembly = Assembly.LoadFile(path);
                    }
                    Type[] exportedTypes = assembly.GetExportedTypes();
                    for (int i = 0; i < exportedTypes.Length; i++)
                    {
                        Type type = exportedTypes[i];
                        if (type.GetInterface("BuzzGUI.Interfaces.IMachineGUIFactory") != null)
                        {
                            guiFactoryDecl = type.GetCustomAttributes(inherit: false).OfType<MachineGUIFactoryDecl>().FirstOrDefault();
                            return guiFactory = Activator.CreateInstance(type) as IMachineGUIFactory;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, path);
                }
                return null;
            }
        }

        public MachineDLL()
        {
            MachineInfo = new MachineInfo();
        }

        public MachineGUIFactoryDecl GUIFactoryDecl
        {
            get
            {
                if (guiFactoryDecl == null)
                {
                    var a = GUIFactory;
                }
                return guiFactoryDecl;
            }
            set => guiFactoryDecl = value;
        }

        public Color SkinTextColor { get; internal set; }
        public bool Is64Bit { get; internal set; }

        public event PropertyChangedEventHandler PropertyChanged;
        public void Load()
        {
        }
    }
}
