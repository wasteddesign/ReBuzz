using BuzzGUI.Common.Presets;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace BuzzGUI.Common.InterfaceExtensions
{
    public static class MachineExtensions
    {
        public static IEnumerable<IParameter> AllParameters(this IMachine machine)
        {
            foreach (var pg in machine.ParameterGroups)
                foreach (var p in pg.Parameters)
                    yield return p;
        }

        public static IEnumerable<IParameter> AllNonInputParameters(this IMachine machine)
        {
            return machine.AllParameters().Where(p => p.Group.Type != ParameterGroupType.Input);
        }

        public static IEnumerable<IParameter> AllNonInputStateParameters(this IMachine machine)
        {
            return machine.AllParameters().Where(p => p.Group.Type != ParameterGroupType.Input && p.Flags.HasFlag(ParameterFlags.State));
        }

        public static IEnumerable<Tuple<IParameter, int>> AllParametersAndTracks(this IMachine machine)
        {
            foreach (var pg in machine.ParameterGroups)
            {
                if (pg.Type == ParameterGroupType.Input)
                {
                    for (int t = 0; t < pg.TrackCount; t++)
                    {
                        yield return Tuple.Create(pg.Parameters[0], t);
                        if (machine.HasStereoInput) yield return Tuple.Create(pg.Parameters[1], t);
                    }

                }
                else
                {
                    for (int t = 0; t < pg.TrackCount; t++)
                    {
                        foreach (var p in pg.Parameters)
                            yield return Tuple.Create(p, t);
                    }
                }

            }
        }

        public static IEnumerable<Tuple<IParameter, int>> AllNonInputStateParametersAndTracks(this IMachine machine)
        {
            return machine.AllParametersAndTracks().Where(p => p.Item1.Group.Type != ParameterGroupType.Input && p.Item1.Flags.HasFlag(ParameterFlags.State));
        }

        public static bool AreParameterNamesUnique(this IMachine machine)
        {
            return AllParameters(machine).Select(p => p.Name).Distinct().Count() == AllParameters(machine).Count();
        }

        static readonly Dictionary<string, PresetDictionary> presetDictionaries = new Dictionary<string, PresetDictionary>();

        public static string GetDirectory(this IMachine machine)
        {
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Gear");

            if (machine.DLL.Info.Type == MachineType.Generator)
                path = Path.Combine(path, "Generators");
            else if (machine.DLL.Info.Type == MachineType.Effect)
                path = Path.Combine(path, "Effects");

            return path;
        }

        public static PresetDictionary GetPresetDictionary(this IMachine machine)
        {
            if (presetDictionaries.ContainsKey(machine.DLL.Name))
                return presetDictionaries[machine.DLL.Name];

            PresetDictionary pd = null;

            var xmlpath = Path.Combine(GetDirectory(machine), machine.DLL.Name + ".prs.xml");
            if (File.Exists(xmlpath))
            {
                try
                {
                    pd = PresetDictionary.Load(machine, xmlpath);
                }
                catch (Exception e)
                {
                    System.Windows.MessageBox.Show(string.Format("Couldn't load preset file '{0}' ({1})", xmlpath, e.Message));
                }
            }

            if (pd == null) pd = new PresetDictionary() { Filename = xmlpath };

            var prspath = Path.Combine(GetDirectory(machine), machine.DLL.Name + ".prs");
            if (File.Exists(prspath))
            {
                try
                {
                    var prspd = PresetDictionary.Load(machine, prspath);
                    if (prspd != null) pd.Merge(prspd);
                }
                catch (Exception e)
                {
                    System.Windows.MessageBox.Show(string.Format("Couldn't load preset file '{0}' ({1})", prspath, e.Message));
                }
            }

            presetDictionaries[machine.DLL.Name] = pd;
            return pd;
        }

        public static IEnumerable<string> GetPresetNames(this IMachine machine)
        {
            foreach (var x in GetPresetDictionary(machine)) yield return x.Key;
        }

        public static Preset GetPreset(this IMachine machine, string name)
        {
            var pd = GetPresetDictionary(machine);
            if (name == "<default>" || !pd.ContainsKey(name))
                return new Preset(machine, true, true);
            else
                return pd[name];
        }

        public static void SetPreset(this IMachine machine, string name, Preset preset)
        {
            if (name == "<default>") return;

            var pd = GetPresetDictionary(machine);
            if (preset != null)
            {
                pd[name] = preset;
            }
            else
            {
                if (pd.ContainsKey(name))
                    pd.Remove(name);
            }

            try
            {
                pd.Save();
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show(string.Format("Couldn't save preset file '{0}' ({1})", pd.Filename, e.Message));
            }

        }

        public static void ImportPresets(this IMachine machine, string filename)
        {
            var pd = GetPresetDictionary(machine);

            if (File.Exists(filename))
            {
                try
                {
                    var prspd = PresetDictionary.Load(machine, filename);
                    if (prspd != null) pd.Merge(prspd);
                }
                catch (Exception e)
                {
                    System.Windows.MessageBox.Show(string.Format("Couldn't import preset file '{0}' ({1})", filename, e.Message));
                }
            }

            try
            {
                pd.Save();
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show(string.Format("Couldn't save preset file '{0}' ({1})", pd.Filename, e.Message));
            }

        }

        public static IParameter GetParameter(this IMachine machine, string name)
        {
            return machine.AllParameters().Where(p => p.Name == name).First();
        }

        public static IAttribute GetAttribute(this IMachine machine, string name)
        {
            return machine.Attributes.Where(p => p.Name == name).First();
        }

        public static Color GetThemeColor(this IMachine machine)
        {
            if (machine.IsControlMachine)
                return machine.Graph.Buzz.ThemeColors["MV Control"];

            switch (machine.DLL.Info.Type)
            {
                case MachineType.Master: return machine.Graph.Buzz.ThemeColors["MV Master"];
                case MachineType.Generator: return machine.IsMuted ? machine.Graph.Buzz.ThemeColors["MV Generator Mute"] : machine.Graph.Buzz.ThemeColors["MV Generator"];
                case MachineType.Effect: return machine.IsMuted ? machine.Graph.Buzz.ThemeColors["MV Effect Mute"] : machine.Graph.Buzz.ThemeColors["MV Effect"];
            }

            return Colors.LightCyan;
        }

        public static string GetNewPatternName(this IMachine machine)
        {
            return Enumerable.Range(0, 1000).Select(n => n.ToString("D2")).Where(n => !machine.Patterns.Select(p => p.Name).Contains(n)).FirstOrDefault();
        }

        public static R GUIMessageExchange<R>(this IMachine machine, params object[] p) where R : struct
        {
            var request = new MemoryStream();
            var bw = new BinaryWriter(request);

            foreach (var x in p)
            {
                if (x is string)
                    bw.WriteASCIIZString(x as string);
                else
                    bw.WriteRaw(x);
            }

            var response = machine.SendGUIMessage(request.ToArray());

            if (response == null) throw new Exception("no response");

            var br = new BinaryReader(new MemoryStream(response));

            if (typeof(R) == typeof(string))
                return (R)(br.ReadASCIIZString() as object);
            else
                return br.ReadRaw<R>();
        }

        public static void GUIMessage(this IMachine machine, params object[] p)
        {
            var request = new MemoryStream();
            var bw = new BinaryWriter(request);

            foreach (var x in p)
                bw.WriteRaw(x);

            machine.SendGUIMessage(request.ToArray());
        }

        #region Attribute menu

        static IEnumerable<IMenuItem> GetAttributeValues(this IMachine machine, int index, ICommand setcmd, ICommand othercmd)
        {
            var a = machine.Attributes[index];

            var values = a.GetPresentableNumberOfValues();

            List<MenuItemVM> l = new List<MenuItemVM>();
            var g = new MenuItemVM.Group();

            foreach (var v in values)
            {
                l.Add(new MenuItemVM()
                {
                    Text = v.ToString(),
                    IsEnabled = true,
                    IsDefault = v == a.DefValue,
                    IsCheckable = true,
                    IsChecked = v == a.Value,
                    StaysOpenOnClick = true,
                    Command = setcmd,
                    CommandParameter = Tuple.Create(a, v),
                    CheckGroup = g
                });
            }

            if (values.Count() < (a.MaxValue - a.MinValue + 1))
            {
                l.Add(new MenuItemVM() { IsSeparator = true });
                l.Add(new MenuItemVM() { Text = "Other...", IsEnabled = true, Command = othercmd, CommandParameter = a });
            }

            return l;
        }


        public static IEnumerable<MenuItemVM> GetAttributeMenuItems(this IMachine machine)
        {
            var setAttributeCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = _x =>
                {
                    var x = _x as Tuple<IAttribute, int>;
                    x.Item1.Value = x.Item2;
                }
            };

            var otherCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = _x =>
                {
                    var a = (IAttribute)_x;

                    Point p = Win32Mouse.GetScreenPosition();

                    ParameterValueEditor hw = new ParameterValueEditor(a.Value, a.MinValue, a.MaxValue, true)
                    {
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Left = p.X /= WPFExtensions.PixelsPerDip,
                        Top = p.Y /= WPFExtensions.PixelsPerDip
                    };

                    new WindowInteropHelper(hw).Owner = Global.Buzz.MachineViewHWND;

                    if ((bool)hw.ShowDialog())
                    {
                        a.Value = hw.Value;
                    }

                }
            };

            var attributes = machine.Attributes;

            List<MenuItemVM> l = new List<MenuItemVM>();
            if (attributes.Count > 0)
            {
                int index = 0;
                foreach (var a in attributes)
                {
                    var v = machine.GetAttributeValues(index, setAttributeCommand, otherCommand);
                    l.Add(new MenuItemVM() { Text = a.Name, Children = v, IsEnabled = v != null });
                    index++;
                }
            }
            else
            {
                l.Add(new MenuItemVM() { Text = "(no attributes)" });
            }

            return l;
        }

        #endregion

    }
}
