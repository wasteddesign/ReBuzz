using System;
using System.Runtime.InteropServices;

namespace BuzzGUI.WaveformControl.r8brain
{
    class R8brainInterop64
    {
        private const string R8brainDllName = "r8bsrc64.dll";

        [DllImport(R8brainDllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr r8b_create(double SrcSampleRate,
            double DstSampleRate, int MaxInLen, double ReqTransBand, ER8BResamplerRes Res);

        [DllImport(R8brainDllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void r8b_delete(IntPtr rs);

        [DllImport(R8brainDllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void r8b_clear(IntPtr rs);

        [DllImport(R8brainDllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int r8b_process(IntPtr rs, [MarshalAs(UnmanagedType.LPArray)] double[] ip0, int l, out IntPtr op0);

    }
}
