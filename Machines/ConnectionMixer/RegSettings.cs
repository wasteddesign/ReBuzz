using BuzzGUI.Common;
using Microsoft.Win32;
using System;
using System.Collections.Generic;

namespace WDE.ConnectionMixer
{
    internal class RegSettings
    {
        public static readonly int NumbeOfRecentFiles = 10;
        private static string registryLocation = Global.RegistryRoot + "CMC\\";

        public static bool SoftTakeoverOnMIDIFocus;

        public static bool MouseWheelEnabled { get; private set; }

        public static List<string> GetRecentFiles()
        {
            List<string> files = new List<string>();

            RegistryKey key = Registry.CurrentUser.OpenSubKey(registryLocation);
            if (key == null) key = Registry.CurrentUser.CreateSubKey(registryLocation);

            for (int i = 0; i < NumbeOfRecentFiles; i++)
            {
                object v = key.GetValue("File" + i);
                if (v != null)
                {
                    try
                    {
                        string fileName = v.ToString();
                        files.Add(fileName);
                    }
                    catch (Exception) { }
                }
            }

            return files;
        }

        public static void AddFileToRecentFiles(string fileName)
        {
            List<string> files = GetRecentFiles();
            files.Remove(fileName);
            files.Insert(0, fileName);
            if (files.Count >= NumbeOfRecentFiles)
                files.RemoveAt(files.Count - 1);

            RegistryKey key = Registry.CurrentUser.OpenSubKey(registryLocation, true);
            if (key == null) key = Registry.CurrentUser.CreateSubKey(registryLocation);

            for (int i = 0; i < files.Count; i++)
            {
                try
                {
                    key.SetValue("File" + i, files[i]);

                }
                catch (Exception) { }
            }
        }

        internal static bool IsAlwaysSoftTakeoverOnMIDIFocus()
        {
            bool ret = true;
            RegistryKey key = Registry.CurrentUser.OpenSubKey(registryLocation, true);
            if (key == null) key = Registry.CurrentUser.CreateSubKey(registryLocation);

            object v = key.GetValue("AlwaysSoftTakeoverOnMIDIFocus", "True");
            if (v != null)
            {
                try
                {
                    ret = Boolean.Parse((string)v);
                }
                catch (Exception) { }
            }

            SoftTakeoverOnMIDIFocus = ret;

            return ret;
        }

        internal static void SetAlwaysSoftTakeoverOnMIDIFocus(bool isChecked)
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(registryLocation, true);
            if (key == null) key = Registry.CurrentUser.CreateSubKey(registryLocation);

            try
            {
                key.SetValue("AlwaysSoftTakeoverOnMIDIFocus", isChecked);
            }
            catch (Exception) { };

            SoftTakeoverOnMIDIFocus = isChecked;
        }

        internal static bool IsMouseWheelEnabled()
        {
            bool ret = true;
            RegistryKey key = Registry.CurrentUser.OpenSubKey(registryLocation, true);
            if (key == null) key = Registry.CurrentUser.CreateSubKey(registryLocation);

            object v = key.GetValue("MouseWheelEnabled", "False");
            if (v != null)
            {
                try
                {
                    ret = Boolean.Parse((string)v);
                }
                catch (Exception) { }
            }

            MouseWheelEnabled = ret;

            return ret;
        }

        internal static void SetMouseWheelEnabled(bool isChecked)
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(registryLocation, true);
            if (key == null) key = Registry.CurrentUser.CreateSubKey(registryLocation);

            try
            {
                key.SetValue("MouseWheelEnabled", isChecked);
            }
            catch (Exception) { };

            MouseWheelEnabled = isChecked;
        }
    }
}
