using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace BuzzGUI.Common
{
    public static class IOExtensions
    {
        public static void WriteASCIIZString(this BinaryWriter bw, string s)
        {
            bw.Write(Encoding.ASCII.GetBytes(s));
            bw.Write((byte)0);
        }

        public static string ReadASCIIZString(this BinaryReader br)
        {
            var sb = new StringBuilder();

            while (true)
            {
                var b = br.ReadByte();
                if (b == 0) break;
                sb.Append((char)b);
            }

            return sb.ToString();
        }

        public static string ReadASCIIStringWithInt32Length(this BinaryReader br)
        {
            int len = br.ReadInt32();
            if (len < 0 || len > 65536) throw new Exception("invalid string");

            var sb = new StringBuilder(len);
            for (int i = 0; i < len; i++)
                sb.Append((char)br.ReadByte());

            return sb.ToString();
        }

        public static bool IsValidFileName(this string filename)
        {
            string invalid = new string(System.IO.Path.GetInvalidFileNameChars());
            var r = new Regex("[" + Regex.Escape(invalid) + "]");
            return !r.IsMatch(filename);
        }

        static byte[] ToByteArray(object x)
        {
            if (!x.GetType().IsValueType) throw new ArgumentException("'x' must be value type");
            byte[] bytes = new byte[Marshal.SizeOf(x)];
            var h = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            Marshal.StructureToPtr(x, h.AddrOfPinnedObject(), false);
            h.Free();
            return bytes;
        }

        static T FromByteArray<T>(byte[] bytes)
        {
            if (!typeof(T).IsValueType) throw new ArgumentException("T must be value type");
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            T x = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return x;
        }

        public static void WriteRaw(this BinaryWriter bw, object o)
        {
            bw.Write(ToByteArray(o));
        }

        public static T ReadRaw<T>(this BinaryReader br)
        {
            var bytes = new byte[Marshal.SizeOf(typeof(T))];
            br.Read(bytes, 0, bytes.Length);
            return FromByteArray<T>(bytes);
        }

    }
}
