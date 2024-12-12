using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using AtmaFileSystem;
using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Win32;
using ReBuzz.Core;
using ReBuzz.FileOps;
using ReBuzz.MachineManagement;
using ReBuzz.ManagedMachine;
using ReBuzz.Midi;

namespace ReBuzzTests;

public class Tests
{
    [Test]
    public void Lol()
    {
        string registryPath = @"Software\ReBuzz";
        Dictionary<string, string> registryData = new Dictionary<string, string>();

        try
        {
            ReadRegistryKey(Registry.CurrentUser.OpenSubKey(registryPath), registryData, registryPath);

            // Display the registry data
            foreach (var entry in registryData)
            {
                Console.WriteLine($"{entry.Key} => {entry.Value}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    static void ReadRegistryKey(RegistryKey key, Dictionary<string, string> registryData, string basePath)
    {
        if (key != null)
        {
            // Read all values in the current key
            foreach (string valueName in key.GetValueNames())
            {
                object value = key.GetValue(valueName);
                string fullPath = $"{basePath}\\{valueName}";
                registryData[fullPath] = value?.ToString() ?? "null";
            }

            // Recursively read all subkeys
            foreach (string subKeyName in key.GetSubKeyNames())
            {
                using (RegistryKey subKey = key.OpenSubKey(subKeyName))
                {
                    ReadRegistryKey(subKey, registryData, $"{basePath}\\{subKeyName}");
                }
            }
        }
        else
        {
            Console.WriteLine($"Registry key {basePath} not found.");
        }
    }


    [Test]
    public void ReadsGearFilesOnCreation([Values(1, 2)] int x)
    {
        using var driver = new Driver();

        driver.Start();

        driver.AssertRequiredPropertiesAreInitialized();
        driver.AssertGearMachinesConsistOf([
          "Jeskola Pianoroll",
      "Modern Pattern Editor",
      "Jeskola Pattern XP",
      "Jeskola Pattern XP mod",
      "Modern Pianoroll",
      "Polac VST 1.1",
      "Polac VSTi 1.1",
      "Jeskola XS-1",
      "CyanPhase Buzz OverLoader",
      "CyanPhase DX Instrument Adapter",
      "CyanPhase DX Effect Adapter",
      "CyanPhase DMO Effect Adapter",
      "11-MidiCCout",
      "Rymix*",
      "FireSledge ParamEQ",
      "BTDSys Pulsar"
        ]);

        driver.NewFile();

        driver.AssertInitialState();
    }
}