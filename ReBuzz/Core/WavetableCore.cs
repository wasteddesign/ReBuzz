using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using libsndfile;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;

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

        public void PlayWave(string path)
        {   
            buzz.PlayWave(path);
        }

        internal WaveCore CreateWave(ushort index)
        {
            var w = new WaveCore(buzz);
            w.WaveChanged += W_WaveChanged;
            WavesList[index] = w;

            return w;
        }
    }
}
