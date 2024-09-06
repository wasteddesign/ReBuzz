using BuzzGUI.Common;
using NAudio.Midi;
using ReBuzz.Core;
using System;
using System.Collections.Generic;

namespace ReBuzz.Midi
{
    internal class MidiEngine
    {
        private readonly ReBuzzCore buzz;
        private readonly Dictionary<int, MidiInDevice> midiIns = new Dictionary<int, MidiInDevice>();
        private readonly Dictionary<int, MidiOut> midiOuts = new Dictionary<int, MidiOut>();

        public MidiEngine(ReBuzzCore buzz)
        {
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

        public void SendMidiOut(int device, int message)
        {
            if (device >= 0 && device < midiOuts.Count)
            {
                midiOuts[device].Send(message);
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


        internal static IList<string> GetMidiControllers()
        {
            List<string> list = new List<string>();

            int numMidiControllers = RegistryEx.Read("numMidiControllers", 0, "Settings");
            for (int i = 0; i < numMidiControllers; i++)
            {
                list.Add(RegistryEx.Read("MidiController" + i, "", "Settings"));
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

            RegistryEx.Write("OpenMidiInDevs", strOpenDevices, "Settings");
        }

        internal void OpenMidiInDevices()
        {
            string strOpenDevices = RegistryEx.Read("OpenMidiInDevs", "", "Settings");

            while (strOpenDevices.Length > 0)
            {
                if (int.TryParse(strOpenDevices.Substring(0, 2), out int midiInputDevice) == true)
                {
                    CreateMidiIn(midiInputDevice);
                }
                strOpenDevices = strOpenDevices.Substring(2);
            }
        }

        internal static void SetMidiInputDevices(List<int> midiIns)
        {
            string strOpenDevices = "";

            foreach (int device in midiIns)
            {
                strOpenDevices += device.ToString("D2");
            }

            RegistryEx.Write("OpenMidiInDevs", strOpenDevices, "Settings");
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

            RegistryEx.Write("OpenMidiOutDevs", strOpenDevices, "Settings");
        }

        internal void OpenMidiOutDevices()
        {
            string strOpenDevices = RegistryEx.Read("OpenMidiOutDevs", "", "Settings");

            while (strOpenDevices.Length > 0)
            {
                if (int.TryParse(strOpenDevices.Substring(0, 2), out int midiOutputDevice) == true)
                {
                    CreateMidiOut(midiOutputDevice);
                }
                strOpenDevices = strOpenDevices.Substring(2);
            }
        }

        internal static void SetMidiOutputDevices(List<int> midiOuts)
        {
            string strOpenDevices = "";

            foreach (int device in midiOuts)
            {
                strOpenDevices += device.ToString("D2");
            }

            RegistryEx.Write("OpenMidiOutDevs", strOpenDevices, "Settings");
        }
    }
}
