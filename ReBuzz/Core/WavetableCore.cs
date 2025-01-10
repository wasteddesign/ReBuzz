using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using libsndfile;
using ReBuzz.Audio;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace ReBuzz.Core
{
    internal class WavetableCore : IWavetable
    {
        public static readonly int NUM_WAVES = 200;
        public ISong Song { get => buzz.Song; }

        readonly List<WaveCore> waves = new List<WaveCore>();
        private readonly ReBuzzCore buzz;

        public List<WaveCore> WavesList { get => waves; }
        public ReadOnlyCollection<IWave> Waves { get => waves.Cast<IWave>().ToReadOnlyCollection(); }

        private float volume;
        public float Volume { get => volume; set => volume = value; }

        public event Action<int> WaveChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        PlayWaveInfo PlayWaveData { get; set; }
        internal class PlayWaveInfo
        {
            public WaveCore wave;
            public int postion;
            public int start;
            public int end;
            public LoopType looptype;
            internal SoundFile sf;
        }

        public WavetableCore(ReBuzzCore bc)
        {
            this.buzz = bc;

            for (int i = 0; i < NUM_WAVES; i++)
            {
                waves.Add(null);
                WaveChanged?.Invoke(i);
            }
        }

        private void W_WaveChanged(int waveIndex)
        {
            WaveChanged?.Invoke(waveIndex);
        }

        public void AllocateWave(int index, string path, string name, int size, WaveFormat format, bool stereo, int root, bool add, bool wavechangedevent)
        {
            if (index < 0 || index >= waves.Count)
                return;

            var w = WavesList[index];
            if (WavesList[index] == null)
            {
                w = new WaveCore(buzz);
                w.WaveChanged += W_WaveChanged;
                WavesList[index] = w;
            }

            w.Index = index;
            w.Name = name;
            w.FileName = path;
            w.Volume = volume;

            int size16Bit = size;

            switch (format)
            {
                case WaveFormat.Int24:
                    size16Bit = (int)Math.Ceiling(size * 3 / 2.0) + 4;
                    break;
                case WaveFormat.Int32:
                case WaveFormat.Float32:
                    size16Bit = size16Bit * 2 + 4;
                    break;
                case WaveFormat.Int16:
                    break;
            }

            if (add)
            {
                bool ok = w.Flags.HasFlag(WaveFlags.Stereo) && stereo || !stereo && !w.Flags.HasFlag(WaveFlags.Stereo);
                ok &= !w.Flags.HasFlag(WaveFlags.Not16Bit) && format == WaveFormat.Int16;
                ok |= w.Layers.Count == 0;
                if (ok)
                {
                    WaveLayerCore layer = new WaveLayerCore(w, path, format, root, stereo, size16Bit);
                    w.LayersList.Add(layer);
                    layer.LayerIndex = w.LayersList.Count - 1;
                }
            }
            // Wave needs to be same format & type
            else
            {
                w.Flags = 0;
                if (stereo)
                    w.Flags |= WaveFlags.Stereo;
                if (format != WaveFormat.Int16)
                    w.Flags |= WaveFlags.Not16Bit;

                var layer = w.LayersList.FirstOrDefault();
                if (layer == null)
                {
                    layer = new WaveLayerCore(w, path, format, root, stereo, size16Bit);
                    w.LayersList.Add(layer);
                }
                else
                {
                    layer.Init(path, format, root, stereo, size16Bit);
                }
            }

            if (wavechangedevent)
            {
                WaveChanged?.Invoke(index);
            }

            PropertyChanged.Raise(this, "Waves");
        }

        public void LoadWave(int index, string path, string name, bool add)
        {
            lock (ReBuzzCore.AudioLock)
            {
                if (index < 0 || index >= waves.Count)
                    return;

                if (path == null) // Clear
                {
                    var w = WavesList[index];
                    if (w != null)
                    {
                        w.WaveChanged -= W_WaveChanged;
                        w.Clear();
                    }
                    WavesList[index] = null;

                    WaveChanged?.Invoke(index);
                }
                else
                {
                    using (var sf = SoundFile.OpenRead(path))
                    {
                        int ReadBufferSize = 4096;
                        var wavetable = buzz.SongCore.Wavetable;

                        // To support waves with more channels, update the read logic below
                        if (sf.ChannelCount != 1 && sf.ChannelCount != 2) throw new Exception("Unsupported channel count.");
                        var subformat = sf.Format & Format.SF_FORMAT_SUBMASK;

                        WaveFormat wf;

                        if (subformat == Format.SF_FORMAT_FLOAT || subformat == Format.SF_FORMAT_DOUBLE) wf = WaveFormat.Float32;
                        else if (subformat == Format.SF_FORMAT_PCM_32) wf = WaveFormat.Int32;
                        else if (subformat == Format.SF_FORMAT_PCM_24) wf = WaveFormat.Int24;
                        else wf = WaveFormat.Int16;

                        var inst = sf.Instrument;
                        var rootnote = BuzzNote.FromMIDINote(Math.Max(0, inst.basenote - 12));  // -12 for backwards compatibility

                        //when adding a new layer to a slot we need to make sure they are all in the same format, convert the old layers in the slot to float / stereo if needed.
                        int ChannelCount = sf.ChannelCount;
                        if (add == true)
                        {
                            WaveCommandHelpers.ConvertSlotIfNeeded(wavetable, index, ref wf, ref ChannelCount);
                        }

                        wavetable.AllocateWave(index, path, name, (int)sf.FrameCount, wf, ChannelCount == 2, rootnote, add, false);

                        var wave = wavetable.Waves[index];
                        var layerFirst = wave.Layers.Where(l => l.RootNote == rootnote).Last();      // multiple layers may have the same root, so use Last() to get the new one

                        layerFirst.SampleRate = sf.SampleRate;

                        if (inst.loop_count > 0)
                        {
                            wave.Flags |= WaveFlags.Loop;
                            layerFirst.LoopStart = (int)inst.loops[0].start;
                            layerFirst.LoopEnd = (int)inst.loops[0].end + 1;                         // buzz loop is right-open, sndfile loop is right-closed
                        }
                        else //no loop, we should set reasonable values
                        {
                            layerFirst.LoopStart = 0;
                            layerFirst.LoopEnd = layerFirst.SampleCount;
                        }

                        var buffer = new float[ReadBufferSize * sf.ChannelCount];

                        long framesread = 0;
                        while (framesread < sf.FrameCount)
                        {
                            var n = sf.ReadFloat(buffer, ReadBufferSize);
                            if (n <= 0) break;

                            for (int ch = 0; ch < sf.ChannelCount; ch++)
                            {
                                layerFirst.SetDataAsFloat(buffer, ch, sf.ChannelCount, ch, (int)framesread, (int)n);

                                //write the same data to the right channel too in case the new layer is mono but we converted the slot to stereo already
                                //if ((ConvertSlotToStereo == true && sf.ChannelCount == 1) || (AllLayersAreStereo == true && sf.ChannelCount == 1))
                                if ((ChannelCount == 2 && sf.ChannelCount == 1))
                                {
                                    //BuzzGUI.Common.Global.Buzz.DCWriteLine("COPY LEFT TO RIGHT ON NEW LAYER");
                                    layerFirst.SetDataAsFloat(buffer, 0, 1, 1, (int)framesread, (int)n);
                                }
                            }

                            framesread += n;
                        }
                    }
                }
            }
        }

        internal WaveCore CreateWave(ushort index)
        {
            var w = new WaveCore(buzz);
            w.WaveChanged += W_WaveChanged;
            WavesList[index] = w;

            return w;
        }

        internal bool IsPlayingWave()
        {
            return PlayWaveData != null;
        }

        internal void PlayWave(WaveCore wave, int start, int end, LoopType looptype)
        {
            lock (ReBuzzCore.AudioLock)
            {
                StopPlayingWave();
                try
                {
                    PlayWaveData = new PlayWaveInfo()
                    {
                        wave = wave,
                        postion = 0,
                        start = start,
                        end = end,
                        looptype = looptype
                    };
                    realTimeResampler.Reset(Global.Buzz.SelectedAudioDriverSampleRate, wave.Layers[0].SampleRate <= 0 ? 44100 : wave.Layers[0].SampleRate);
                }
                catch (Exception e)
                {
                }
            }
        }

        public void PlayWave(string file)
        {
            lock (ReBuzzCore.AudioLock)
            {
                StopPlayingWave();
                try
                {
                    var sf = SoundFile.OpenRead(file);
                    PlayWaveData = new PlayWaveInfo()
                    {
                        wave = null,
                        postion = 0,
                        start = 0,
                        end = 0,
                        looptype = LoopType.None,
                        sf = sf
                    };
                    realTimeResampler.Reset(Global.Buzz.SelectedAudioDriverSampleRate, sf.SampleRate);
                }
                catch (Exception e)
                {
                }
            }
        }

        internal void StopPlayingWave()
        {
            lock (ReBuzzCore.AudioLock)
            {
                if (PlayWaveData != null && PlayWaveData.sf != null)
                {
                    PlayWaveData.sf.Dispose();
                }
                PlayWaveData = null;
            }
        }

        RealTimeResampler realTimeResampler = new();

        static int ReadBufferSize = 2 * 256;
        float[] wavetableAudiobufferFile = new float[ReadBufferSize];
        float[] wavetableAudiobuffer = new float[ReadBufferSize];

        // Read sampleCount stereo samples to buffer
        internal bool GetPlayWaveSamples(float[] buffer, int offset, int sampleCount)
        {
            if (PlayWaveData != null)
            {
                if (PlayWaveData.sf != null)
                {
                    int samplesRead = 0;
                    while (realTimeResampler.AvailableSamples() < sampleCount)
                    {
                        if (!ReadSamplesFromFile(offset, sampleCount))
                            return false;

                        realTimeResampler.FillBuffer(wavetableAudiobuffer, sampleCount);
                    }

                    realTimeResampler.GetSamples(buffer, offset, sampleCount);
                }
                else if (PlayWaveData.wave.Layers.Count > 0)
                {
                    var layer = PlayWaveData.wave.Layers.First();
                    while (realTimeResampler.AvailableSamples() < sampleCount)
                    {
                        layer.GetDataAsFloat(wavetableAudiobuffer, 0, 2, 0, PlayWaveData.postion, sampleCount);
                        layer.GetDataAsFloat(wavetableAudiobuffer, 0 + 1, 2, 1, PlayWaveData.postion, sampleCount);

                        realTimeResampler.FillBuffer(wavetableAudiobuffer, sampleCount);
                        PlayWaveData.postion += sampleCount;
                    }
                    realTimeResampler.GetSamples(buffer, offset, sampleCount);

                    return true;
                }
            }
            return false;
        }

        bool ReadSamplesFromFile(int offset, int sampleCount)
        {
            var sf = PlayWaveData.sf;
            int outOffset = 0;

            while (sampleCount > 0)
            {
                int maxReadFrameCount = ReadBufferSize / sf.ChannelCount;
                int readFrameCount = Math.Min(maxReadFrameCount, sampleCount);
                var n = sf.ReadFloat(wavetableAudiobufferFile, readFrameCount);
                if (n <= 0) return false;

                int inOffset = 0;
                for (int j = 0; j < readFrameCount; j++)
                {
                    for (int ch = 0; ch < sf.ChannelCount; ch++)
                    {
                        if (ch == 0)
                        {
                            wavetableAudiobuffer[outOffset] = wavetableAudiobufferFile[inOffset];

                            // Conver mono to stereo
                            if (sf.ChannelCount == 1)
                            {
                                wavetableAudiobuffer[outOffset + 1] = wavetableAudiobufferFile[inOffset];
                            }
                        }
                        else if (ch == 1)
                        {
                            wavetableAudiobuffer[outOffset + 1] = wavetableAudiobufferFile[inOffset + 1];
                        }
                        else
                        {
                            break;
                        }
                    }
                    outOffset += 2;
                    inOffset += sf.ChannelCount;
                }
                sampleCount -= readFrameCount;
            }
            return true;
        }
    }
}
