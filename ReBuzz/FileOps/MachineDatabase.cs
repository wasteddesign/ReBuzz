using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using ReBuzz.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace ReBuzz.FileOps
{
    internal interface IMachineDatabase
    {
        event Action<string> DatabaseEvent;
        Dictionary<int, MachineDatabase.InstrumentInfo> DictLibRef { get; set; }
        void CreateDB();
        string GetLibName(int id);
        MenuItemCore IndexMenu { get; }
    }

    internal class MachineDatabase : IMachineDatabase
    {
        internal struct InstrumentInfo
        {
            public string libName;
            public bool IsLoaderInstrument;
            public string InstrumentName;
            public string InstrumentFullName;
            public string InstrumentPath;
        }
        public event Action<string> DatabaseEvent;

        private readonly ReBuzzCore buzz;
        NativeMachine.NativeMachineHost nativeMachineHost;
        NativeMachine.NativeMachineHost nativeMachineHost64;
        private string buzzPath;
        private readonly IUiDispatcher dispatcher;

        private MenuItemCore IndexMenu { get; set; }

        MenuItemCore IMachineDatabase.IndexMenu => IndexMenu;

        public Dictionary<int, InstrumentInfo> DictLibRef { get; set; }

        public MachineDatabase(ReBuzzCore buzz, string buzzPath, IUiDispatcher dispatcher)
        {
            this.buzz = buzz;
            DictLibRef = new Dictionary<int, InstrumentInfo>();
            IndexMenu = new MenuItemCore();
            this.buzzPath = buzzPath;
            this.dispatcher = dispatcher;
        }

        public void CreateDB()
        {
            string filepath = Path.Combine(buzzPath, @"gear\index.txt");
            try
            {
                nativeMachineHost = new NativeMachine.NativeMachineHost("Index", buzzPath, dispatcher);
                nativeMachineHost.InitHost(buzz, false); // 32 bit

                nativeMachineHost64 = new NativeMachine.NativeMachineHost("Index64", buzzPath, dispatcher);
                nativeMachineHost64.InitHost(buzz, true); // 64 bit

                IndexMenu = ParseMenu(filepath);
                nativeMachineHost.Dispose();
                nativeMachineHost64.Dispose();
            }
            catch (Exception ex)
            {
                buzz.DCWriteErrorLine(ex.InnerException.Message);
                MessageBox.Show(ex.Message);
            }
        }

        public string GetLibName(int id)
        {
            return DictLibRef.ContainsKey(id) ? DictLibRef[id].libName : null;
        }

        private MenuItemCore ParseMenu(string file)
        {
            Dictionary<string, int> addedMachines = new Dictionary<string, int>();

            MenuItemCore menuPos = new MenuItemCore();
            MenuItemCore rootMenu = menuPos;
            int paramID = 0;

            if (File.Exists(file))
            {
                foreach (string indexLine in File.ReadLines(file))
                {
                    string line = indexLine.Trim();
                    if (line == "/..")
                    {
                        // First order
                        menuPos.ChildrenList = menuPos.ChildrenList.OrderBy(m => m.Children.Count() == 0).ThenBy(m => m.Text).ToList();
                        // Go left
                        menuPos = FindParent(menuPos, rootMenu);
                        if (menuPos == null)
                            menuPos = rootMenu;
                    }
                    else if (line.StartsWith("/"))
                    {
                        // Create new submenu
                        MenuItemCore menuItem = new MenuItemCore() { Text = line.Remove(0, 1), IsEnabled = true };
                        menuPos.ChildrenList.Add(menuItem);
                        menuPos = menuItem;
                    }
                    else if (line.StartsWith("*"))
                    {
                        //Loader
                        var loaderName = line.Split(',');
                        // first one is loader DLL, second one is name
                        string loaderLib = loaderName.First().Substring(1).Trim();
                        string loaderDisplayName = loaderName.Last().Trim();

                        paramID = AddLoaderMenus(loaderLib, loaderDisplayName, menuPos, paramID);
                    }
                    else
                    {
                        var names = SplitAndKeep(line, new char[] { ',' });
                        foreach (string name in names)
                        {
                            string menuName = name.Trim();
                            if (menuName.Length > 0)
                            {
                                if (menuName.EndsWith(",")) // Optional
                                {
                                    var menuNameCleaned = menuName.Substring(0, menuName.Length - 1).Trim();
                                    MenuItemCore menuItem = new MenuItemCore() { Text = menuNameCleaned };

                                    if (buzz.MachineDLLs.ContainsKey(menuNameCleaned))
                                    {
                                        // Don't add twise to our DB
                                        if (!addedMachines.ContainsKey(menuNameCleaned))
                                        {
                                            addedMachines.Add(menuNameCleaned, 0);
                                            var iInfo = new InstrumentInfo();
                                            iInfo.libName = menuNameCleaned;
                                            DictLibRef[paramID] = iInfo;
                                            menuItem.ID = paramID;
                                            paramID++;
                                        }
                                        else
                                        {
                                            menuItem.ID = addedMachines[menuNameCleaned];
                                        }
                                        menuItem.IsEnabled = true;
                                    }
                                    else
                                    {
                                        menuItem.IsEnabled = false;
                                    }
                                    menuPos.ChildrenList.Add(menuItem);

                                }
                                else
                                {
                                    MenuItemCore menuItem = new MenuItemCore() { Text = menuName };
                                    if (buzz.MachineDLLs.ContainsKey(menuName))
                                    {
                                        // Don't add twise to our DB
                                        if (!addedMachines.ContainsKey(menuName))
                                        {
                                            addedMachines.Add(menuName, paramID);
                                            var iInfo = new InstrumentInfo();
                                            iInfo.libName = menuName;
                                            menuItem.ID = paramID;
                                            DictLibRef[paramID] = iInfo;
                                            paramID++;
                                        }
                                        else
                                        {
                                            menuItem.ID = addedMachines[menuName];
                                        }
                                        menuItem.IsEnabled = true;
                                    }
                                    else
                                    {
                                        menuItem.IsEnabled = false;
                                    }
                                    menuPos.ChildrenList.Add(menuItem);
                                }
                            }
                        }
                    }
                }
            }

            MenuItemCore menuItemOthers = new MenuItemCore() { Text = "Other Machines", IsEnabled = true };
            rootMenu.ChildrenList.Add(menuItemOthers);

            // Add machines not in index

            var restOfDlls = buzz.MachineDLLs.Keys.Where(n => !DictLibRef.Values.Any(i => i.libName == n));
            foreach (var libName in restOfDlls)
            {
                var iInfo = new InstrumentInfo();
                iInfo.libName = libName;
                DictLibRef[paramID] = iInfo;

                if (buzz.MachineDLLs[libName].Info.Flags.HasFlag(MachineInfoFlags.USES_INSTRUMENTS))
                {
                    paramID = AddLoaderMenus(libName, libName, menuItemOthers, paramID);
                }
                else
                {
                    MenuItemCore menuItem = new MenuItemCore() { Text = libName, ID = paramID, IsEnabled = true };
                    paramID++;
                    menuItemOthers.ChildrenList.Add(menuItem);
                }
            }
            menuItemOthers.ChildrenList = menuItemOthers.ChildrenList.OrderBy(m => m.Text).ToList();

            rootMenu.ChildrenList = rootMenu.ChildrenList.OrderBy(m => m.Children.Count() == 0).ThenBy(m => m.Text).ToList();
            return rootMenu;
        }

        private int AddLoaderMenus(string loaderLib, string loaderDisplayName, MenuItemCore menuPos, int paramID)
        {
            if (buzz.MachineDLLs.ContainsKey(loaderLib))
            {
                var machineDll = buzz.MachineDLLs[loaderLib];
                DatabaseEvent.Invoke(machineDll.Name);
                var mInfo = machineDll.Info;
                if (mInfo.Flags.HasFlag(MachineInfoFlags.USES_INSTRUMENTS))
                {
                    bool is64Bit = (machineDll as MachineDLL).Is64Bit;
                    var uiMessage = is64Bit ? nativeMachineHost64.UIMessage : nativeMachineHost.UIMessage;
                    var machine = new MachineCore(buzz.SongCore, buzzPath, dispatcher, is64Bit);
                    if (!uiMessage.UILoadLibrarySync(buzz, machine, loaderLib, machineDll.Path))
                    {
                        buzz.DCWriteErrorLine("Error loading machine: " + loaderLib);
                        return -1;
                    }
                    uiMessage.UINewMISync(machine, loaderLib);

                    DatabaseEvent.Invoke("Building DB " + machineDll.Name);
                    IntPtr dllPtr = uiMessage.UIGetDLLPtr(machine, loaderLib);
                    var instruments = uiMessage.UIGetInstrumentList(machine, dllPtr);

                    var rootMenu = new MenuItemCore() { Text = loaderDisplayName, IsEnabled = true };

                    foreach (var instr in instruments)
                    {
                        MenuItemCore menuIterator = rootMenu;
                        var splitted = instr.Split('/').Select(p => p.Trim()).ToList();

                        for (int i = 0; i < splitted.Count; i++)
                        {
                            string menuPath = splitted[i];
                            if (i == splitted.Count - 1) // Last
                            {
                                DatabaseEvent.Invoke("Building DB " + menuPath);
                                string adapterInstrumentPath = uiMessage.UIGetInstrumentPath(machine, dllPtr, instr.Trim());
                                InstrumentInfo instrumentInfo = new InstrumentInfo();

                                if (adapterInstrumentPath != null)
                                {
                                    // Add the instrument
                                    var newMenu = new MenuItemCore() { Text = menuPath, ID = paramID, IsEnabled = true };
                                    menuIterator.ChildrenList.Add(newMenu);

                                    instrumentInfo.IsLoaderInstrument = true;
                                    instrumentInfo.libName = loaderLib;
                                    instrumentInfo.InstrumentFullName = instr.Trim();
                                    instrumentInfo.InstrumentName = menuPath;
                                    instrumentInfo.InstrumentPath = adapterInstrumentPath.Trim();
                                }
                                else
                                {
                                    // Add the adapter instead
                                    var newMenu = new MenuItemCore() { Text = menuPath, ID = paramID, IsEnabled = true };
                                    menuIterator.ChildrenList.Add(newMenu);

                                    instrumentInfo.IsLoaderInstrument = true;
                                    instrumentInfo.libName = loaderLib;
                                    instrumentInfo.InstrumentFullName = instr.Trim();
                                    instrumentInfo.InstrumentName = menuPath;
                                    instrumentInfo.InstrumentPath = Path.Combine(Path.GetDirectoryName(machine.DLL.Path), menuPath);
                                }
                                DictLibRef[paramID] = instrumentInfo;
                                paramID++;
                            }
                            else if (menuIterator.Children.FirstOrDefault(m => m.Text == menuPath) == null)
                            {
                                // Create submenu
                                var newMenu = new MenuItemCore() { Text = menuPath, IsEnabled = true };
                                menuIterator.ChildrenList.Add(newMenu);
                                menuIterator = newMenu;
                            }
                            else
                            {
                                // goto subment
                                menuIterator = menuIterator.ChildrenList.FirstOrDefault(m => m.Text == menuPath);
                            }
                        }
                    }

                    if (rootMenu.ChildrenList != null)
                    {
                        rootMenu.ChildrenList = rootMenu.ChildrenList.OrderBy(m => m.Children.Count() == 0).ThenBy(m => m.Text).ToList();
                    }
                    menuPos.ChildrenList.Add(rootMenu);
                }

            }
            return paramID;
        }

        private MenuItemCore FindParent(MenuItemCore menuToFind, MenuItemCore menuLevel)
        {
            foreach (MenuItemCore item in menuLevel.Children)
            {
                if (item == menuToFind)
                {
                    return menuLevel;
                }
            }

            // If not found, go through sub menus
            foreach (MenuItemCore nextMenuLevel in menuLevel.Children)
            {
                MenuItemCore parent = FindParent(menuToFind, nextMenuLevel);
                if (parent != null)
                {
                    return parent;
                }
            }

            return null;
        }

        internal static IEnumerable<string> SplitAndKeep(string s, char[] delims)
        {
            int start = 0, index;

            while ((index = s.IndexOfAny(delims, start)) != -1)
            {
                if (index - start > 0)
                    yield return s.Substring(start, index - start + 1);
                //yield return s.Substring(index, 1);
                start = index + 1;
            }

            if (start < s.Length)
            {
                yield return s.Substring(start);
            }
        }

    }
}
