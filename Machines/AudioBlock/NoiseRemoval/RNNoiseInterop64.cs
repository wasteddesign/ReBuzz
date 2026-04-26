using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace WDE.AudioBlock.NoiseRemoval
{
    internal class RNNoiseInterop64
    {
        private const string RNNoiseDllName = "rnnoise64.dll";

        [DllImport(RNNoiseDllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rnnoise_get_size();

        [DllImport(RNNoiseDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void rnnoise_create_simple([MarshalAs(UnmanagedType.LPStr)] string filename);

        [DllImport(RNNoiseDllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void rnnoise_destroy_simple();

        [DllImport(RNNoiseDllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern float rnnoise_process_frame_simple([MarshalAs(UnmanagedType.LPArray)] float[] output, [MarshalAs(UnmanagedType.LPArray)] float[] input);
    }
}
