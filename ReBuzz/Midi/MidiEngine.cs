using BuzzGUI.Common;
using NAudio.Midi;
using ReBuzz.Core;
using System;
using System.Collections.Generic;
using BuzzGUI.Interfaces;
using System.Threading;

namespace ReBuzz.Midi
{
    internal class MidiEngine
    {
        private readonly IRegistryEx registryEx;
        private readonly IBuzz buzz;
        private readonly Dictionary<int, MidiInDevice> midiIns = new Dictionary<int, MidiInDevice>();
        private readonly Dictionary<int, MidiOut> midiOuts = new Dictionary<int, MidiOut>();

        public MidiEngine(IBuzz buzz, IRegistryEx registryEx)
        {
            this.registryEx = registryEx;
            this.buzz = buzz;
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
                    midiOut.Dispose();
                }

                midiOuts.Clear();
            }
        }

        public void ReleaseAll()
        {
            DisposeMidiIn();
            DisposeMidiOuts();
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

        internal void SetMidiInputDevices()
        {
            string strOpenDevices = "";

            foreach (int device in midiIns.Keys)
            {
                strOpenDevices += device.ToString("D2");
            }

            registryEx.Write("OpenMidiInDevs", strOpenDevices, "Settings");
        }

        internal void OpenMidiInDevices()
        {
            string strOpenDevices = registryEx.Read("OpenMidiInDevs", "", "Settings");

            while (strOpenDevices.Length > 0)
            {
                if (int.TryParse(strOpenDevices.Substring(0, 2), out int midiInputDevice) == true)
                {
                    CreateMidiIn(midiInputDevice);
                }
                strOpenDevices = strOpenDevices.Substring(2);
            }
        }

        internal static void SetMidiInputDevices(IRegistryEx registryEx, List<int> midiIns)
        {
            string strOpenDevices = "";

            foreach (int device in midiIns)
            {
                strOpenDevices += device.ToString("D2");
            }

            registryEx.Write("OpenMidiInDevs", strOpenDevices, "Settings");
        }

        internal IEnumerable<int> GetMidiOutputDevices()
        {
            return midiOuts.Keys.ToReadOnlyCollection();
        }

        internal void SetMidiOutputDevices()
        {
            string strOpenDevices = "";

            foreach (int device in midiOuts.Keys)
            {
                strOpenDevices += device.ToString("D2");
            }

            registryEx.Write("OpenMidiOutDevs", strOpenDevices, "Settings");
        }

        internal void OpenMidiOutDevices()
        {
            string strOpenDevices = registryEx.Read("OpenMidiOutDevs", "", "Settings");

            while (strOpenDevices.Length > 0)
            {
                if (int.TryParse(strOpenDevices.Substring(0, 2), out int midiOutputDevice) == true)
                {
                    CreateMidiOut(midiOutputDevice);
                }
                strOpenDevices = strOpenDevices.Substring(2);
            }
        }

        internal static void SetMidiOutputDevices(IRegistryEx registryEx, List<int> midiOuts)
        {
            string strOpenDevices = "";

            foreach (int device in midiOuts)
            {
                strOpenDevices += device.ToString("D2");
            }

            registryEx.Write("OpenMidiOutDevs", strOpenDevices, "Settings");
        }
    }
}
