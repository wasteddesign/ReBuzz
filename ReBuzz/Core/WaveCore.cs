using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace ReBuzz.Core
{
    internal class WaveCore : IWave
    {
        public int Index
        {
            get;
            set;
        }

        public string Name { get; set; }
        public string FileName { get; set; }

        public WaveFlags Flags { get; set; }
        public float Volume { get; set; }

        private readonly ReBuzzCore buzz;
        List<WaveLayerCore> waveLayers;
        public ReadOnlyCollection<IWaveLayer> Layers { get => waveLayers.Cast<IWaveLayer>().ToReadOnlyCollection(); }

        public List<WaveLayerCore> LayersList { get => waveLayers; set => waveLayers = value; }

        public WaveCore(ReBuzzCore buzz)
        {
            this.buzz = buzz;
            waveLayers = new List<WaveLayerCore>();
            Flags = WaveFlags.Not16Bit | WaveFlags.Stereo;
            Volume = 1f;
        }

        public event Action<int> WaveChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        internal List<Envelope> envelopes = new List<Envelope>();
        public IEnvelope GetEnvelope(int index, IMachine m)
        {
            if (index < envelopes.Count)
                return envelopes[index];

            return null;
        }

        public void Play(IMachine m, int start = 0, int end = -1, LoopType looptype = LoopType.None)
        {
            buzz.SongCore.WavetableCore.PlayWave(this, start, end, looptype);
        }

        public void Stop(IMachine m)
        {
            buzz.SongCore.WavetableCore.StopPlayingWave();
        }

        internal void Invalidate()
        {
            if (WaveChanged != null)
            {
                WaveChanged.Invoke(Index);
            }
            buzz.MachineManager.InvalidateWaves();
            PropertyChanged.Raise(this, "Layers");
        }

        internal void Clear()
        {
            foreach (var l in LayersList)
            {
                l.Release();
            }
            LayersList.Clear();
            Name = FileName = null;
            Volume = 0;
            Flags = 0;
            Invalidate();
        }
    }
}
