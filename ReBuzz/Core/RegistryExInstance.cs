using System;
using System.Collections.Generic;
using BuzzGUI.Common;
using Microsoft.Win32;

namespace ReBuzz.Core;

public interface IRegistryEx
{
    void Write<T>(string key, T x, string path = "BuzzGUI");
    T Read<T>(string key, T def, string path = "BuzzGUI");
    IEnumerable<T> ReadNumberedList<T>(string key, string path = "BuzzGUI");
    void DeleteCurrentUserSubkey(string key);
    void CreateCurrentUserSubKey(string subkey);
    void SetCurrentUserSubKeyValue(string subKey, string name, string value);
}

public class RegistryExInstance : IRegistryEx
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
        var numberedList = RegistryEx.ReadNumberedList<T>(key, path);
        Console.WriteLine("ReadNumberedList: " + key + " [" + string.Join(", ", numberedList) + "] " + path);
        return numberedList;
    }

    public void DeleteCurrentUserSubkey(string key)
    {
        Registry.CurrentUser.DeleteSubKey(key);
    }

    public void CreateCurrentUserSubKey(string subkey)
    {
        Registry.CurrentUser.CreateSubKey(subkey);
    }

    public void SetCurrentUserSubKeyValue(string subKey, string name, string value)
    {
        (Registry.CurrentUser.OpenSubKey(subKey) ?? Registry.CurrentUser.CreateSubKey(subKey)).SetValue(name, value);
    }
}