using BuzzGUI.Interfaces;
using System;
using System.ComponentModel;
using System.IO.MemoryMappedFiles;

namespace ReBuzz.Core
{
    internal unsafe class WaveLayerCore : IWaveLayer
    {
        //private float[] buffer;

        // mappedFile if needed to share to native machines
        private MemoryMappedFile mappedFile;
        private MemoryMappedViewAccessor accessor;
        internal byte* basePointer;

        public WaveLayerCore(WaveCore wave, string path, WaveFormat format, int root, bool stereo, int size)
        {
            this.Wave = wave;
            Init(path, format, root, stereo, size);
        }

        public void Init(string path, WaveFormat format, int root, bool stereo, int size)
        {
            Path = path;
            WaveFormatData = (int)format;
            RootNote = root;
            ChannelCount = stereo ? 2 : 1;
            SampleCount = size;
            SampleCount16Bit = ReverseAdjustSampleCount(SampleCount);
            SampleCount16Bit += SampleCount16Bit % GetBitsPerSample() / 8;

            bufferSize = SampleCount * ChannelCount * sizeof(float);

            //buffer = new float[SampleCount * ChannelCount];

            //int bytesize = SampleCount * ChannelCount * sizeof(float);
            //mappedFile = MemoryMappedFile.CreateNew(Path + DateTime.Now.Ticks, bytesize);

            CreateBuffer();
            LoopEnd = size;
            LoopStart = 0;

            //UpdateBufferAddr();
        }

        public void Release()
        {
            if (mappedFile != null)
            {
                mappedFile.Dispose();
            }
            basePointer = null;
            RawSamples = IntPtr.Zero;
        }

        internal void CreateBuffer()
        {
            if (mappedFile != null)
                mappedFile.Dispose();

            mappedFile = null;
            accessor = null;
            RawSamples = IntPtr.Zero;

            if (bufferSize > 0)
            {
                mappedFile = MemoryMappedFile.CreateNew("WaveLayer_" + DateTime.Now.Ticks, bufferSize);
                accessor = mappedFile.CreateViewAccessor();
                //buffer = new float[SampleCount * ChannelCount];

                //GCHandle handle1 = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                //RawSamples = handle1.AddrOfPinnedObject();
                //handle1.Free();

                mappedFile.CreateViewAccessor().SafeMemoryMappedViewHandle.AcquirePointer(ref basePointer);
                RawSamples = (IntPtr)basePointer;
            }
        }

        public WaveLayerCore()
        {
            Path = "";
            //UpdateBufferAddr();
            RawSamples = IntPtr.Zero;
        }

        /*
        void UpdateBufferAddr()
        {
            //GCHandle handle1 = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            //RawSamples = handle1.AddrOfPinnedObject();
            //handle1.Free();
            

        }
        */

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
        public int SampleCount { get; set; }
        public int RootNote { get; set; }
        public int SampleRate { get; set; }
        public int LoopStart { get; set; }
        public int LoopEnd { get; set; }

        public int ChannelCount { get; set; }
        public int SampleCount16Bit { get; internal set; }

        // Needs to be public because of WaveControl and Reflection...
        public int layerIndex;
        public int LayerIndex { get => layerIndex; set => layerIndex = value; }

        public event PropertyChangedEventHandler PropertyChanged;

        /*
        public void GetDataAsFloat(float[] output, int outoffset, int outstride, int channel, int offset, int count)
        {
            unsafe
            {
                fixed (float* pout = output, pbuf = buffer)
                {
                    if (ChannelCount == 1)
                    {
                        // This wave is mono
                        int j = outoffset;
                        int bufferIndex = offset;
                        for (int i = 0; i < count; i++)
                        {
                            pout[j] = pbuf[bufferIndex];
                            j += outstride;
                            bufferIndex++;
                        }
                    }
                    else if (ChannelCount == 2)
                    {
                        // This wave is stereo
                        int j = outoffset;
                        int bufferIndex = 2 * offset + channel;
                        for (int i = 0; i < count; i++)
                        {
                            pout[j] = pbuf[bufferIndex];
                            bufferIndex += 2;
                            j += outstride;
                        }
                    }
                }
            }
        }
            
        */

        public void GetDataAsFloat(float[] output, int outoffset, int outstride, int channel, int offset, int count)
        {
            if (accessor == null)
                return;

            if (ChannelCount == 1)
            {
                // This wave is mono
                int j = outoffset;
                float* buffer = (float*)basePointer;
                int floatLen = bufferSize >> 2;
                fixed (float* dest = output)
                {
                    for (int i = 0; i < count; i++)
                    {
                        int bufferIndex = i + offset;
                        if (j >= output.Length)
                            return;

                        if (bufferIndex < 0 || bufferIndex >= floatLen)
                            dest[j] = 0;
                        else
                            dest[j] = buffer[bufferIndex];
                        j += outstride;
                    }
                }

            }
            else if (ChannelCount == 2)
            {
                // This wave is stereo
                int j = outoffset;
                float* buffer = (float*)basePointer;
                int floatLen = bufferSize >> 2;
                fixed (float* dest = output)
                {
                    for (int i = 0; i < count; i++)
                    {
                        int bufferIndex = 2 * i + 2 * offset + channel;
                        if (j >= output.Length)
                            return;

                        if (bufferIndex < 0 || bufferIndex >= floatLen)
                            dest[j] = 0;
                        else
                            dest[j] = buffer[bufferIndex];

                        j += outstride;
                    }
                }
            }
        }

        /*
        public void GetDataAsFloat(float[] output, int outoffset, int outstride, int channel, int offset, int count)
        {
            if (ChannelCount == 1)
            {
                // This wave is mono
                int j = outoffset;
                for (int i = 0; i < count; i++)
                {
                    int bufferIndex = i + offset;
                    if (j >= output.Length)
                        return;

                    if (bufferIndex < 0 || bufferIndex >= buffer.Length)
                        output[j] = 0;
                    else
                        output[j] = buffer[bufferIndex];
                    j += outstride;
                }
            }
            else if (ChannelCount == 2)
            {
                // This wave is stereo
                int j = outoffset;
                for (int i = 0; i < count; i++)
                {
                    int bufferIndex = 2 * i + 2 * offset + channel;
                    if (j >= output.Length)
                        return;

                    if (bufferIndex < 0 || bufferIndex >= buffer.Length)
                        output[j] = 0;
                    else
                        output[j] = buffer[bufferIndex];

                    output[j] = buffer[bufferIndex];
                    j += outstride;
                }
            }
        }
        */

        public void InvalidateData()
        {
            Wave.Invalidate();
        }

        public void SetDataAsFloat(float[] input, int inoffset, int instride, int channel, int offset, int count)
        {
            if (accessor == null)
                return;

            if (ChannelCount == 1)
            {
                float* buffer = (float*)basePointer;
                int floatLen = bufferSize >> 2;
                int j = inoffset;

                fixed (float* source = input)
                {
                    for (int i = 0; i < count; i++)
                    {
                        buffer[i + offset + channel] = source[j];
                        j += instride;
                    }
                }
            }
            else if (ChannelCount == 2)
            {
                float* buffer = (float*)basePointer;
                int floatLen = bufferSize >> 2;
                int j = inoffset;

                fixed (float* source = input)
                {
                    for (int i = 0; i < 2 * count; i += 2)
                    {
                        buffer[i + 2 * offset + channel] = source[j];
                        j += instride;
                    }
                }
            }
        }

        public bool extended = true;
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
    }
}
