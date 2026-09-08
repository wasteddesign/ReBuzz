using System;
using System.Collections.Generic;
using System.Text;

namespace SysExTests
{
    public static class SysExRequests
    {
        // --- Manufacturer IDs (1-byte) ---
        public const byte IdSequential = 0x01;
        public const byte IdRoland = 0x41;
        public const byte IdKorg = 0x42;
        public const byte IdYamaha = 0x43;
        public const byte IdAkai = 0x47;

        // 3-byte IDs (start with 0x00)
        public static readonly byte[] IdNativeInstruments = { 0x00, 0x21, 0x09 };
        public static readonly byte[] IdElektron = { 0x00, 0x20, 0x3C };
        public static readonly byte[] IdArturia = { 0x00, 0x20, 0x6B };
        public static readonly byte[] IdNovation = { 0x00, 0x20, 0x29 };
        public static readonly byte[] IdKurzweil = { 0x00, 0x20, 0x0A };
        public static readonly byte[] IdClaviaNord = { 0x00, 0x20, 0x33 };
        public static readonly byte[] IdMAudio = { 0x00, 0x01, 0x05 }; // common M‑Audio ID

        // --- Universal SysEx ---

        // MIDI Identity Request (works on almost everything)
        public static readonly byte[] UniversalIdentityRequest = {
        0xF0, 0x7E, 0x7F, 0x06, 0x01, 0xF7
    };

        // Generic “bulk dump request” template for unknown devices
        public static byte[] GenericDumpRequest(byte manufacturerId, params byte[] payload)
        {
            var msg = new List<byte> { 0xF0, manufacturerId };
            msg.AddRange(payload);
            msg.Add(0xF7);
            return msg.ToArray();
        }

        public static byte[] GenericDumpRequest3(byte[] manufacturerId3, params byte[] payload)
        {
            var msg = new List<byte> { 0xF0 };
            msg.AddRange(manufacturerId3);
            msg.AddRange(payload);
            msg.Add(0xF7);
            return msg.ToArray();
        }

        // --- Roland (0x41) ---

        // RQ1: Data Request (used for bulk/state reads)
        public static byte[] RolandRQ1(byte deviceId, byte modelId, int address, int size)
        {
            // NOTE: real Roland messages include checksum; this is a simplified template.
            return new byte[] {
            0xF0, 0x41, deviceId, modelId, 0x11,
            (byte)((address >> 16) & 0x7F),
            (byte)((address >> 8) & 0x7F),
            (byte)(address & 0x7F),
            (byte)((size >> 16) & 0x7F),
            (byte)((size >> 8) & 0x7F),
            (byte)(size & 0x7F),
            0xF7
        };
        }

        // Example: “full setup/state” request for a digital piano (address/size are device‑specific)
        public static byte[] RolandFullStateRequest(byte deviceId, byte modelId)
            => RolandRQ1(deviceId, modelId, address: 0x01000100, size: 256);

        // --- Yamaha (0x43) ---

        // Bulk Dump Request (DX‑style template)
        public static byte[] YamahaBulkDumpRequest(byte deviceNumber, byte format, byte byteCount)
        {
            return new byte[] {
            0xF0, 0x43, deviceNumber, format, byteCount, 0xF7
        };
        }

        // Example: request current voice/patch
        public static readonly byte[] YamahaVoiceDumpRequest = {
        0xF0, 0x43, 0x00, 0x00, 0x7F, 0xF7
    };

        // --- Korg (0x42) ---

        // Generic Korg dump request (Volca / Minilogue‑style template)
        public static byte[] KorgDumpRequest(byte globalChannel, byte mode, byte dataType)
        {
            return new byte[] {
            0xF0, 0x42, globalChannel, mode, dataType, 0xF7
        };
        }

        // Example: request current program
        public static readonly byte[] KorgProgramDumpRequest = {
        0xF0, 0x42, 0x30, 0x00, 0x01, 0x00, 0xF7
    };

        // --- Akai (0x47) ---

        public static byte[] AkaiDumpRequest(byte deviceId, byte modelId, byte section)
        {
            return new byte[] {
            0xF0, 0x47, deviceId, modelId, section, 0xF7
        };
        }

        // Example: MPC project/state dump request (section is device‑specific)
        public static readonly byte[] AkaiProjectDumpRequest = {
        0xF0, 0x47, 0x00, 0x01, 0x10, 0xF7
    };

        // --- Sequential / DSI (0x01) ---

        public static readonly byte[] DsiProgramDumpRequest = {
        0xF0, 0x01, 0x0A, 0x02, 0xF7
    };

        public static readonly byte[] DsiGlobalDumpRequest = {
        0xF0, 0x01, 0x0A, 0x01, 0xF7
    };

        // --- Native Instruments (Maschine / Komplete Kontrol) ---

        public static byte[] NativeInstrumentsStateRequest()
            => GenericDumpRequest3(IdNativeInstruments, 0x01); // payload is device‑specific

        // --- Elektron (Digitakt, Octatrack, etc.) ---

        public static byte[] ElektronProjectDumpRequest()
            => GenericDumpRequest3(IdElektron, 0x01); // project/state request

        // --- Arturia (MicroFreak, MatrixBrute, etc.) ---

        public static byte[] ArturiaStateDumpRequest()
            => GenericDumpRequest3(IdArturia, 0x01);

        // --- Novation (Circuit, Peak, etc.) ---

        public static byte[] NovationStateDumpRequest()
            => GenericDumpRequest3(IdNovation, 0x01);

        // --- Kurzweil ---

        public static byte[] KurzweilStateDumpRequest()
            => GenericDumpRequest3(IdKurzweil, 0x01);

        // --- Clavia / Nord ---

        public static byte[] NordProgramDumpRequest()
            => GenericDumpRequest3(IdClaviaNord, 0x01);

        // --- M‑Audio keyboards / controllers ---

        // Many M‑Audio keyboards are controllers and don’t expose full “state dumps”,
        // but you can still:
        //  - identify the device
        //  - request any vendor‑specific state if the model supports it.
        public static byte[] MAudioIdentityRequest()
            => GenericDumpRequest3(IdMAudio, 0x01);

        public static byte[] MAudioStateDumpRequest()
            => GenericDumpRequest3(IdMAudio, 0x02); // placeholder; model‑specific in practice

        // --- Helper: choose a “best guess” request by manufacturer name ---

        public static byte[] GetDefaultStateRequestForManufacturer(string manufacturer)
        {
            manufacturer = manufacturer?.ToLowerInvariant() ?? "";

            if (manufacturer.Contains("roland")) return RolandFullStateRequest(0x10, 0x00);
            if (manufacturer.Contains("yamaha")) return YamahaVoiceDumpRequest;
            if (manufacturer.Contains("korg")) return KorgProgramDumpRequest;
            if (manufacturer.Contains("akai")) return AkaiProjectDumpRequest;
            if (manufacturer.Contains("sequential") ||
                manufacturer.Contains("dave smith")) return DsiProgramDumpRequest;
            if (manufacturer.Contains("native")) return NativeInstrumentsStateRequest();
            if (manufacturer.Contains("elektron")) return ElektronProjectDumpRequest();
            if (manufacturer.Contains("arturia")) return ArturiaStateDumpRequest();
            if (manufacturer.Contains("novation")) return NovationStateDumpRequest();
            if (manufacturer.Contains("kurzweil")) return KurzweilStateDumpRequest();
            if (manufacturer.Contains("nord") ||
                manufacturer.Contains("clavia")) return NordProgramDumpRequest();
            if (manufacturer.Contains("m-audio") ||
                manufacturer.Contains("maudio") /*||
                manufacturer.Contains("oxygen")*/) return MAudioStateDumpRequest();

            // Fallback: universal identity request
            return UniversalIdentityRequest;
        }
    }
}
