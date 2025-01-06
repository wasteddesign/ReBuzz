using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace libsndfile
{
    public enum FileMode
    {
        SFM_READ = 0x10,
        SFM_WRITE = 0x20,
        SFM_RDWR = 0x30
    };

    public enum ErrorValue
    {
        SF_ERR_NO_ERROR = 0,
        SF_ERR_UNRECOGNISED_FORMAT = 1,
        SF_ERR_SYSTEM = 2,
        SF_ERR_MALFORMED_FILE = 3,
        SF_ERR_UNSUPPORTED_ENCODING = 4
    };

    [Flags]
    public enum Format
    {   /* Major formats. */
        SF_FORMAT_WAV = 0x010000,       /* Microsoft WAV format (little endian default). */
        SF_FORMAT_AIFF = 0x020000,      /* Apple/SGI AIFF format (big endian). */
        SF_FORMAT_AU = 0x030000,        /* Sun/NeXT AU format (big endian). */
        SF_FORMAT_RAW = 0x040000,       /* RAW PCM data. */
        SF_FORMAT_PAF = 0x050000,       /* Ensoniq PARIS file format. */
        SF_FORMAT_SVX = 0x060000,       /* Amiga IFF / SVX8 / SV16 format. */
        SF_FORMAT_NIST = 0x070000,      /* Sphere NIST format. */
        SF_FORMAT_VOC = 0x080000,       /* VOC files. */
        SF_FORMAT_IRCAM = 0x0A0000,     /* Berkeley/IRCAM/CARL */
        SF_FORMAT_W64 = 0x0B0000,       /* Sonic Foundry's 64 bit RIFF/WAV */
        SF_FORMAT_MAT4 = 0x0C0000,      /* Matlab (tm) V4.2 / GNU Octave 2.0 */
        SF_FORMAT_MAT5 = 0x0D0000,      /* Matlab (tm) V5.0 / GNU Octave 2.1 */
        SF_FORMAT_PVF = 0x0E0000,       /* Portable Voice Format */
        SF_FORMAT_XI = 0x0F0000,        /* Fasttracker 2 Extended Instrument */
        SF_FORMAT_HTK = 0x100000,       /* HMM Tool Kit format */
        SF_FORMAT_SDS = 0x110000,       /* Midi Sample Dump Standard */
        SF_FORMAT_AVR = 0x120000,       /* Audio Visual Research */
        SF_FORMAT_WAVEX = 0x130000,     /* MS WAVE with WAVEFORMATEX */
        SF_FORMAT_SD2 = 0x160000,       /* Sound Designer 2 */
        SF_FORMAT_FLAC = 0x170000,      /* FLAC lossless file format */
        SF_FORMAT_CAF = 0x180000,       /* Core Audio File format */
        SF_FORMAT_WVE = 0x190000,       /* Psion WVE format */
        SF_FORMAT_OGG = 0x200000,       /* Xiph OGG container */
        SF_FORMAT_MPC2K = 0x210000,     /* Akai MPC 2000 sampler */
        SF_FORMAT_RF64 = 0x220000,      /* RF64 WAV file */
        SF_FORMAT_MPEG = 0x230000,      /* MPEG-1/2 audio stream */

        /* Subtypes from here on. */

        SF_FORMAT_PCM_S8 = 0x0001,      /* Signed 8 bit data */
        SF_FORMAT_PCM_16 = 0x0002,      /* Signed 16 bit data */
        SF_FORMAT_PCM_24 = 0x0003,      /* Signed 24 bit data */
        SF_FORMAT_PCM_32 = 0x0004,      /* Signed 32 bit data */

        SF_FORMAT_PCM_U8 = 0x0005,      /* Unsigned 8 bit data (WAV and RAW only) */

        SF_FORMAT_FLOAT = 0x0006,       /* 32 bit float data */
        SF_FORMAT_DOUBLE = 0x0007,      /* 64 bit float data */

        SF_FORMAT_ULAW = 0x0010,        /* U-Law encoded. */
        SF_FORMAT_ALAW = 0x0011,        /* A-Law encoded. */
        SF_FORMAT_IMA_ADPCM = 0x0012,       /* IMA ADPCM. */
        SF_FORMAT_MS_ADPCM = 0x0013,        /* Microsoft ADPCM. */

        SF_FORMAT_GSM610 = 0x0020,      /* GSM 6.10 encoding. */
        SF_FORMAT_VOX_ADPCM = 0x0021,       /* OKI / Dialogix ADPCM */

        SF_FORMAT_NMS_ADPCM_16 = 0x0022,        /* 16kbs NMS G721-variant encoding. */
        SF_FORMAT_NMS_ADPCM_24 = 0x0023,        /* 24kbs NMS G721-variant encoding. */
        SF_FORMAT_NMS_ADPCM_32 = 0x0024,        /* 32kbs NMS G721-variant encoding. */

        SF_FORMAT_G721_32 = 0x0030,     /* 32kbs G721 ADPCM encoding. */
        SF_FORMAT_G723_24 = 0x0031,     /* 24kbs G723 ADPCM encoding. */
        SF_FORMAT_G723_40 = 0x0032,     /* 40kbs G723 ADPCM encoding. */

        SF_FORMAT_DWVW_12 = 0x0040,         /* 12 bit Delta Width Variable Word encoding. */
        SF_FORMAT_DWVW_16 = 0x0041,         /* 16 bit Delta Width Variable Word encoding. */
        SF_FORMAT_DWVW_24 = 0x0042,         /* 24 bit Delta Width Variable Word encoding. */
        SF_FORMAT_DWVW_N = 0x0043,      /* N bit Delta Width Variable Word encoding. */

        SF_FORMAT_DPCM_8 = 0x0050,      /* 8 bit differential PCM (XI only) */
        SF_FORMAT_DPCM_16 = 0x0051,     /* 16 bit differential PCM (XI only) */

        SF_FORMAT_VORBIS = 0x0060,      /* Xiph Vorbis encoding. */
        SF_FORMAT_OPUS = 0x0064,        /* Xiph/Skype Opus encoding. */

        SF_FORMAT_ALAC_16 = 0x0070,     /* Apple Lossless Audio Codec (16 bit). */
        SF_FORMAT_ALAC_20 = 0x0071,     /* Apple Lossless Audio Codec (20 bit). */
        SF_FORMAT_ALAC_24 = 0x0072,     /* Apple Lossless Audio Codec (24 bit). */
        SF_FORMAT_ALAC_32 = 0x0073,     /* Apple Lossless Audio Codec (32 bit). */

        SF_FORMAT_MPEG_LAYER_I = 0x0080,        /* MPEG-1 Audio Layer I */
        SF_FORMAT_MPEG_LAYER_II = 0x0081,       /* MPEG-1 Audio Layer II */
        SF_FORMAT_MPEG_LAYER_III = 0x0082,      /* MPEG-2 Audio Layer III */

        /* Endian-ness options. */

        SF_ENDIAN_FILE = 0x00000000,    /* Default file endian-ness. */
        SF_ENDIAN_LITTLE = 0x10000000,  /* Force little endian-ness. */
        SF_ENDIAN_BIG = 0x20000000, /* Force big endian-ness. */
        SF_ENDIAN_CPU = 0x30000000, /* Force CPU endian-ness. */

        SF_FORMAT_SUBMASK = 0x0000FFFF,
        SF_FORMAT_TYPEMASK = 0x0FFF0000,
        SF_FORMAT_ENDMASK = 0x30000000
    };

    public enum Command
    {
        SFC_GET_LIB_VERSION = 0x1000,
        SFC_GET_LOG_INFO = 0x1001,
        SFC_GET_CURRENT_SF_INFO = 0x1002,


        SFC_GET_NORM_DOUBLE = 0x1010,
        SFC_GET_NORM_FLOAT = 0x1011,
        SFC_SET_NORM_DOUBLE = 0x1012,
        SFC_SET_NORM_FLOAT = 0x1013,
        SFC_SET_SCALE_FLOAT_INT_READ = 0x1014,
        SFC_SET_SCALE_INT_FLOAT_WRITE = 0x1015,

        SFC_GET_SIMPLE_FORMAT_COUNT = 0x1020,
        SFC_GET_SIMPLE_FORMAT = 0x1021,

        SFC_GET_FORMAT_INFO = 0x1028,

        SFC_GET_FORMAT_MAJOR_COUNT = 0x1030,
        SFC_GET_FORMAT_MAJOR = 0x1031,
        SFC_GET_FORMAT_SUBTYPE_COUNT = 0x1032,
        SFC_GET_FORMAT_SUBTYPE = 0x1033,

        SFC_CALC_SIGNAL_MAX = 0x1040,
        SFC_CALC_NORM_SIGNAL_MAX = 0x1041,
        SFC_CALC_MAX_ALL_CHANNELS = 0x1042,
        SFC_CALC_NORM_MAX_ALL_CHANNELS = 0x1043,
        SFC_GET_SIGNAL_MAX = 0x1044,
        SFC_GET_MAX_ALL_CHANNELS = 0x1045,

        SFC_SET_ADD_PEAK_CHUNK = 0x1050,

        SFC_UPDATE_HEADER_NOW = 0x1060,
        SFC_SET_UPDATE_HEADER_AUTO = 0x1061,

        SFC_FILE_TRUNCATE = 0x1080,

        SFC_SET_RAW_START_OFFSET = 0x1090,

        /* Commands reserved for dithering, which is not implemented. */
        SFC_SET_DITHER_ON_WRITE = 0x10A0,
        SFC_SET_DITHER_ON_READ = 0x10A1,

        SFC_GET_DITHER_INFO_COUNT = 0x10A2,
        SFC_GET_DITHER_INFO = 0x10A3,

        SFC_GET_EMBED_FILE_INFO = 0x10B0,

        SFC_SET_CLIPPING = 0x10C0,
        SFC_GET_CLIPPING = 0x10C1,

        SFC_GET_CUE_COUNT = 0x10CD,
        SFC_GET_CUE = 0x10CE,
        SFC_SET_CUE = 0x10CF,

        SFC_GET_INSTRUMENT = 0x10D0,
        SFC_SET_INSTRUMENT = 0x10D1,

        SFC_GET_LOOP_INFO = 0x10E0,

        SFC_GET_BROADCAST_INFO = 0x10F0,
        SFC_SET_BROADCAST_INFO = 0x10F1,

        SFC_GET_CHANNEL_MAP_INFO = 0x1100,
        SFC_SET_CHANNEL_MAP_INFO = 0x1101,

        SFC_RAW_DATA_NEEDS_ENDSWAP = 0x1110,

        /* Support for Wavex Ambisonics Format */
        SFC_WAVEX_SET_AMBISONIC = 0x1200,
        SFC_WAVEX_GET_AMBISONIC = 0x1201,

        /*
        ** RF64 files can be set so that on-close, writable files that have less
        ** than 4GB of data in them are converted to RIFF/WAV, as per EBU
        ** recommendations.
        */
        SFC_RF64_AUTO_DOWNGRADE = 0x1210,

        SFC_SET_VBR_ENCODING_QUALITY = 0x1300,
        SFC_SET_COMPRESSION_LEVEL = 0x1301,

        /* Ogg format commands */
        SFC_SET_OGG_PAGE_LATENCY_MS = 0x1302,
        SFC_SET_OGG_PAGE_LATENCY = 0x1303,
        SFC_GET_OGG_STREAM_SERIALNO = 0x1306,

        SFC_GET_BITRATE_MODE = 0x1304,
        SFC_SET_BITRATE_MODE = 0x1305,

        /* Cart Chunk support */
        SFC_SET_CART_INFO = 0x1400,
        SFC_GET_CART_INFO = 0x1401,

        /* Opus files original samplerate metadata */
        SFC_SET_ORIGINAL_SAMPLERATE = 0x1500,
        SFC_GET_ORIGINAL_SAMPLERATE = 0x1501,

        /* Following commands for testing only. */
        SFC_TEST_IEEE_FLOAT_REPLACE = 0x6001,

        /*
        ** These SFC_SET_ADD_* values are deprecated and will disappear at some
        ** time in the future. They are guaranteed to be here up to and
        ** including version 1.0.8 to avoid breakage of existing software.
        ** They currently do nothing and will continue to do nothing.
        */
        SFC_SET_ADD_HEADER_PAD_CHUNK = 0x1051,

        SFC_SET_ADD_DITHER_ON_WRITE = 0x1070,
        SFC_SET_ADD_DITHER_ON_READ = 0x1071
    };

    public enum LoopMode
    {
        SF_LOOP_NONE = 800,
        SF_LOOP_FORWARD,
        SF_LOOP_BACKWARD,
        SF_LOOP_ALTERNATING
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct SF_INFO
    {
        public long frames;
        public int samplerate;
        public int channels;
        public Format format;
        public int sections;
        public int seekable;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SF_FORMAT_INFO
    {
        public int format;
        readonly IntPtr name;
        readonly IntPtr extension;

        public string Name { get { return Marshal.PtrToStringAnsi(name); } }
        public string Extension { get { return Marshal.PtrToStringAnsi(extension); } }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SF_INSTRUMENT
    {
        [CLSCompliant(false)]
        [StructLayout(LayoutKind.Sequential)]
        public struct Loop
        {
            public LoopMode mode;
            public uint start;
            public uint end;
            public uint count;
        }

        public int gain;
        public byte basenote, detune;
        public byte velocity_lo, velocity_hi;
        public byte key_lo, key_hi;
        public int loop_count;

        [CLSCompliant(false)]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public Loop[] loops;

    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SF_VIRTUAL_IO
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate long sf_vio_get_filelen(IntPtr user_data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate long sf_vio_seek(long offset, int whence, IntPtr user_data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate long sf_vio_read(IntPtr ptr, long count, IntPtr user_data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate long sf_vio_write(IntPtr ptr, long count, IntPtr user_data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate long sf_vio_tell(IntPtr user_data);

        public sf_vio_get_filelen get_filelen;
        public sf_vio_seek seek;
        public sf_vio_read read;
        public sf_vio_write write;
        public sf_vio_tell tell;
    }


    public class SoundFile : IDisposable
    {
        [DllImport("libsndfile-1", CallingConvention = CallingConvention.Cdecl)]
        extern static IntPtr sf_open(string path, FileMode mode, ref SF_INFO sfinfo);

        [DllImport("libsndfile-1", CallingConvention = CallingConvention.Cdecl)]
        extern static IntPtr sf_open_virtual(ref SF_VIRTUAL_IO sfvirtual, FileMode mode, ref SF_INFO sfinfo, IntPtr user_data);

        [DllImport("libsndfile-1", CallingConvention = CallingConvention.Cdecl)]
        extern static ErrorValue sf_close(IntPtr sndfile);

        [DllImport("libsndfile-1", CallingConvention = CallingConvention.Cdecl)]
        extern static int sf_error(IntPtr sndfile);

        [DllImport("libsndfile-1", CallingConvention = CallingConvention.Cdecl)]
        extern static IntPtr sf_strerror(IntPtr sndfile);

        [DllImport("libsndfile-1", CallingConvention = CallingConvention.Cdecl)]
        extern static long sf_readf_float(IntPtr sndfile, [Out] float[] ptr, long frames);

        [DllImport("libsndfile-1", CallingConvention = CallingConvention.Cdecl)]
        extern static long sf_writef_float(IntPtr sndfile, IntPtr ptr, long frames);

        [DllImport("libsndfile-1", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sf_command")]
        extern static int sf_command_int(IntPtr sndfile, Command command, ref int data, int datasize);

        [DllImport("libsndfile-1", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sf_command")]
        extern static int sf_command_SF_FORMAT_INFO(IntPtr sndfile, Command command, ref SF_FORMAT_INFO data, int datasize);

        [DllImport("libsndfile-1", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sf_command")]
        extern static int sf_command_SF_INSTRUMENT(IntPtr sndfile, Command command, ref SF_INSTRUMENT data, int datasize);

        [DllImport("libsndfile-1", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sf_command")]
        extern static int sf_command_StringBuilder(IntPtr sndfile, Command command, StringBuilder data, int datasize);


        IntPtr sndfile;
        SF_INFO sfinfo;
        SF_VIRTUAL_IO sfvirtual;
        GCHandle sfvirtualHandle;
        GCHandle streamHandle;

        public SF_INFO Info { get { return sfinfo; } }
        public long FrameCount { get { return sfinfo.frames; } }
        public int SampleRate { get { return sfinfo.samplerate; } }
        public int ChannelCount { get { return sfinfo.channels; } }
        public Format Format { get { return sfinfo.format; } }
        public int SectionCount { get { return sfinfo.sections; } }
        public bool IsSeekable { get { return sfinfo.seekable != 0; } }

        public SoundFile(string path, FileMode mode, SF_INFO sfinfo)
        {
            this.sfinfo = sfinfo;
            sndfile = sf_open(path, mode, ref this.sfinfo);
            if (sndfile == IntPtr.Zero) throw new Exception(Marshal.PtrToStringAnsi(sf_strerror(IntPtr.Zero)));
        }

        public SoundFile(Stream stream, FileMode mode, SF_INFO sfinfo)
        {
            sfvirtualHandle = GCHandle.Alloc(sfvirtual);
            streamHandle = GCHandle.Alloc(stream);

            sfvirtual.get_filelen = stream_get_filelen;
            sfvirtual.seek = stream_seek;
            sfvirtual.read = stream_read;
            sfvirtual.write = stream_write;
            sfvirtual.tell = stream_tell;

            this.sfinfo = sfinfo;
            sndfile = sf_open_virtual(ref sfvirtual, mode, ref this.sfinfo, GCHandle.ToIntPtr(streamHandle));
            if (sndfile == IntPtr.Zero) throw new Exception(Marshal.PtrToStringAnsi(sf_strerror(IntPtr.Zero)));
        }

        public static SoundFile OpenRead(string path) { return new SoundFile(path, FileMode.SFM_READ, new SF_INFO()); }
        public static SoundFile OpenRead(Stream stream) { return new SoundFile(stream, FileMode.SFM_READ, new SF_INFO()); }

        public static SoundFile Create(string path, int samplerate, int channels, Format format)
        {
            var sfinfo = new SF_INFO();
            sfinfo.samplerate = samplerate;
            sfinfo.channels = channels;
            sfinfo.format = format;
            return new SoundFile(path, FileMode.SFM_WRITE, sfinfo);
        }

        public static SoundFile Create(Stream stream, int samplerate, int channels, Format format)
        {
            var sfinfo = new SF_INFO();
            sfinfo.samplerate = samplerate;
            sfinfo.channels = channels;
            sfinfo.format = format;
            return new SoundFile(stream, FileMode.SFM_WRITE, sfinfo);
        }

        public void Close()
        {
            if (sndfile != IntPtr.Zero)
            {
                var res = sf_close(sndfile);
                if (res != ErrorValue.SF_ERR_NO_ERROR) throw new Exception(res.ToString());
                sndfile = IntPtr.Zero;
                sfinfo = new SF_INFO();

            }

            if (sfvirtualHandle.IsAllocated) sfvirtualHandle.Free();
            if (streamHandle.IsAllocated) streamHandle.Free();

        }

        public void Dispose()
        {
            Close();
        }

        public long ReadFloat(float[] output, long framecount)
        {
            if (output.Length < framecount * ChannelCount) throw new ArgumentException("output.Length < framecount * ChannelCount");
            return sf_readf_float(sndfile, output, framecount);
        }

        public long WriteFloat(float[] input, int offset, long framecount)
        {
            if (input.Length - offset < framecount * ChannelCount) throw new ArgumentException("input.Length - offset < framecount * ChannelCount");
            var gch = GCHandle.Alloc(input);
            var r = sf_writef_float(sndfile, Marshal.UnsafeAddrOfPinnedArrayElement(input, offset), framecount);
            gch.Free();
            return r;
        }

        public static int SimpleFormatCount
        {
            get
            {
                int count = 0;
                sf_command_int(IntPtr.Zero, Command.SFC_GET_SIMPLE_FORMAT_COUNT, ref count, 4);
                return count;
            }
        }

        public static int FormatMajorCount
        {
            get
            {
                int count = 0;
                sf_command_int(IntPtr.Zero, Command.SFC_GET_FORMAT_MAJOR_COUNT, ref count, 4);
                return count;
            }
        }

        public static SF_FORMAT_INFO GetSimpleFormat(int format)
        {
            var fi = new SF_FORMAT_INFO();
            fi.format = format;
            var res = sf_command_SF_FORMAT_INFO(IntPtr.Zero, Command.SFC_GET_SIMPLE_FORMAT, ref fi, 3 * IntPtr.Size);
            return fi;
        }

        public static SF_FORMAT_INFO GetFormatMajor(int format)
        {
            var fi = new SF_FORMAT_INFO();
            fi.format = format;
            var res = sf_command_SF_FORMAT_INFO(IntPtr.Zero, Command.SFC_GET_FORMAT_MAJOR, ref fi, 3 * IntPtr.Size);
            return fi;
        }

        public static IEnumerable<SF_FORMAT_INFO> SimpleFormats
        {
            get
            {
                int count = SimpleFormatCount;
                for (int i = 0; i < count; i++)
                    yield return GetSimpleFormat(i);
            }
        }

        public static IEnumerable<SF_FORMAT_INFO> FormatsMajor
        {
            get
            {
                int count = FormatMajorCount;
                for (int i = 0; i < count; i++)
                    yield return GetFormatMajor(i);
            }
        }

        public string LogInfo
        {
            get
            {
                var sb = new StringBuilder(65536);
                sf_command_StringBuilder(sndfile, Command.SFC_GET_LOG_INFO, sb, sb.Capacity);
                return sb.ToString();
            }
        }

        public SF_INSTRUMENT Instrument
        {
            get
            {
                var i = new SF_INSTRUMENT();
                var res = sf_command_SF_INSTRUMENT(sndfile, Command.SFC_GET_INSTRUMENT, ref i, Marshal.SizeOf(i));
                if (res == 0)
                {
                    i.basenote = 60;
                    i.velocity_hi = 127;
                    i.key_hi = 127;
                }
                return i;
            }
        }

        public bool Clipping
        {
            get
            {
                int data = 0;
                return sf_command_int(sndfile, Command.SFC_GET_CLIPPING, ref data, 0) != 0;
            }
            set
            {
                int data = 0;
                sf_command_int(sndfile, Command.SFC_SET_CLIPPING, ref data, value ? 1 : 0);
            }
        }

        #region Stream IO

        static long stream_get_filelen(IntPtr user_data)
        {
            var s = GCHandle.FromIntPtr(user_data).Target as Stream;
            return s.Length;
        }

        static long stream_seek(long offset, int whence, IntPtr user_data)
        {
            var s = GCHandle.FromIntPtr(user_data).Target as Stream;
            switch (whence)
            {
                case 0: return s.Seek(offset, SeekOrigin.Begin);
                case 1: return s.Seek(offset, SeekOrigin.Current);
                case 2: return s.Seek(offset, SeekOrigin.End);
            }
            return 0;
        }

        static long stream_read(IntPtr ptr, long count, IntPtr user_data)
        {
            var s = GCHandle.FromIntPtr(user_data).Target as Stream;

            var buffer = new byte[count];
            int bytesread = s.Read(buffer, 0, buffer.Length);
            Marshal.Copy(buffer, 0, ptr, bytesread);

            return bytesread;
        }

        static long stream_write(IntPtr ptr, long count, IntPtr user_data)
        {
            var s = GCHandle.FromIntPtr(user_data).Target as Stream;

            var buffer = new byte[count];
            Marshal.Copy(ptr, buffer, 0, (int)count);
            s.Write(buffer, 0, buffer.Length);

            return count;
        }

        static long stream_tell(IntPtr user_data)
        {
            var s = GCHandle.FromIntPtr(user_data).Target as Stream;
            return s.Position;
        }

        #endregion

    }
}
