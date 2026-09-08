using System;
using System.Collections.Generic;
using System.Text;

namespace ReBuzz.Midi
{
    public static class MidiCiMessages
    {
        private const byte SysExStart = 0xF0;
        private const byte SysExEnd = 0xF7;

        private const byte UniversalNonRealtime = 0x7E;
        private const byte MidiCiSubId = 0x0D;

        // --- 1. Discovery ---
        public static byte[] Discovery(byte deviceId = 0x7F)
        {
            return new byte[]
            {
            SysExStart,
            UniversalNonRealtime,
            deviceId,
            MidiCiSubId,
            0x01, // Discovery
            SysExEnd
            };
        }

        // --- 2. Profile Inquiry ---
        public static byte[] ProfileInquiry(byte deviceId = 0x7F)
        {
            return new byte[]
            {
            SysExStart,
            UniversalNonRealtime,
            deviceId,
            MidiCiSubId,
            0x03, // Profile Inquiry
            SysExEnd
            };
        }

        // --- 3. Property Exchange: Get Property Data ---
        public static byte[] PropertyGet(string resource, string property)
        {
            var json = $"{{\"resource\":\"{resource}\",\"property\":\"{property}\"}}";
            return BuildPropertyExchange(0x01, json); // 0x01 = Get
        }

        // --- 4. Property Exchange: Set Property Data ---
        public static byte[] PropertySet(string resource, string jsonPayload)
        {
            return BuildPropertyExchange(0x02, jsonPayload); // 0x02 = Set
        }

        // --- Helper: Build Property Exchange SysEx ---
        private static byte[] BuildPropertyExchange(byte operation, string json)
        {
            var jsonBytes = Encoding.UTF8.GetBytes(json);

            var msg = new List<byte>
        {
            SysExStart,
            UniversalNonRealtime,
            0x7F,        // device ID (broadcast)
            MidiCiSubId,
            0x0D,        // Property Exchange
            operation    // 0x01 = Get, 0x02 = Set
        };

            msg.AddRange(jsonBytes);
            msg.Add(SysExEnd);

            return msg.ToArray();
        }
    }
}
