using System;
using System.Collections.Generic;
using BuzzGUI.Common;
using Microsoft.Win32;

namespace ReBuzz.Core
{
    public interface IRegistryEx
    {
        void Write<T>(string key, T x, string path = "BuzzGUI");
        T Read<T>(string key, T def, string path = "BuzzGUI");
        IEnumerable<T> ReadNumberedList<T>(string key, string path = "BuzzGUI");
        void DeleteCurrentUserSubKey(string key);
        IRegistryKey CreateCurrentUserSubKey(string subKey);
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

        public void DeleteCurrentUserSubKey(string key)
        {
            Registry.CurrentUser.DeleteSubKey(key);
        }

        public IRegistryKey CreateCurrentUserSubKey(string subKey)
        {
            return new WindowsRegistryKey(Registry.CurrentUser.CreateSubKey(subKey));
        }
    }
}