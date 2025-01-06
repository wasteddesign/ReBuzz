using BuzzGUI.Interfaces;
using libsndfile;
using System;
using System.IO;

namespace BuzzGUI.Common
{
    [Serializable]
    public class TemporaryWave : IWaveLayer
    {
        WaveFormat format;
        int sampleCount;
        int rootNote;
        int sampleRate;
        int channelCount;
        int loopStart;
        int loopEnd;
        readonly float[] left;
        float[] right;
        readonly int index;
        string path;
        string name;

        public WaveFormat Format { get { return format; } }
        public int SampleCount { get { return sampleCount; } }
        public int RootNote { get { return rootNote; } set { throw new NotImplementedException(); } }
        public int SampleRate { get { return sampleRate; } set { throw new NotImplementedException(); } }
        public int ChannelCount { get { return channelCount; } }
        public int LoopStart { get { return loopStart; } set { throw new NotImplementedException(); } }
        public int LoopEnd { get { return loopEnd; } set { throw new NotImplementedException(); } }
        public float[] Left { get { return left; } private set { throw new NotImplementedException(); } }
        public float[] Right { get { return right; } private set { throw new NotImplementedException(); } }
        public int Index { get { return index; } } //note, only valid for TemporaryWaves constructed trough an IWaveformBase
        public string Path { get { return path; } }
        public string Name { get { return name; } } //TODO do what must be done when the name changes
        public IntPtr RawSamples { get; private set; }

#pragma warning disable 67
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        public TemporaryWave(float[] DataLeft, WaveFormat iFormat, int iSampleRate, int iRootNote, string iPath, string iName) //for mono
        {
            left = DataLeft;
            InitializeWave(1, iFormat, iSampleRate, iRootNote, DataLeft.Length, 0, DataLeft.Length, iPath, iName);
        }

        public TemporaryWave(float[] DataLeft, float[] DataRight, WaveFormat iFormat, int iSampleRate, int iRootNote, string iPath, string iName) //for stereo
        {
            left = DataLeft;
            right = DataRight;
            InitializeWave(2, iFormat, iSampleRate, iRootNote, DataLeft.Length, 0, DataLeft.Length, iPath, iName);
        }

        public TemporaryWave(IWaveLayer layer) //for making a copy of an existing layer
        {
            if (layer.ChannelCount == 1)
            {
                left = new float[layer.SampleCount];
                layer.GetDataAsFloat(left, 0, 1, 0, 0, layer.SampleCount);
            }
            else if (layer.ChannelCount == 2)
            {
                left = new float[layer.SampleCount];
                right = new float[layer.SampleCount];
                layer.GetDataAsFloat(left, 0, 1, 0, 0, layer.SampleCount);
                layer.GetDataAsFloat(right, 0, 1, 1, 0, layer.SampleCount);
            }

            InitializeWave(layer.ChannelCount, layer.Format, layer.SampleRate, layer.RootNote, layer.SampleCount, layer.LoopStart, layer.LoopEnd, layer.Path, System.IO.Path.GetFileNameWithoutExtension(layer.Path));

            //we need to store the index to find out which one was selected when running a command that allocates again.
            index = WaveCommandHelpers.GetLayerIndex(layer);
        }


        public TemporaryWave(Stream ms)
        {
            // get audio stream 
            if (ms == null) return;
            libsndfile.SF_INFO msInfo = new SF_INFO();

            using (var s = new SoundFile(ms, libsndfile.FileMode.SFM_READ, msInfo))
            {
                if (s == null) return;

                var subformat = s.Format & libsndfile.Format.SF_FORMAT_SUBMASK;
                WaveFormat wf;

                if (subformat == libsndfile.Format.SF_FORMAT_FLOAT || subformat == libsndfile.Format.SF_FORMAT_DOUBLE) wf = WaveFormat.Float32;
                else if (subformat == libsndfile.Format.SF_FORMAT_PCM_32) wf = WaveFormat.Int32;
                else if (subformat == libsndfile.Format.SF_FORMAT_PCM_24) wf = WaveFormat.Int24;
                else wf = WaveFormat.Int16;

                if (s.ChannelCount == 1)
                {
                    left = new float[s.FrameCount];
                    s.ReadFloat(left, s.FrameCount);
                }
                else if (s.ChannelCount == 2)
                {
                    left = new float[s.FrameCount];
                    right = new float[s.FrameCount];
                    for (int i = 0; i < s.FrameCount; i++)
                    {
                        float[] hold = new float[2];
                        s.ReadFloat(hold, 1);

                        left[i] = hold[0];
                        right[i] = hold[1];
                    }
                }



                //TODO use initializewvae
                int iSampleCount = (int)(s.FrameCount * s.ChannelCount);

                channelCount = s.ChannelCount;
                format = wf;
                sampleRate = s.SampleRate;
                rootNote = BuzzNote.FromMIDINote(Math.Max(0, s.Instrument.basenote - 12)); //TODO re-think
                sampleCount = iSampleCount;
                loopStart = 0; //TODO re-think
                loopEnd = sampleCount; //TODO re-think
                path = "Copy.wav"; //need to set this because we're infering the name from the path in other places TODO re-think
                name = "Copy"; //TODO re-think
            }
        }

        private void InitializeWave(int iChannelCount, WaveFormat iFormat, int iSampleRate, int iRootNote, int iSampleCount, int iLoopStart, int iLoopEnd, string iPath, string iName)
        {
            channelCount = iChannelCount;
            format = iFormat;
            sampleRate = iSampleRate;
            rootNote = iRootNote;
            sampleCount = iSampleCount;
            loopStart = iLoopStart;
            loopEnd = iLoopEnd;
            path = iPath;
            name = iName;

            //BuzzGUI.Common.Global.Buzz.DCWriteLine("layername: " + name);
            //BuzzGUI.Common.Global.Buzz.DCWriteLine("layerpath: " + path);
        }

        public void CopyLeftToRight()
        {
            right = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                right[i] = left[i];
            }
        }

        public void GetDataAsFloat(float[] output, int outoffset, int outstride, int channel, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public void SetDataAsFloat(float[] input, int inoffset, int instride, int channel, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public void InvalidateData()
        {
            throw new NotImplementedException();
        }

    }
}
