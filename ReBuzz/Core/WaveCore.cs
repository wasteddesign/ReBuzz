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

        WaveFlags flags;
        public WaveFlags Flags { get => flags; set { flags = value; buzz.MachineManager.UpdateWaveInfo(); } }

        float volume = 1.0f;
        public float Volume {
            get
            {
                return volume;
            }
            set
            {
                volume = value;
                // Update waves info to native machines
                buzz.MachineManager.UpdateWaveInfo();
            }
        }

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

        internal List<Envelope> envelopes = new List<Envelope>()
        {
            new Envelope(),     // Volume
            new Envelope(),     // Panning
            new Envelope()      // Pitch
        };

        /// <summary>
        /// TODO:
        /// The returned envelope is associated with machine 'm'. The association is needed for CMachineInterface::GetWaveEnvPlayPos calls.
        /// </summary>
        public IEnvelope GetEnvelope(int index, IMachine m)
        {
            var mc = m as MachineCore;
            if (index < envelopes.Count)
            {
                if (index == 1 && !m.HasStereoOutput)
                    return null;
                return envelopes[index];
            }

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
            buzz.MachineManager.UpdateWaveInfo();
            buzz.MachineManager.SetWaveChangedEvent(Index);
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
