using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Common.Settings;
using BuzzGUI.Interfaces;
using ReBuzz.Common;
using System;
using System.ComponentModel;
using System.Threading;

namespace ReBuzz.Core
{
    internal class MachineConnectionCore : IMachineConnection
    {
        private static IntPtr connectionHandleCounter = 100;

        public IntPtr CMachineConnection { get; private set; }
        public IMachine Source { get; set; }
        public IMachine Destination { get; set; }

        int sourceChannel = 0;
        float panL = 1.0f;
        float panR = 1.0f;

        public int SourceChannel
        {
            get => sourceChannel;
            set { sourceChannel = value; PropertyChanged.Raise(this, "SourceChannel"); }
        }

        int destinationChannel = 0;
        public int DestinationChannel { get => destinationChannel; set { destinationChannel = value; PropertyChanged.Raise(this, "DestinationChannel"); } }

        readonly Interpolator interpolatorAmp = new Interpolator();

        int amp = 0x4000;
        public int Amp
        {
            get
            {
                return amp;
            }
            set
            {
                if (amp != value)
                {
                    amp = value;
                    interpolatorAmp.SetTarget(value, 4); // 4 "steps"

                    if (Destination != null)
                    {
                        int index = Destination.Inputs.IndexOf(this);
                        if (index != -1)
                        {
                            var paramAmp = Destination.ParameterGroups[0].Parameters[0];
                            paramAmp.SetValue(index, amp);
                        }
                    }
                }
            }
        }

        int pan = 0x4000;
        public int Pan
        {
            get => pan;
            set
            {
                if (pan != value)
                {
                    pan = value;

                    if (Destination != null)
                    {
                        int index = Destination.Inputs.IndexOf(this);
                        if (index != -1)
                        {
                            var paramPan = Destination.ParameterGroups[0].Parameters[1];
                            paramPan.SetValue(index, pan);
                        }

                        double panScaled = pan / ((double)0x4000);
                        double pPan = panScaled / 2.0 * Math.PI / 2.0;
                        panL = (float)(2.0 / Math.Sqrt(2.0) * Math.Cos(pPan));
                        panR = (float)(2.0 / Math.Sqrt(2.0) * Math.Sin(pPan));

                        if (engineSettings.EqualPowerPanning)
                        {
                            if (Pan <= 0x4000)
                            {
                                panL = 1;
                            }
                            if (Pan >= 0x4000)
                            {
                                panR = 1;
                            }
                        }
                    }
                }
            }
        }

        public bool HasPan { get; set; }

        public void DoTap(Sample[] sampleBuffer, int nSamples, bool stereo, SongTime songTime)
        {
            if (Tap != null)
            {
                float[] samples = new float[nSamples * 2];
                int j = 0;
                for (int i = 0; i < nSamples; i++)
                {
                    samples[j] = sampleBuffer[i].L;
                    j++;
                    samples[j] = sampleBuffer[i].R;
                    j++;
                }

                //dispatcher.BeginInvoke(() =>
                //{
                    Tap?.Invoke(samples, stereo, songTime);
                //});
            }
        }

        // Tap-gated variant: fetches the SongTime only when a tap is actually
        // registered, so the common no-tap path allocates nothing (SongTime is a
        // class, and it was previously constructed per output connection per
        // chunk as the call-site argument - before the Tap null check inside
        // DoTap could run). Each invocation that does fire still gets its own
        // fresh SongTime instance, preserving the previous per-call semantics
        // for tap consumers.
        internal void DoTap(Sample[] sampleBuffer, int nSamples, bool stereo, ReBuzzCore buzz)
        {
            if (Tap == null)
                return;
            DoTap(sampleBuffer, nSamples, stereo, buzz.GetSongTime());
        }

        internal void UpdateBuffer(Sample[] samples, int nSamples)
        {
            float ampStart = interpolatorAmp.Value / 0x4000;
            float ampCurrent = interpolatorAmp.Tick() / 0x4000;
            float ampStep = (ampStart - ampCurrent) / nSamples;

            // Zero-latency fast path. When addedLatency == 0 the latency ring's
            // read position always equals its write position (both start equal
            // and advance in lockstep), so the ring is a pass-through: the second
            // loop below would read back exactly what the first loop wrote. Write
            // the amp/pan-scaled samples straight into buffer instead, skipping
            // the ring copy and the per-sample modulo. Arithmetic, the amp ramp,
            // and the interpolatorAmp.Tick() side effect above are all identical
            // to the ring path, so this is bit-exact. latencyBuffer is read
            // nowhere else, and every transition back to a latency > 0 state goes
            // through UpdateLatencyBuffers, which resets both positions - so not
            // advancing them here is unobservable.
            if (addedLatency == 0)
            {
                for (int i = 0; i < nSamples; i++)
                {
                    buffer[i].L = samples[i].L * ampStart * panL;
                    buffer[i].R = samples[i].R * ampStart * panR;
                    ampStart += ampStep;
                }
                return;
            }

            // Latency-compensated path. Ring wrap via a branch rather than a
            // per-sample integer divide (% Length); positions are always
            // < Length, so ++pos == Length is exactly the wrap case.
            for (int i = 0; i < nSamples; i++)
            {
                latencyBuffer[latencyBufferWritePos].L = samples[i].L * ampStart * panL;
                latencyBuffer[latencyBufferWritePos].R = samples[i].R * ampStart * panR;
                if (++latencyBufferWritePos == latencyBuffer.Length) latencyBufferWritePos = 0;
                ampStart += ampStep;
            }

            for (int i = 0; i < nSamples; i++)
            {
                buffer[i].L = latencyBuffer[latencyBufferReadPos].L;
                buffer[i].R = latencyBuffer[latencyBufferReadPos].R;
                if (++latencyBufferReadPos == latencyBuffer.Length) latencyBufferReadPos = 0;
            }
        }

        internal void ClearBuffer(int num)
        {
            for (int i = 0; i < num; i++)
            {
                buffer[i].L = 0;
                buffer[i].R = 0;
            }
        }

        public MachineConnectionCore(IUiDispatcher dispatcher, EngineSettings engineSettings)
        {
            interpolatorAmp.Value = amp;

            this.engineSettings = engineSettings;
            CMachineConnection = connectionHandleCounter++;
            this.dispatcher = dispatcher;
        }

        public MachineConnectionCore(MachineCore source, int sourceChannel, MachineCore destination, int destinationChannel, int amp, int pan, IUiDispatcher dispatcher, EngineSettings engineSettings)
        {
            this.engineSettings = engineSettings;
            this.dispatcher = dispatcher;
            Source = source;
            this.sourceChannel = sourceChannel;
            Destination = destination;
            this.destinationChannel = destinationChannel;
            Amp = amp;
            Pan = pan;
        }

        internal void ClearEvents()
        {
            Tap = null;
            PropertyChanged = null;
        }

        internal void UpdateLatencyBuffers(int latencyDelta)
        {
            lock (ReBuzzCore.AudioLock)
            {
                if (latencyDelta >= 0 && addedLatency != latencyDelta)
                {
                    addedLatency = latencyDelta;
                    latencyBuffer = new Sample[256 + latencyDelta];
                }
                latencyBufferReadPos = 0;
                latencyBufferWritePos = latencyDelta;
            }
        }

        Sample[] buffer = new Sample[256];
        Sample[] latencyBuffer = new Sample[256];
        int addedLatency = 0;
        int latencyBufferReadPos = 0;
        int latencyBufferWritePos = 0;
        private readonly IUiDispatcher dispatcher;
        private readonly EngineSettings engineSettings;

        public Sample[] Buffer { get
            {
                return buffer;
            } 
        }

        public event Action<float[], bool, SongTime> Tap;
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
