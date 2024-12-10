using System;
using System.Collections.Generic;
using BuzzGUI.Common;

namespace ReBuzz.Core;

public interface IRegistryEx
{
  void Write<T>(string key, T x, string path = "BuzzGUI");
  T Read<T>(string key, T def, string path = "BuzzGUI");
  IEnumerable<T> ReadNumberedList<T>(string key, string path = "BuzzGUI");
}

public class RegistryExInstance : IRegistryEx
{
  public void Write<T>(string key, T x, string path = "BuzzGUI")
  {
    Console.WriteLine("Write: " + key + " " + x + " " + path); //bug
    RegistryEx.Write(key, x, path);
  }

  public T Read<T>(string key, T def, string path = "BuzzGUI")
  {
    var result = RegistryEx.Read(key, def, path);
    Console.WriteLine("Read: " + key + " " + result + " " + path);
    return result;
  }

  public IEnumerable<T> ReadNumberedList<T>(string key, string path = "BuzzGUI")
  {
    var numberedList = RegistryEx.ReadNumberedList<T>(key, path);
    Console.WriteLine("ReadNumberedList: " + key + " [" + string.Join(", ", numberedList) + "] " + path);
    return numberedList;
  }

  public static RegistryMonitor CreateMonitor(string path = "BuzzGUI")
  {
    return RegistryEx.CreateMonitor(path);
  }
}