using System;
using System.Runtime.InteropServices;

namespace WDE.AudioBlock.r8brain
{
    public enum ER8BResamplerRes
    {
        r8brr16 = 0, ///< 16-bit precision resampler.
                     ///<
        r8brr16IR = 1, ///< 16-bit precision resampler for impulse responses.
                       ///<
        r8brr24 = 2 ///< 24-bit precision resampler (including 32-bit floating
                    ///< point).
                    ///<
    };

    class R8brain : IDisposable
    {
        private IntPtr handle;
        private readonly bool is64Bit;

        public R8brain()
        {
            is64Bit = IntPtr.Size == 8;

        }

        public void Dispose()
        {
            Delete();
        }

        public void Create(double SrcSampleRate,
            double DstSampleRate, int MaxInLen, double ReqTransBand, ER8BResamplerRes Res)
        {
            handle = is64Bit ? R8brainInterop64.r8b_create(SrcSampleRate, DstSampleRate, MaxInLen, ReqTransBand, Res) :
                R8brainInterop32.r8b_create(SrcSampleRate, DstSampleRate, MaxInLen, ReqTransBand, Res);
        }

        public void Delete()
        {
            if (is64Bit)
                R8brainInterop64.r8b_delete(handle);
            else
                R8brainInterop32.r8b_delete(handle);
        }

        public void Clear()
        {
            if (handle != IntPtr.Zero)
            {
                if (is64Bit)
                    R8brainInterop64.r8b_clear(handle);
                else
                    R8brainInterop32.r8b_clear(handle);
            }
        }

        public int Process(double[] ip0, int l, out double[] op0)
        {
            int outputSize = 0;
            op0 = null;

            if (handle != IntPtr.Zero)
            {
                IntPtr op0_ptr;

                if (is64Bit)
                    outputSize = R8brainInterop64.r8b_process(handle, ip0, l, out op0_ptr);
                else
                    outputSize = R8brainInterop32.r8b_process(handle, ip0, l, out op0_ptr);

                op0 = new double[outputSize];

                if (op0_ptr != IntPtr.Zero)
                {
                    // Access voilation if drawing graph and playing a the same time.
                    Marshal.Copy(op0_ptr, op0, 0, outputSize);
                }
            }

            return outputSize;
        }
    }
}
