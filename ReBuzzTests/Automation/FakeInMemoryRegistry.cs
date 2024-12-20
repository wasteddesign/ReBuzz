using ReBuzz.Core;
using System.Collections.Generic;
using System.Threading;

namespace ReBuzzTests.Automation
{
    internal class FakeInMemoryRegistry : IRegistryEx
    {
        private readonly Lock lockObject = new();

        private readonly Dictionary<(string Path, string Key), object> registryDictionary = new()
        {
            [("ASIO", "SampleRate")] = 44100,
            [("ASIO", "BufferSize")] = 1024,
            [("BuzzGUI\\EngineSettings", "ProcessMutedMachines")] = false,
            [("BuzzGUI\\EngineSettings", "MachineDelayCompensation")] = true,
            [("BuzzGUI\\EngineSettings", "AccurateBPM")] = true,
            [("BuzzGUI\\EngineSettings", "Multithreading")] = true,
            [("BuzzGUI\\EngineSettings", "SubTickTiming")] = true,
            [("BuzzGUI\\MachineViewSettings", "SignalAnalysisMode")] = "VST",
            [("BuzzGUI\\SequenceEditorSettings", "HideEditor")] = true,
            [("BuzzGUI\\SignalAnalysisVSTWindow", "DefaultSignalAnalysisVST")] =
                @"C:\Program Files\ReBuzz\Gear\Vst\JoaCHIP Quick Meter.dll",
            [("Recent File List", "File1")] = @"C:\Songs\Song1.bmx",
            [("Recent File List", "File2")] = @"C:\Songs\Song2.bmx",
            [("Recent File List", "File3")] = @"C:\Songs\Song3.bmx",
            [("Recent File List", "File4")] = @"C:\Songs\Song4.bmx",
            [("Recent File List", "File5")] = @"C:\Songs\Song5.bmx",
            [("Recent File List", "File6")] = @"C:\Songs\Song6.bmx",
            [("Recent File List", "File7")] = @"C:\Songs\Song7.bmx",
            [("Recent File List", "File8")] = @"C:\Songs\Song8.bmx",
            [("Settings", "DefaultPE")] = "Modern Pattern Editor",
            [("Settings", "Theme")] = "<default>",
            [("Settings", "BuzzPath64")] = @"C:\Program Files\ReBuzz\",
            [("Settings", "AudioDriver")] = "ASIO4ALL v2",
            [("Settings", "OpenMidiInDevs")] = "00",
            [("Settings", "OpenMidiOutDevs")] = "",
            [("Settings", "numMidiControllers")] = 0,
            [("Settings", "ProcessorAffinity")] = 65535u,
            [("Settings", "AudioThreadType")] = 0,
            [("Settings", "AudioThreads")] = 8,
            [("Settings", "WorkAlgorithm")] = 1,
            [("Settings", "MidiControllerPlay")] = "",
            [("Settings", "MidiControllerStop")] = "",
            [("Settings", "MidiControllerRecord")] = "",
            [("Settings", "MidiControllerForward")] = "",
            [("Settings", "MidiControllerBackward")] = "",
            [("WASAPI", "DeviceID")] = "{0.0.0.00000000}.{49a443a1-228c-42cf-bcbb-2c9a48480989}",
            [("WASAPI", "DeviceIDIn")] = "",
            [("WASAPI", "SampleRate")] = 44100,
            [("WASAPI", "Mode")] = 1,
            [("WASAPI", "Poll")] = 0,
            [("WASAPI", "BufferSize")] = 8192
        };

        public void Write<T>(string key, T x, string path = "BuzzGUI")
        {
            lock (lockObject)
            {
                TestContext.Out.WriteLine($"Write to memory: {path}=>{key}=>{x}");
                registryDictionary[(path, key)] = x;
            }
        }

        public T Read<T>(string key, T def, string path = "BuzzGUI")
        {
            lock (lockObject)
            {
                if (registryDictionary.TryGetValue((path, key), out var cachedResult))
                {
                    TestContext.Out.WriteLine($"Read: {path}=>{key}=>{cachedResult}");
                    return (T)cachedResult;
                }

                Assert.Fail($"Read from in-memory registry failed: {path}, {key}");
                return default!;
            }
        }

        public IEnumerable<T> ReadNumberedList<T>(string key, string path = "BuzzGUI")
        {
            lock (lockObject)
            {
                if (registryDictionary.TryGetValue((path, key), out var cachedResult))
                {
                    IEnumerable<T> numberedList = (IEnumerable<T>)cachedResult;
                    TestContext.Out.WriteLine($"ReadNumberedList: {path}=>{key}=>[{string.Join(", ", numberedList)}]");
                    return numberedList;
                }

                Assert.Fail($"ReadNumberedList from in-memory registry failed: {path}, {key}");

                return default!;
            }
        }

        public void DeleteCurrentUserSubKey(string key)
        {
            Assert.Fail("Not used in any of the current tests");
        }

        public IRegistryKey CreateCurrentUserSubKey(string subKey)
        {
            Assert.Fail("Not used in any of the current tests");
            return null!;
        }
    }
}