using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WDE.AudioBlock.NoiseRemoval
{
    public class RNNoise
    {
        private bool is64Bit;

        public RNNoise()
        {
            is64Bit = IntPtr.Size == 8;
        }

        public void Create(string filename)
        {
            if (is64Bit) { RNNoiseInterop64.rnnoise_create_simple(filename); }
            else { RNNoiseInterop32.rnnoise_create_simple(filename); }
        }

        public void Destroy()
        {
            if (is64Bit) { RNNoiseInterop64.rnnoise_destroy_simple(); }
            else { RNNoiseInterop32.rnnoise_destroy_simple(); }
        }

        public void ProcessFrame(float[] input, float[] output)
        {
            if (is64Bit) { RNNoiseInterop64.rnnoise_process_frame_simple(output, input); }
            else { RNNoiseInterop32.rnnoise_process_frame_simple(output, input); }
        }
    }
}
