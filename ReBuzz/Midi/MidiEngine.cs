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
            var midiIns = registryEx.ReadNumberedList<string>("MidiIn", "MIDI In List").ToList();
            int midiInDevsCount = MidiIn.NumberOfDevices;

            foreach (var productName in midiIns)
            {
                for (int i = 0; i < midiInDevsCount; i++)
                {
                    if (MidiIn.DeviceInfo(i).ProductName == productName)
                    {
                        CreateMidiIn(i);
                        break;
                    }
                }
            }
        }

        internal void SetMidiInputDevices2(List<int> midiInDevices)
        {
            try
            {
                registryEx.DeleteCurrentUserSubKey(buzz.registryRoot + "\\" + "MIDI In List");
            }
            catch { }

            var regKey = registryEx.CreateCurrentUserSubKey(buzz.registryRoot + "\\" + "MIDI In List");

            int i = 1;
            foreach(var id in midiInDevices)
            {
                regKey.SetValue("MidiIn" + (i++), MidiIn.DeviceInfo(id).ProductName);
            }
        }

        internal IEnumerable<int> GetMidiOutputDevices()
        {
            return midiOuts.Keys.ToReadOnlyCollection();
        }


        internal void SetMidiOutputDevices2(List<int> midiOutDevices)
        {
            try
            {
                registryEx.DeleteCurrentUserSubKey(buzz.registryRoot + "\\" + "MIDI Out List");
            }
            catch { }

            var regKey = registryEx.CreateCurrentUserSubKey(buzz.registryRoot + "\\" + "MIDI Out List");

            int i = 1;
            foreach (var id in midiOutDevices)
            {
                regKey.SetValue("MidiOut" + (i++), MidiOut.DeviceInfo(id).ProductName);
            }
        }

        internal void OpenMidiOutDevices2()
        {
            var midiOuts = registryEx.ReadNumberedList<string>("MidiOut", "MIDI Out List").ToList();
            int midiOutDevsCount = MidiOut.NumberOfDevices;

            foreach (var productName in midiOuts)
            {
                for (int i = 0; i < midiOutDevsCount; i++)
                {
                    if (MidiOut.DeviceInfo(i).ProductName == productName)
                    {
                        CreateMidiOut(i);
                        break;
                    }
                }
            }
        }
    }
}
