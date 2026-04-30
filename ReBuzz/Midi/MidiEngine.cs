using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using NAudio.CoreAudioApi;
using NAudio.Midi;
using ReBuzz.Common;
using ReBuzz.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Windows.Devices.Enumeration;

namespace ReBuzz.Midi
{
    internal class MidiEngine
    {
        private readonly IRegistryEx registryEx;
        private readonly ReBuzzCore buzz;
        private readonly Dictionary<int, MidiInDevice> midiIns = new Dictionary<int, MidiInDevice>();
        private readonly Dictionary<int, MidiOutDevice> midiOuts = new Dictionary<int, MidiOutDevice>();
        private DeviceWatcher _watcher;

        Midi2 midi2;

        internal Midi2 Midi2 { get => midi2; }

        public MidiEngine(ReBuzzCore buzz, IRegistryEx registryEx)
        {
            this.registryEx = registryEx;
            this.buzz = buzz;

            midi2 = new Midi2(buzz);

            CheckRegistryDataFormat();

            // Use empty selector to get all MIDI devices
            string selector = "";// "System.Devices.InterfaceClassGuid:=\"{6DC23320-AB33-4CE4-80D4-BBB3EBBF2814}\"";

            _watcher = DeviceInformation.CreateWatcher(selector);
            _watcher.Added += OnDeviceAdded;
            _watcher.Removed += OnDeviceRemoved;
            _watcher.Updated += OnDeviceUpdated;
            _watcher.EnumerationCompleted += OnEnumerationCompleted;
            _watcher.Stopped += OnWatcherStopped;
            _watcher.Start();

        }
        private void OnDeviceAdded(DeviceWatcher sender, DeviceInformation args)
        {
            // New device spotted. Called a lot when watcher is started as it enumerates the installed (not connected) MIDI devices
            //buzz.DCWriteLine("OnDeviceAdded");
        }

        private void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            // A known device was removed (uninstalled)
            //buzz.DCWriteLine("OnDeviceRemoved");
        }

        Lock midiLock = new();
        private async void OnDeviceUpdated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            // A device that we already know about has changed somehow (connected, disconnected, other?)

            int midiInDevsCount = MidiIn.NumberOfDevices;
            int midiOutDevsCount = MidiOut.NumberOfDevices;

            var info = await DeviceInformation.CreateFromIdAsync(args.Id);

            lock (midiLock)
            {
                if (info.IsEnabled)
                {
                    buzz.DCWriteLine("Device connected: " + info.Name);

                    var inputsInfo = registryEx.ReadDictionary("MIDI In List");
                    if (inputsInfo.ContainsKey(info.Name))
                    {
                        if ((Int32)inputsInfo[info.Name] == 1)
                        {
                            for (int i = 0; i < midiInDevsCount; i++)
                            {
                                if (MidiIn.DeviceInfo(i).ProductName == info.Name)
                                {
                                    CreateMidiIn(i);
                                    break;
                                }
                            }
                        }
                    }

                    var outputsInfo = registryEx.ReadDictionary("MIDI Out List");
                    if (outputsInfo.ContainsKey(info.Name))
                    {
                        if ((Int32)outputsInfo[info.Name] == 1)
                        {
                            for (int i = 0; i < midiOutDevsCount; i++)
                            {
                                if (MidiOut.DeviceInfo(i).ProductName == info.Name)
                                {
                                    CreateMidiOut(i);
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    buzz.DCWriteLine("Device disconnected: " + info.Name);

                    var inputsInfo = registryEx.ReadDictionary("MIDI In List");
                    if (inputsInfo.ContainsKey(info.Name))
                    {
                        for (int i = 0; i < midiIns.Count; i++)
                        {
                            if (midiIns.ElementAt(i).Value.ProductName == info.Name)
                            {
                                midiIns.ElementAt(i).Value.UnsubscribeEvents();
                                midiIns.Remove(midiIns.ElementAt(i).Key);
                                break;
                            }
                        }
                    }

                    var outputsInfo = registryEx.ReadDictionary("MIDI Out List");
                    if (outputsInfo.ContainsKey(info.Name))
                    {
                        for (int i = 0; i < midiOuts.Count; i++)
                        {
                            if (midiOuts.ElementAt(i).Value.ProductName == info.Name)
                            {
                                midiOuts.Remove(midiOuts.ElementAt(i).Key);
                                break;
                            }
                        }
                    }
                }
            }
        }

        private void OnEnumerationCompleted(DeviceWatcher sender, object args)
        {
            // Watcher has finished enumerating devices
            //buzz.DCWriteLine("OnEnumerationCompleted");
        }

        private void OnWatcherStopped(DeviceWatcher sender, object args)
        {
            // The watcher has stopped watching
            //buzz.DCWriteLine("OnWatcherStopped");
        }

        public void DisposeWatcher()
        {
            if (_watcher != null)
            {
                _watcher.Added -= OnDeviceAdded;
                _watcher.Removed -= OnDeviceRemoved;
                _watcher.Updated -= OnDeviceUpdated;
                _watcher.EnumerationCompleted -= OnEnumerationCompleted;
                _watcher.Stopped -= OnWatcherStopped;

                if (_watcher.Status == DeviceWatcherStatus.Started ||
                    _watcher.Status == DeviceWatcherStatus.EnumerationCompleted)
                {
                    _watcher.Stop();
                }

                _watcher = null;
            }
        }

        public void CreateMidiIn(int selectedDeviceIndex)
        {
            try
            {
                if (midiIns.ContainsKey(selectedDeviceIndex))
                {
                    //midiIns[selectedDeviceIndex].DisposeMidiIn();
                    midiIns.Remove(selectedDeviceIndex);
                }

                if (!midiIns.ContainsKey(selectedDeviceIndex))
                {
                    var midiIn = new MidiInDevice(buzz);
                    midiIn.CreateMidiIn(selectedDeviceIndex);
                    midiIns.Add(selectedDeviceIndex, midiIn);
                }
            }
            catch (Exception e)
            {
                buzz.DCWriteLine(e.Message);
            }
        }

        public void DisposeMidiIn()
        {
            foreach (var md in midiIns.Values)
            {
                md.DisposeMidiIn();
            }
            midiIns.Clear();
        }

        public void CreateMidiOut(int selectedDeviceIndex)
        {
            try
            {
                if (midiOuts.ContainsKey(selectedDeviceIndex))
                {
                    //midiOuts[selectedDeviceIndex].MidiOut.Dispose();
                    midiOuts.Remove(selectedDeviceIndex);
                }

                if (!midiOuts.ContainsKey(selectedDeviceIndex))
                {
                    midiOuts.Add(selectedDeviceIndex, new MidiOutDevice(selectedDeviceIndex));
                }
            }
            catch (Exception e)
            {
                buzz.DCWriteLine(e.Message);
            }
        }

        Lock midiOutLock = new Lock();

        public void SendMidiOut(int device, int message)
        {
            if (midiOuts.ContainsKey(device))
            {
                lock (midiOutLock)
                {
                    try
                    {
                        midiOuts[device].MidiOut.Send(message);
                    }
                    catch (Exception e)
                    {
                        buzz.DCWriteLine(e.Message);
                    }
                }
            }
        }

        public void SendMidiOut(int device, byte[] message)
        {
            if (midiOuts.ContainsKey(device))
            {
                lock (midiOutLock)
                {
                    try
                    {
                        midiOuts[device].MidiOut.SendBuffer(message);
                    }
                    catch (Exception e)
                    {
                        buzz.DCWriteLine(e.Message);
                    }
                }
            }
        }

        public void DisposeMidiOuts()
        {
            lock (ReBuzzCore.AudioLock)
            {
                foreach (var midiOut in midiOuts.Values)
                {
                    try
                    {
                        midiOut.MidiOut.Dispose();
                    }
                    catch (Exception e)
                    {
                        buzz.DCWriteLine(e.Message);
                    }
                }

                midiOuts.Clear();
            }
        }

        public void ReleaseAll()
        {
            DisposeMidiIn();
            DisposeMidiOuts();

            Midi2.Release();
        }


        internal static IList<string> GetMidiControllers(IRegistryEx registryEx)
        {
            List<string> list = new List<string>();

            int numMidiControllers = registryEx.Read("numMidiControllers", 0, "Settings");
            for (int i = 0; i < numMidiControllers; i++)
            {
                list.Add(registryEx.Read("MidiController" + i, "", "Settings"));
            }

            return list;
        }

        internal void CheckRegistryDataFormat()
        {
            long version = registryEx.Read("MIDIDeviceFormat", 1, "Settings");

            if(version == 1)
            {
                // Convert MidiOut1 = SomeDevice to SomeDevice = flags
                // flags is just 1 for enabled now but may add more (eg. hidden)
                var inputsInfoOld = registryEx.ReadDictionary("MIDI In List");
                Dictionary<string, Int32> inputsInfo = new Dictionary<string, Int32>();
                foreach (var item in inputsInfoOld)
                {
                    inputsInfo.Add((string)item.Value, 1);
                }

                registryEx.DeleteCurrentUserSubKey(buzz.registryRoot + "\\" + "MIDI In List");
                SetMidiInputDevices2(inputsInfo);

                var outputsInfoOld = registryEx.ReadDictionary("MIDI Out List");
                Dictionary<string, Int32> outputsInfo = new Dictionary<string, Int32>();
                foreach (var item in outputsInfoOld)
                {
                    outputsInfo.Add((string)item.Value, 1);
                }
                registryEx.DeleteCurrentUserSubKey(buzz.registryRoot + "\\" + "MIDI Out List");
                SetMidiOutputDevices2(outputsInfo);
            }

            registryEx.Write("MIDIDeviceFormat", 2, "Settings");
        }

        internal IEnumerable<int> GetMidiInputDevices()
        {
            return midiIns.Keys.ToReadOnlyCollection();
        }

        internal void OpenMidiInDevices2()
        {
            var inputsInfo = registryEx.ReadDictionary("MIDI In List").KeyValuesToStringInt();
            
            int midiInDevsCount = MidiIn.NumberOfDevices;

            foreach (var item in inputsInfo)
            {
                if (((Int32)item.Value & 1) > 0) // Flags & 1 == enabled
                {
                    for (int i = 0; i < midiInDevsCount; i++)
                    {
                        if (MidiIn.DeviceInfo(i).ProductName == item.Key)
                        {
                            CreateMidiIn(i);
                            break;
                        }
                    }
                }
            }
        }

        internal void SetMidiInputDevices2(Dictionary<string, Int32> midiInDevices)
        {
            var regKey = registryEx.CreateCurrentUserSubKey(buzz.registryRoot + "\\" + "MIDI In List");
            try
            {
                foreach (var item in midiInDevices)
                {
                    regKey.SetValue(item.Key, item.Value); // DeviceName = BitFlags
                }
            }
            catch { }
        }

        internal IEnumerable<int> GetMidiOutputDevices()
        {
            return midiOuts.Keys.ToReadOnlyCollection();
        }

        internal IEnumerable<Tuple<int, string>> GetMidiOuts()
        {
            List<Tuple<int, string>> moList = new List<Tuple<int, string>>();
            foreach (var mo in midiOuts)
                moList.Add(new Tuple<int, string>(mo.Key, mo.Value.ProductName));

            return moList;
        }

        internal void SetMidiOutputDevices2(Dictionary<string, Int32> midiOutDevices)
        {
            var regKey = registryEx.CreateCurrentUserSubKey(buzz.registryRoot + "\\" + "MIDI Out List");
            try
            {
                foreach (var item in midiOutDevices)
                {
                    regKey.SetValue(item.Key, item.Value); // DeviceName = BitFlags
                }
            }
            catch { }
        }

        internal void OpenMidiOutDevices2()
        {
            var outputsInfo = registryEx.ReadDictionary("MIDI Out List").KeyValuesToStringInt();

            int midiOutDevsCount = MidiOut.NumberOfDevices;

            foreach (var item in outputsInfo)
            {
                if (((Int32)item.Value & 1) > 0) // Flags & 1 == enabled
                {
                    for (int i = 0; i < midiOutDevsCount; i++)
                    {
                        if (MidiOut.DeviceInfo(i).ProductName == item.Key)
                        {
                            CreateMidiOut(i);
                            break;
                        }
                    }
                }
            }
        }
    }
}
