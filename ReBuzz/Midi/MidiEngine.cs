using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using NAudio.Midi;
using ReBuzz.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ReBuzz.Midi
{
    internal class MidiEngine
    {
        private readonly IRegistryEx registryEx;
        private readonly ReBuzzCore buzz;
        private readonly Dictionary<int, MidiInDevice> midiIns = new Dictionary<int, MidiInDevice>();
        private readonly Dictionary<int, MidiOut> midiOuts = new Dictionary<int, MidiOut>();

        Midi2 midi2;

        internal Midi2 Midi2 { get => midi2; }

        public MidiEngine(ReBuzzCore buzz, IRegistryEx registryEx)
        {
            this.registryEx = registryEx;
            this.buzz = buzz;

            midi2 = new Midi2(buzz);
        }

        public void CreateMidiIn(int selectedDeviceIndex)
        {
            try
            {
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
                if (!midiOuts.ContainsKey(selectedDeviceIndex))
                    midiOuts.Add(selectedDeviceIndex, new MidiOut(selectedDeviceIndex));
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
                        midiOuts[device].Send(message);
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
                        midiOuts[device].SendBuffer(message);
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
                foreach (MidiOut midiOut in midiOuts.Values)
                {
                    try
                    {
                        midiOut.Dispose();
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

        internal IEnumerable<int> GetMidiInputDevices()
        {
            return midiIns.Keys.ToReadOnlyCollection();
        }

        internal void OpenMidiInDevices2()
        {
            var midiIns = registryEx.ReadDictionary("MIDI Inputs");
            
            int midiInDevsCount = MidiIn.NumberOfDevices;

            foreach (var item in midiIns)
            {
                if ((Int32)item.Value == 1)
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

        internal void SetMidiInputDevices2(Dictionary<string, bool> midiInDevices)
        {
            var regKey = registryEx.CreateCurrentUserSubKey(buzz.registryRoot + "\\" + "MIDI Inputs");
            try
            {
                foreach (var item in midiInDevices)
                {
                    regKey.SetValue(item.Key, item.Value ? 1 : 0);
                }
            }
            catch { }
        }

        internal IEnumerable<int> GetMidiOutputDevices()
        {
            return midiOuts.Keys.ToReadOnlyCollection();
        }


        internal void SetMidiOutputDevices2(Dictionary<string, bool> midiOutDevices)
        {
            var regKey = registryEx.CreateCurrentUserSubKey(buzz.registryRoot + "\\" + "MIDI Outputs");
            try
            {
                foreach (var item in midiOutDevices)
                {
                    regKey.SetValue(item.Key, item.Value ? 1 : 0);
                }
            }
            catch { }
        }

        internal void OpenMidiOutDevices2()
        {
            var midiOuts = registryEx.ReadDictionary("MIDI Outputs");

            int midiOutDevsCount = MidiOut.NumberOfDevices;

            foreach (var item in midiOuts)
            {
                if ((Int32)item.Value == 1)
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
