using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace SysExTests
{
    public class MidiCiStateManager
    {
        public event Action<JsonDocument> StateReceived;

        public void HandleIncomingSysEx(byte[] data)
        {
            // MIDI-CI Property Exchange messages contain JSON after the header
            int jsonStart = Array.IndexOf(data, (byte)'{');
            int jsonEnd = Array.LastIndexOf(data, (byte)'}');

            if (jsonStart < 0 || jsonEnd < 0)
                return;

            var jsonBytes = new byte[jsonEnd - jsonStart + 1];
            Array.Copy(data, jsonStart, jsonBytes, 0, jsonBytes.Length);

            var json = Encoding.UTF8.GetString(jsonBytes);

            try
            {
                var doc = JsonDocument.Parse(json);
                StateReceived?.Invoke(doc);
                Console.WriteLine("MIDI-CI state received.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"JSON parse error: {ex.Message}");
            }
        }
    }
}
