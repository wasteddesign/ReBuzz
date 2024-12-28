using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace BuzzGUI.Common.Settings
{
    public class Setting : INotifyPropertyChanged
    {
        readonly Settings settings;
        readonly PropertyInfo pi;

        public Setting(Settings settings, PropertyInfo pi)
        {
            this.settings = settings;
            this.pi = pi;
        }

        public string Value
        {
            get
            {
                if (Presets == null)
                {
                    var v = pi.GetValue(settings, null);
                    return v != null ? v.ToString() : "<null>";
                }
                else
                {
                    var sel = Presets.FirstOrDefault(p => p.Item2.All(s => settings.Get(s.Item1).Equals(s.Item2)));
                    return sel != null ? sel.Item1 : null;
                }
            }
            set
            {
                if (Presets == null)
                {
                    string oldv = pi.GetValue(settings, null).ToString();
                    if (value != oldv)
                    {
                        pi.SetValue(settings, TypeDescriptor.GetConverter(pi.PropertyType).ConvertFromString(value), null);
                        IsModified = true;
                        settings.OnPropertyChanged(Name);
                        PropertyChanged.Raise(this, "Value");
                    }
                }
                else
                {
                    foreach (var s in Presets.First(i => i.Item1 == value).Item2)
                        settings.Set(s.Item1, s.Item2);
                }
            }
        }

        public object ValueObject { get { return pi.GetValue(settings, null); } }

        public string Name { get; set; }
        public string[] Options { get; set; }
        public string Default { get; set; }
        public bool IsDefault { get { return Value == Default; } }
        public string Description { get; set; }
        public Tuple<string, Tuple<string, string>[]>[] Presets { get; set; }
        public bool IsModified { get; set; }

        public override string ToString() { return Name; }

        public event PropertyChangedEventHandler PropertyChanged;

    };

    public abstract class Settings : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        readonly string registryLocation;

        IEnumerable<PropertyInfo> Properties { get { return from x in GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance) where x.DeclaringType != typeof(Settings) select x; } }
        public List<Setting> List = new List<Setting>();

        public void OnPropertyChanged(string name)
        {
            PropertyChanged.Raise(this, name);
        }

        public void Set(string name, string value)
        {
            List.First(s => s.Name == name).Value = value;
        }

        public void Set(string name, int value)
        {
            Set(name, value.ToString());
        }

        public void Set(string name, bool value)
        {
            Set(name, value ? "True" : "False");
        }

        public object Get(string name)
        {
            return List.First(s => s.Name == name).Value;
        }

        public Settings()
        {
            var t = GetType();

            foreach (var p in Properties)
            {
                var decl = p.GetCustomAttributes(false).FirstOrDefault(z => z.GetType() == typeof(BuzzSetting)) as BuzzSetting;
                if (decl != null)
                    p.SetValue(this, decl.DefaultValue, null);

                Setting s = new Setting(this, p)
                {
                    Name = p.Name,
                    Default = decl != null && decl.DefaultValue != null ? decl.DefaultValue.ToString() : "",
                    Description = decl.Description,
                };

                if (decl.Type == BuzzSettingType.FontFamily)
                    s.Options = Fonts.SystemFontFamilies.Select(ff => ff.Source).OrderBy(n => n).ToArray();
                else if (decl.Type == BuzzSettingType.FontStyle)
                    s.Options = typeof(FontStyles).GetProperties(BindingFlags.Public | BindingFlags.Static).Select(fi => fi.Name).ToArray();
                else if (decl.Type == BuzzSettingType.FontWeight)
                    s.Options = typeof(FontWeights).GetProperties(BindingFlags.Public | BindingFlags.Static).Select(fi => fi.Name).ToArray();
                else if (decl.Type == BuzzSettingType.FontStretch)
                    s.Options = typeof(FontStretches).GetProperties(BindingFlags.Public | BindingFlags.Static).Select(fi => fi.Name).ToArray();
                else if (p.PropertyType == typeof(int))
                    s.Options = Enumerable.Range(decl.Minimum, decl.Maximum - decl.Minimum + 1).Select(x => x.ToString()).ToArray();
                else if (p.PropertyType == typeof(bool))
                    s.Options = new[] { "True", "False" };
                else if (p.PropertyType.IsEnum)
                    s.Options = p.PropertyType.GetFields(BindingFlags.Public | BindingFlags.Static).Select(fi => fi.Name).ToArray();

                if (decl.Presets != null)
                {
                    s.Options = decl.Presets.Split(null).Select(ol => ol.First() as string).ToArray();
                    s.Presets = decl.Presets.Split(null).Select(ol => Tuple.Create(
                        ol.First() as string,
                        ol.Skip(1).SelectFromTwo((k, v) => Tuple.Create(k as string, v.ToString())).ToArray()))
                        .ToArray();
                }

                List.Add(s);
            }

            registryLocation = Global.RegistryRoot + "BuzzGUI\\" + t.Name;
            Load();

        }

        public void Load()
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(registryLocation);
            if (key == null) key = Registry.CurrentUser.CreateSubKey(registryLocation);

            foreach (var p in List)
            {
                object v = key.GetValue(p.Name);
                if (v != null)
                {
                    try
                    {
                        p.Value = v.ToString();
                        p.IsModified = false;
                    }
                    catch (Exception) { }

                }

            }
        }

        public void Save()
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(registryLocation, true);
            if (key == null) key = Registry.CurrentUser.CreateSubKey(registryLocation);

            foreach (var p in List)
            {
                if (p.IsModified && p.ValueObject != null)
                {
                    key.SetValue(p.Name, p.ValueObject);
                    p.IsModified = false;
                }
            }
        }

    }
}
