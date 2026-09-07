using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ReBuzz.Midi
{
    public class MidiSynthStateManager
    {
        private byte[] _lastStateDump;

        public event Action<byte[]> StateReceived;

        public bool HasState => _lastStateDump != null;

        public void HandleIncomingSysEx(byte[] data)
        {
            _lastStateDump = data;
            StateReceived?.Invoke(data);
            Console.WriteLine($"State dump received ({data.Length} bytes)");
        }

        public byte[] GetStoredState() => _lastStateDump;

        public void ClearState() => _lastStateDump = null;

        // --- Persistence ---

        public void SaveStateToFile(string path)
        {
            if (_lastStateDump == null)
            {
                Console.WriteLine("No state to save.");
                return;
            }

            File.WriteAllBytes(path, _lastStateDump);
            Console.WriteLine($"State saved to {path}");
        }

        public bool LoadStateFromFile(string path)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"State file not found: {path}");
                return false;
            }

            _lastStateDump = File.ReadAllBytes(path);
            Console.WriteLine($"State loaded from {path} ({_lastStateDump.Length} bytes)");
            return true;
        }
    }
}
