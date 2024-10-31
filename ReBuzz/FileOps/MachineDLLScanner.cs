using BuzzGUI.Common;
using BuzzGUI.Interfaces;
//using BuzzGUI.MachineView.MDBTab.MDB;
using ReBuzz.Core;
using ReBuzz.ManagedMachine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;

namespace ReBuzz.FileOps
{
    internal class MachineDLLScanner
    {
        static readonly string dllDataFileName = "MachineDLLCache.xml";
        public MachineDLLScanner()
        {
        }

        public static Dictionary<string, MachineDLL> GetMachineDLLs(ReBuzzCore buzz)
        {
            XMLMachineDLLs xmlMachines = null;
            FileStream f = null;
            try
            {
                string xmlFilePath = GetFullFileName();
                if (File.Exists(xmlFilePath))
                {
                    f = File.OpenRead(xmlFilePath);
                    xmlMachines = LoadMachineDLLs(f);

                    if (xmlMachines != null)
                    {
                        // Check dll counts
                        string filepath = Path.Combine(Global.BuzzPath, @"Gear\Effects");
                        var effectDllFiles = GetDllList(filepath);
                        filepath = Path.Combine(Global.BuzzPath, @"Gear\Generators");
                        var generatorDllFiles = GetDllList(filepath);
                        if (Global.GeneralSettings.AlwaysRescanPlugins || xmlMachines.NumDLLsInEffectsFolder != effectDllFiles.Count || xmlMachines.NumDLLsGeneratorsFolder != generatorDllFiles.Count)
                        {
                            xmlMachines = RescanMachineDLLs(buzz);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Error loading Machine DLL Cache Data:\n\n" + e.ToString(), "Machine DLL Cache Load Error", MessageBoxButton.OK);
                if (f != null)
                    f.Close();
            }

            try
            {
                if (xmlMachines == null)
                {
                    xmlMachines = RescanMachineDLLs(buzz);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("RescanMachineDLLs error:\n\n" + e.ToString(), "Machine DLL Cache Load Error", MessageBoxButton.OK);
            }


            Dictionary<string, MachineDLL> md = new Dictionary<string, MachineDLL>();
            try
            {
                AddMachineDllsToDictionary(xmlMachines.EffectMachineDLLs, md);
                AddMachineDllsToDictionary(xmlMachines.GeneratorMachineDLLs, md);
            }
            catch (Exception)
            {
                //MessageBox.Show("AddMachineDllsToDictionary error:\n\n" + e.ToString(), "Machine DLL Cache Load Error", MessageBoxButton.OK);
                xmlMachines = RescanMachineDLLs(buzz);
                md.Clear();
                AddMachineDllsToDictionary(xmlMachines.EffectMachineDLLs, md);
                AddMachineDllsToDictionary(xmlMachines.GeneratorMachineDLLs, md);
            }
            return md;
        }

        public static void AddMachineDllsToDictionary(XMLMachineDLL[] xMLMachineDLLs, Dictionary<string, MachineDLL> md)
        {
            foreach (var xmac in xMLMachineDLLs)
            {
                MachineDLL mDll = new MachineDLL();

                mDll.Name = xmac.Name;
                mDll.Path = xmac.Path;
                mDll.IsManaged = xmac.IsManaged;
                mDll.Is64Bit = xmac.Is64Bit;
                mDll.IsOutOfProcess = (IntPtr.Size == 4 && xmac.Is64Bit) || (IntPtr.Size == 8 && !xmac.Is64Bit);

                using (var fs = File.OpenRead(mDll.Path))
                {
                    mDll.SHA1Hash = string.Join("", new SHA1CryptoServiceProvider().ComputeHash(fs).Select(b => b.ToString("X2")));
                }

                var mi = new MachineInfo();
                var xmi = xmac.MachineInfo;

                mi.Type = xmi.Type;
                mi.Version = xmi.Version;
                mi.InternalVersion = xmi.InternalVersion;
                mi.Flags = xmi.Flags;
                mi.MinTracks = xmi.MinTracks;
                mi.MaxTracks = xmi.MaxTracks;
                mi.Name = xmi.Name;
                mi.ShortName = xmi.ShortName;
                mi.Author = xmi.Author;
                mDll.MachineInfo = mi;

                if (!(mi.Flags.HasFlag(MachineInfoFlags.PATTERN_EDITOR) && !mDll.IsManaged))
                {
                    // 64bit machines overwrite 32bits...
                    md[mDll.Name] = mDll;
                }
            }
        }

        public static XMLMachineDLLs LoadMachineDLLs(Stream input)
        {
            if (input == null)
            {
                return null;
            }
            var s = new XmlSerializer(typeof(XMLMachineDLLs));

            var r = XmlReader.Create(input);
            object o = null;
            try
            {
                o = s.Deserialize(r);
            }
            catch (Exception)
            {
                //MessageBox.Show("Error loading Machine DLL Cache Data:\n\n" + e.ToString(), "Machine DLL Cache Load Error", MessageBoxButton.OK);
                Global.Buzz.DCWriteLine("Error loading Machine DLL Cache Data. Rebuilding cache...");
            }
            r.Close();
            input.Close();
            var t = o as XMLMachineDLLs;
            return t;
        }

        public static string GetFullFileName()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ReBuzzCore.AppDataPath);
            return Path.Combine(dir, dllDataFileName);
        }

        public static string GetFilePath()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ReBuzzCore.AppDataPath);
            return dir;
        }

        public static XMLMachineDLLs RescanMachineDLLs(ReBuzzCore buzz)
        {
            buzz.DCWriteLine("RescanMachineDLLs");
            var xFileName = GetFullFileName();
            var f = File.Create(xFileName);

            NativeMachine.NativeMachineHost nativeMachineHost = new NativeMachine.NativeMachineHost("ScanDLLs");
            nativeMachineHost.InitHost(buzz, false);

            NativeMachine.NativeMachineHost nativeMachineHost64 = new NativeMachine.NativeMachineHost("ScanDLLs64");
            nativeMachineHost64.InitHost(buzz, true);

            string filepath = Path.Combine(Global.BuzzPath, @"Gear\Effects");
            var effectDllFiles = GetDllList(filepath);
            filepath = Path.Combine(Global.BuzzPath, @"Gear\Generators");
            var generatorDllFiles = GetDllList(filepath);

            XMLMachineDLLs machineDLLs = new XMLMachineDLLs();
            machineDLLs.NumDLLsInEffectsFolder = effectDllFiles.Count;
            machineDLLs.NumDLLsGeneratorsFolder = generatorDllFiles.Count;

            List<XMLMachineDLL> effectsList = ValidateDlls(buzz, nativeMachineHost, nativeMachineHost64, effectDllFiles);
            List<XMLMachineDLL> generatorsList = ValidateDlls(buzz, nativeMachineHost, nativeMachineHost64, generatorDllFiles);

            machineDLLs.EffectMachineDLLs = effectsList.ToArray();
            machineDLLs.GeneratorMachineDLLs = generatorsList.ToArray();

            Save(f, machineDLLs);

            nativeMachineHost.Dispose();
            nativeMachineHost64.Dispose();

            return machineDLLs;
        }

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

        private static List<XMLMachineDLL> ValidateDlls(ReBuzzCore buzz, NativeMachine.NativeMachineHost nativeMachineHost, NativeMachine.NativeMachineHost nativeMachineHost64, List<FileInfo> machineDllFiles)
        {
            List<XMLMachineDLL> machineDLLs = new List<XMLMachineDLL>();

            var uiMessage = nativeMachineHost.UIMessage;
            var uiMessage64 = nativeMachineHost64.UIMessage;

            foreach (FileInfo file in machineDllFiles)
            {
                XMLMachineDLL mDll = null;
                try
                {
                    mDll = ValidateDll(buzz, file.Name, file.FullName, nativeMachineHost, nativeMachineHost64);
                    if (mDll != null)
                    {
                        machineDLLs.Add(mDll);
                    }
                }
                catch (Exception e)
                {
                    Global.Buzz.DCWriteLine(e.ToString());
                }
            }
            return machineDLLs;
        }

        public static XMLMachineDLL ValidateDll(ReBuzzCore buzz, string libName, string path, NativeMachine.NativeMachineHost nativeMachineHost, NativeMachine.NativeMachineHost nativeMachineHost64)
        {
            var uiMessage = nativeMachineHost.UIMessage;
            var uiMessage64 = nativeMachineHost64.UIMessage;

            if (libName.EndsWith("GUI.dll"))
                return null;

            // Managed machines
            if (libName.EndsWith("NET.dll"))
            {
                libName = libName.Remove(libName.Length - 8);

                ManagedMachineDLL managedMachineDLL = new ManagedMachineDLL();
                managedMachineDLL.LoadManagedMachine(path);

                XMLMachineDLL mDll = new XMLMachineDLL();
                mDll.Name = libName;
                mDll.Path = path;
                mDll.Is64Bit = true;

                var module = managedMachineDLL.Assembly.Modules.FirstOrDefault();
                if (module != null)
                {
                    managedMachineDLL.Assembly.Modules.First().GetPEKind(out var peKind, out var peType);
                    mDll.Is64Bit = !(peKind == PortableExecutableKinds.Required32Bit);
                }
                mDll.IsManaged = true;

                XMLMachineInfo machineInfo = new XMLMachineInfo();
                var mi = managedMachineDLL.MachineInfo;

                machineInfo.Type = mi.Type;
                machineInfo.Version = mi.Version;
                machineInfo.InternalVersion = mi.InternalVersion;
                machineInfo.Flags = mi.Flags;
                machineInfo.MinTracks = mi.MinTracks;
                machineInfo.MaxTracks = mi.MaxTracks;
                machineInfo.Name = mi.Name;
                machineInfo.ShortName = mi.ShortName;
                machineInfo.Author = mi.Author;
                mDll.MachineInfo = machineInfo;

                return mDll;
            }
            // 64 bit native machines
            else if (libName.EndsWith("x64.dll"))
            {
                libName = libName.Remove(libName.Length - 8);
                var machine = new MachineCore(buzz.SongCore);
                machine.MachineDLL.Is64Bit = true;
                if (uiMessage64.UILoadLibrarySync(buzz, machine, libName, path))
                {
                    XMLMachineDLL mDll = new XMLMachineDLL();
                    mDll.Name = libName;
                    mDll.Path = path;
                    mDll.Is64Bit = true;

                    XMLMachineInfo machineInfo = new XMLMachineInfo();
                    var mi = machine.DLL.Info;

                    machineInfo.Type = mi.Type;
                    machineInfo.Version = mi.Version;
                    machineInfo.InternalVersion = mi.InternalVersion;
                    machineInfo.Flags = mi.Flags;
                    machineInfo.MinTracks = mi.MinTracks;
                    machineInfo.MaxTracks = mi.MaxTracks;
                    machineInfo.Name = mi.Name;
                    machineInfo.ShortName = mi.ShortName;
                    machineInfo.Author = mi.Author;
                    mDll.MachineInfo = machineInfo;

                    //if (!(machineInfo.Flags.HasFlag(MachineInfoFlags.PATTERN_EDITOR) && !mDll.IsManaged))
                    return mDll;
                }
            }
            // 32 bit
            else
            {
                libName = libName.Remove(libName.Length - 4);
                var machine = new MachineCore(buzz.SongCore);

                if (uiMessage.UILoadLibrarySync(buzz, machine, libName, path))
                {
                    XMLMachineDLL mDll = new XMLMachineDLL();
                    mDll.Name = libName;
                    mDll.Path = path;
                    mDll.IsOutOfProcess = true;

                    XMLMachineInfo machineInfo = new XMLMachineInfo();
                    var mi = machine.DLL.Info;

                    machineInfo.Type = mi.Type;
                    machineInfo.Version = mi.Version;
                    machineInfo.InternalVersion = mi.InternalVersion;
                    machineInfo.Flags = mi.Flags;
                    machineInfo.MinTracks = mi.MinTracks;
                    machineInfo.MaxTracks = mi.MaxTracks;
                    machineInfo.Name = mi.Name;
                    machineInfo.ShortName = mi.ShortName;
                    machineInfo.Author = mi.Author;
                    mDll.MachineInfo = machineInfo;

                    //if (!(machineInfo.Flags.HasFlag(MachineInfoFlags.PATTERN_EDITOR) && !mDll.IsManaged))
                    return mDll;
                }
            }
            return null;
        }

        public static XMLMachineDLL ValidateDll(ReBuzzCore buzz, string libName, string path)
        {
            NativeMachine.NativeMachineHost nativeMachineHost = new NativeMachine.NativeMachineHost("ScanDLLs");
            nativeMachineHost.InitHost(buzz, false);

            NativeMachine.NativeMachineHost nativeMachineHost64 = new NativeMachine.NativeMachineHost("ScanDLLs64");
            nativeMachineHost64.InitHost(buzz, true);

            return ValidateDll(buzz, libName, path, nativeMachineHost, nativeMachineHost64);
        }

        public static List<FileInfo> GetDllList(string filepath)
        {
            List<string> list = new List<string>();

            DirectoryInfo d = new DirectoryInfo(filepath);

            return d.GetFiles().Where(f => f.Name.EndsWith(".dll")).ToList();
        }
    }

    // XML import/export structures
    [XmlType(TypeName = "MachineDLLs")]
    public class XMLMachineDLLs
    {
        public int NumDLLsInEffectsFolder { get; set; }
        public int NumDLLsGeneratorsFolder { get; set; }

        public XMLMachineDLL[] EffectMachineDLLs { get; set; }
        public XMLMachineDLL[] GeneratorMachineDLLs { get; set; }

    }

    [XmlType(TypeName = "MachineDLL")]
    public class XMLMachineDLL
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public XMLMachineInfo MachineInfo { get; set; }
        public bool IsOutOfProcess { get; set; }
        public bool IsManaged { get; set; }
        public bool Is64Bit { get; set; }
    }

    [XmlType(TypeName = "MachineInfo")]
    public class XMLMachineInfo
    {
        [XmlAttribute]
        public MachineType Type { get; set; }
        [XmlAttribute]
        public int Version { get; set; }
        [XmlAttribute]
        public int InternalVersion { get; set; }
        [XmlAttribute]
        public MachineInfoFlags Flags { get; set; }
        [XmlAttribute]
        public int MinTracks { get; set; }
        [XmlAttribute]
        public int MaxTracks { get; set; }
        [XmlAttribute]
        public string Name { get; set; }
        [XmlAttribute]
        public string ShortName { get; set; }
        [XmlAttribute]
        public string Author { get; set; }
    }

}
