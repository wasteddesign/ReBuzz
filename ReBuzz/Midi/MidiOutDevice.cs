using NAudio.Midi;
using System;
using System.Collections.Generic;
using System.Text;

namespace ReBuzz.Midi
{
    internal class MidiOutDevice
    {
        public string ProductName { get; private set; }

        public MidiOut MidiOut { get; private set; }

        public MidiOutDevice(int deviceid)
        {
            this.ProductName = MidiOut.DeviceInfo(deviceid).ProductName;
            this.MidiOut = new MidiOut(deviceid);
        }
    }
}
