using BuzzGUI.Interfaces;
using System;
using System.ComponentModel;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace ReBuzz.Core
{
    internal unsafe class WaveLayerCore : IWaveLayer
    {
        internal string mappedFileId;

        //private float[] buffer;

        // mappedFile if needed to share to native machines
        private MemoryMappedFile mappedFile;
        internal MemoryMappedViewAccessor accessor;
        internal byte* basePointer;

        public WaveLayerCore(WaveCore wave, string path, WaveFormat format, int root, bool stereo, int size16Bit)
        {
            this.Wave = wave;
            Init(path, format, root, stereo, size16Bit);
            LoopEnd16Bit = size16Bit;
            LoopStart16Bit = 0;
        }

        public void Init(string path, WaveFormat format, int root, bool stereo, int size16Bit)
        {
            Path = path;
            WaveFormatData = (int)format;
            RootNote = root;
            ChannelCount = stereo ? 2 : 1;

            SampleCount16Bit = size16Bit;

            bufferSize = SampleCount16Bit * 2 * ChannelCount;

            CreateBuffer();

            if (format != WaveFormat.Int16)
            {
                extended = true;
                WaveFormatData = (int)format;
            }
        }

        public void Init(string path, int root, bool stereo, int size16Bit)
        {
            Path = path;
            RootNote = root;
            ChannelCount = stereo ? 2 : 1;

            SampleCount16Bit = size16Bit;

            bufferSize = SampleCount16Bit * 2 * ChannelCount;

            CreateBuffer();
        }

        public void Release()
        {
            if (mappedFile != null)
            {
                mappedFile.Dispose();
                mappedFile = null;
            }
            mappedFileId = null;
            basePointer = null;
            RawSamples = IntPtr.Zero;
        }

        internal void CreateBuffer()
        {
            Release();

            if (bufferSize > 0)
            {
                mappedFileId = "WaveLayer_" + DateTime.Now.Ticks;
                mappedFile = MemoryMappedFile.CreateNew(mappedFileId, bufferSize);
                accessor = mappedFile.CreateViewAccessor();

                mappedFile.CreateViewAccessor().SafeMemoryMappedViewHandle.AcquirePointer(ref basePointer);
                RawSamples = (IntPtr)basePointer;
            }
        }

        public WaveLayerCore()
        {
            Path = "";
            RawSamples = IntPtr.Zero;
        }

        public WaveCore Wave { get; set; }
        public string Path { get; private set; }

        public IntPtr RawSamples { get; private set; }

        public int WaveFormatData { get; set; }
        public WaveFormat Format
        {
            get
            {
                if (IsExtended())
                {
                    int num4 = WaveFormatData;
                    if (num4 != 1)
                    {
                        if (num4 != 2)
                        {
                            return (num4 == 3) ? WaveFormat.Int24 : WaveFormat.Int16;
                        }
                        return WaveFormat.Int32;
                    }
                    return WaveFormat.Float32;
                }
                return WaveFormat.Int16;
            }
        }


        readonly int sampleCount = 0;
        public int SampleCount { get => AdjustSampleCount( SampleCount16Bit ); }
        public int RootNote { get; set; }
        public int SampleRate { get; set; }
        public int LoopStart { get => AdjustSampleCount(LoopStart16Bit); set => LoopStart16Bit = ReverseAdjustSampleCount(value); }
        public int LoopEnd { get => AdjustSampleCount(LoopEnd16Bit); set => LoopEnd16Bit = ReverseAdjustSampleCount(value); }

        public int ChannelCount { get; set; }
        public int SampleCount16Bit { get; internal set; }

        // Needs to be public because of WaveControl and Reflection...
        public int layerIndex;
        public int LayerIndex { get => layerIndex; set => layerIndex = value; }
        internal int LoopEnd16Bit { get; set; }
        internal int LoopStart16Bit { get; set; }

        
        internal bool extended;
        public bool Extended { get => extended;
            internal set
            {
                extended = value;
                if (extended)
                    WaveFormatData = basePointer[0];
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void GetDataAsFloat(float[] output, int outoffset, int outstride, int channel, int offset, int count)
        {
            if (accessor == null)
                return;

            if (channel >= ChannelCount)
                channel = 0;

            int j = outoffset;
            byte* buffer = (byte*)basePointer;
            int bytesPerSample = 2;
            float div = 1 / 32768.0f;
            int offsetAddBytes = 0;
            if (IsExtended())
            {
                offsetAddBytes = 8;
                bytesPerSample = GetBitsPerSample() / 8;
                switch (Format)
                {
                    case WaveFormat.Int24:
                        div /= 256;
                        break;
                    case WaveFormat.Int32:
                        div /= 65536;
                        break;
                }
            }

            //offset = ReverseAdjustSampleCount(offset);
            
            fixed ( float* dest = output)
            {
                int bufferIndex = (ChannelCount * offset + channel) * bytesPerSample + offsetAddBytes;
                for (int i = 0; i < count; i++)
                {
                    if (j >= output.Length)
                        return;

                    if (bufferIndex < 0 || bufferIndex >= bufferSize)
                        dest[j] = 0;
                    else
                    {
                        switch (Format)
                        {
                            case WaveFormat.Int16:
                                {
                                    short sample = Unsafe.ReadUnaligned<short>(ref buffer[bufferIndex]);
                                    dest[j] = sample * div;
                                }
                                break;
                            case WaveFormat.Int24:
                                {
                                    int sample = (buffer[bufferIndex] << 8 | buffer[bufferIndex + 1] << 16 | buffer[bufferIndex + 2] << 24) >> 8;
                                    dest[j] = sample * div;
                                }
                                break;
                            case WaveFormat.Int32:
                                {
                                    int sample = Unsafe.ReadUnaligned<int>(ref buffer[bufferIndex]);
                                    dest[j] = sample * div;
                                }
                                break;
                            case WaveFormat.Float32:
                                {
                                    float sample = BitConverter.Int32BitsToSingle(Unsafe.ReadUnaligned<int>(ref buffer[bufferIndex]));
                                    dest[j] = sample;
                                }
                                break;
                        }
                    }
                            
                    j += outstride;
                    bufferIndex += ChannelCount * bytesPerSample;
                }
            }
        }

        public void InvalidateData()
        {
            Wave.Invalidate();
        }

        public void SetDataAsFloat(float[] input, int inoffset, int instride, int channel, int offset, int count)
        {
            if (accessor == null)
                return;

            if (channel >= ChannelCount)
                channel = 0;

            int j = inoffset;
            byte* buffer = basePointer;
            int bytesPerSample = 2;
            float mul = 32768.0f;
            int offsetAddBytes = 0;
            if (IsExtended())
            {
                offsetAddBytes = 8;
                bytesPerSample = GetBitsPerSample() / 8;
                switch (Format)
                {
                    case WaveFormat.Int24:
                        mul *= 256;
                        break;
                    case WaveFormat.Int32:
                        mul *= 65536;
                        break;
                }
            }

            fixed (float* source = input)
            {
                int bufferIndex = (ChannelCount * offset + channel) * bytesPerSample + offsetAddBytes;

                for (int i = 0; i < count; i++)
                {
                    if (j >= input.Length)
                        return;

                    if (bufferIndex < 0 || bufferIndex + bytesPerSample >= bufferSize)
                        return;
                    else
                    {
                        switch (Format)
                        {
                            case WaveFormat.Int16:
                                {
                                    short sample = (short)(source[j] * mul);
                                    Unsafe.WriteUnaligned(ref buffer[bufferIndex], sample);
                                }
                                break;
                            case WaveFormat.Int24:
                                {
                                    int sample = (int)(source[j] * mul);
                                    buffer[bufferIndex] = (byte)(sample & 0xFF);
                                    buffer[bufferIndex + 1] = (byte)((sample >> 8) & 0xFF);
                                    buffer[bufferIndex + 2] = (byte)((sample >> 16) & 0xFF);
                                }
                                break;
                            case WaveFormat.Int32:
                                {
                                    int sample = (int)(source[j] * mul);
                                    Unsafe.WriteUnaligned(ref buffer[bufferIndex], sample);
                                }
                                break;
                            case WaveFormat.Float32:
                                {
                                    float sample = source[j];
                                    Unsafe.WriteUnaligned(ref buffer[bufferIndex], sample);
                                }
                                break;
                        }
                    }

                    j += instride;
                    bufferIndex += ChannelCount * bytesPerSample;
                }
            }
        }


        private int bufferSize;

        public bool IsExtended()
        {
            return extended;
        }

        public int AdjustSampleCount(int iCount)
        {
            if (IsExtended())
            {
                WaveFormat format = Format;
                int num = ((format == WaveFormat.Float32) ? 32 : ((format == WaveFormat.Int32) ? 32 : ((format != WaveFormat.Int24) ? 16 : 24)));
                return (iCount - 4) * 2 / (num / 8);
            }
            return iCount;
        }

        public int ReverseAdjustSampleCount(int iCount)
        {
            if (IsExtended())
            {
                WaveFormat format = Format;
                int num = ((format == WaveFormat.Float32) ? 32 : ((format == WaveFormat.Int32) ? 32 : ((format != WaveFormat.Int24) ? 16 : 24)));
                return num / 8 * iCount / 2 + 4;
            }
            return iCount;
        }

        public int GetBitsPerSample()
        {
            WaveFormat format = Format;
            if (format != WaveFormat.Float32)
            {
                if (format != WaveFormat.Int32)
                {
                    return (format != WaveFormat.Int24) ? 16 : 24;
                }
                return 32;
            }
            return 32;
        }

        internal void SetRawByteData(byte[] inBuffer)
        {
            if (accessor == null)
                return;

            byte* buffer = (byte*)basePointer;

            fixed (byte* source = inBuffer)
            {
                for (int i = 0; i < inBuffer.Length; i++)
                {
                    buffer[i] = source[i];
                }
            }
        }

        internal byte[] GetRawByteData()
        {
            if (accessor == null)
                return new byte[bufferSize];

            byte* buffer = (byte*)basePointer;
            byte[] retBuffer = new byte[bufferSize];

            fixed (byte* dest = retBuffer)
            {
                for (int i = 0; i < retBuffer.Length; i++)
                {
                    retBuffer[i] = buffer[i];
                }
            }

            return retBuffer;
        }
    }
}
