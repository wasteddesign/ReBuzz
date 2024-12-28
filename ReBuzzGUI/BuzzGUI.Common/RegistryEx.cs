using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace BuzzGUI.Common
{
    public static class RegistryEx
    {
        static readonly string regpath = Global.RegistryRoot;

        public static void Write<T>(string key, T x, string path = "BuzzGUI")
        {
            try
            {
                var regkey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(regpath + "\\" + path);
                if (regkey == null) return;
                regkey.SetValue(key, x.ToString());
            }
            catch (Exception) { }

        }

        public static T Read<T>(string key, T def, string path = "BuzzGUI")
        {
            try
            {
                var regkey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(regpath + "\\" + path);
                if (regkey == null) return def;
                object o = regkey.GetValue(key);
                if (o == null) return def;
                return (T)TypeDescriptor.GetConverter(typeof(T)).ConvertFromString(o.ToString());
            }
            catch (Exception)
            {
                return def;
            }

        }

        public static IEnumerable<T> ReadNumberedList<T>(string key, string path = "BuzzGUI")
        {
            var regkey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(regpath + "\\" + path);
            if (regkey == null) yield break;

            int i = 0;
            while (i++ < 0x7fffffff)
            {
                object o = regkey.GetValue(key + i.ToString());
                if (o == null) yield break;
                yield return (T)TypeDescriptor.GetConverter(typeof(T)).ConvertFromString(o.ToString());
            }
        }

        public static RegistryMonitor CreateMonitor(string path = "BuzzGUI")
        {
            return new RegistryMonitor(Microsoft.Win32.RegistryHive.CurrentUser, regpath + path);
        }

    }
}
