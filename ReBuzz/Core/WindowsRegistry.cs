using BuzzGUI.Common;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Windows.Input;

namespace ReBuzz.Core
{
    public interface IRegistryEx
    {
        void Write<T>(string key, T x, string path = "BuzzGUI");
        T Read<T>(string key, T def, string path = "BuzzGUI");
        IEnumerable<T> ReadNumberedList<T>(string key, string path = "BuzzGUI");
        Dictionary<string, object> ReadDictionary(string path = "BuzzGUI");
        void DeleteCurrentUserSubKey(string key);
        IRegistryKey CreateCurrentUserSubKey(string subKey);
        void DeleteCurrentUserValue(string value, string path);
    }

    public class WindowsRegistry : IRegistryEx
    {
        public void Write<T>(string key, T x, string path = "BuzzGUI")
        {
            RegistryEx.Write(key, x, path);
        }

        public T Read<T>(string key, T def, string path = "BuzzGUI")
        {
            return RegistryEx.Read(key, def, path);
        }

        public IEnumerable<T> ReadNumberedList<T>(string key, string path = "BuzzGUI")
        {
            IEnumerable<T> numberedList = RegistryEx.ReadNumberedList<T>(key, path);
            Console.WriteLine("ReadNumberedList: " + key + " [" + string.Join(", ", numberedList) + "] " + path);
            return numberedList;
        }

        public Dictionary<string, object> ReadDictionary(string path = "BuzzGUI")
        {
            Dictionary<string, object> values = RegistryEx.ReadDictionary(path);
            Console.WriteLine("ReadDictionary: " + " [" + string.Join(", ", values) + "] " + path);
            return values;
        }

        public void DeleteCurrentUserSubKey(string key)
        {
            using (RegistryKey rkey = Registry.CurrentUser.OpenSubKey(key, true))
            {
                // Check if the key exists
                if (rkey != null)
                {
                    Registry.CurrentUser.DeleteSubKey(key);
                }
            }
        }

        public IRegistryKey CreateCurrentUserSubKey(string subKey)
        {
            return new WindowsRegistryKey(Registry.CurrentUser.CreateSubKey(subKey));
        }

        public void DeleteCurrentUserValue(string value, string path)
        {
            using (RegistryKey rkey = Registry.CurrentUser.OpenSubKey(path, true))
            {
                // Check if the key exists
                if (rkey != null)
                {
                    rkey.DeleteValue(value);
                }
            }
        }
    }
}