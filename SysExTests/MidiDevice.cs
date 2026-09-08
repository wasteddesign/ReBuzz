using System;
using System.Collections.Generic;
using System.Text;

namespace SysExTests
{
    using NAudio.Midi;
    using ReBuzz.Midi;
    using System;

    public class MidiDevice : IDisposable
    {
        private readonly MidiIn _midiIn;
        private readonly MidiOut _midiOut;
        private readonly MidiSynthStateManager _stateManager;
        private readonly MidiCiStateManager _ciManager;

        public int InputIndex { get; }
        public int OutputIndex { get; }

        public MidiDevice(int inputDeviceIndex, int outputDeviceIndex,
                          MidiSynthStateManager stateManager,
                          MidiCiStateManager ciManager)
        {
            InputIndex = inputDeviceIndex;
            OutputIndex = outputDeviceIndex;
            _stateManager = stateManager;
            _ciManager = ciManager;

            _midiIn = new MidiIn(inputDeviceIndex);
            _midiOut = new MidiOut(outputDeviceIndex);

            _midiIn.SysexMessageReceived += OnSysExMessageReceaved;
            _midiIn.ErrorReceived += (s, e) => Console.WriteLine($"MIDI In error: {e}");
            _midiIn.Start();
        }

        private void OnSysExMessageReceaved(object? sender, MidiInSysexMessageEventArgs e)
        {
            var data = e.SysexBytes;
            // MIDI-CI messages contain JSON → forward to CI manager
            if (data.Length > 5 && data[3] == 0x0D)
            {
                _ciManager?.HandleIncomingSysEx(data);
                return;
            }
            _stateManager.HandleIncomingSysEx(data);
        }

        public void SendSysEx(byte[] message)
        {
            _midiOut.SendBuffer(message);
            Console.WriteLine($"SysEx sent ({message.Length} bytes)");
        }

        public void RequestStateDump(byte[] requestMessage)
        {
            SendSysEx(requestMessage);
            Console.WriteLine("State dump request sent.");
        }

        public void SendStateDump(byte[] stateDump)
        {
            SendSysEx(stateDump);
            Console.WriteLine("State dump sent back to device.");
        }

        public void Dispose()
        {
            _midiIn?.Stop();
            _midiIn?.Dispose();
            _midiOut?.Dispose();
        }
    }

}
