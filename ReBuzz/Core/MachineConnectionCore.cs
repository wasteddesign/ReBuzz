using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Common.Settings;
using BuzzGUI.Interfaces;
using ReBuzz.Audio.BurstProtection;
using ReBuzz.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

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

        BurstProtectionEngine burstProtection;

        public int SourceChannel
        {
            get => sourceChannel;
            set { sourceChannels.Clear(); SetSourceChannel(value, true); }
        }

        int destinationChannel = 0;
        public int DestinationChannel { get => destinationChannel; set { destinationChannels.Clear(); SetDestinationChannel(value, true); } }

        HashSet<int> sourceChannels = new HashSet<int>();
        public IEnumerable<int> SourceChannels => sourceChannels;

        public void SetSourceChannel(int channel, bool set)
        {
            if (set)
            {
                sourceChannels.Add(channel);
            }
            else
            {
                if (sourceChannels.Count > 1)
                {
                    // Ensure at least one channel is selected
                    sourceChannels.Remove(channel);
                }
            }
            int lowest = sourceChannels.Count > 0
                ? SourceChannels.Min()
                : 0; // Get the lowest channel or default to 0 if none are selected
            sourceChannel = lowest;
            PropertyChanged.Raise(this, "SourceChannel");
            PropertyChanged.Raise(this, "SourceChannels");
        }

        HashSet<int> destinationChannels = new HashSet<int>();
        public IEnumerable<int> DestinationChannels => destinationChannels;
        public void SetDestinationChannel(int channel, bool set)
        {
            if (set)
            {
                destinationChannels.Add(channel);
            }
            else
            {
                // Ensure at least one channel is selected
                if (destinationChannels.Count > 1)
                {
                    destinationChannels.Remove(channel);
                }
            }

            int lowest = destinationChannels.Count > 0
                ? DestinationChannels.Min()
                : 0; // Get the lowest channel or default to 0 if none are selected
            destinationChannel = lowest;
            PropertyChanged.Raise(this, "DestinationChannel");
            PropertyChanged.Raise(this, "DestinationChannels");
        }

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

        // Tap buffers are reused. Handlers run synchronously on the engine thread
        // that processed the source machine, and must not keep the array (or the
        // SongTime) after they return; copy if needed. Allocating a fresh array per
        // tapped connection per chunk was the largest allocator left on the audio
        // threads while any VU meter was visible.
        //
        // Handlers read samples.Length, so the array must be exactly nSamples * 2.
        // Chunk sizes vary at tick boundaries (a full chunk plus one or two
        // remainder sizes), so keep a few exact-size buffers rather than one.
        const int TapBufferSlots = 4;
        readonly float[][] tapBuffers = new float[TapBufferSlots][];
        int tapBufferNextSlot;
        SongTime tapSongTime;

        float[] GetTapBuffer(int length)
        {
            var buffers = tapBuffers;
            for (int i = 0; i < buffers.Length; i++)
            {
                var b = buffers[i];
                if (b != null && b.Length == length)
                    return b;
            }

            var newBuffer = new float[length];
            buffers[tapBufferNextSlot] = newBuffer;
            tapBufferNextSlot = (tapBufferNextSlot + 1) % buffers.Length;
            return newBuffer;
        }

        public void DoTap(Sample[] sampleBuffer, int nSamples, bool stereo, SongTime songTime)
        {
            if (Tap != null)
            {
                // Every element is overwritten below, so a reused buffer needs no clearing.
                float[] samples = GetTapBuffer(nSamples * 2);
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

        // Tap-gated variant: fills the SongTime only when a tap is registered, so
        // the common no-tap path does no work. The SongTime instance is reused per
        // connection, like the sample buffer (see above).
        internal void DoTap(Sample[] sampleBuffer, int nSamples, bool stereo, ReBuzzCore buzz)
        {
            if (Tap == null)
                return;
            var songTime = tapSongTime ??= new SongTime();
            buzz.FillSongTime(songTime);
            DoTap(sampleBuffer, nSamples, stereo, songTime);
        }

        float ampStart;
        float ampCurrent;
        float ampStep;
        internal void PreUpdateBufferActions(int nSamples)
        {
            ampStart = interpolatorAmp.Value / 0x4000;
            ampCurrent = interpolatorAmp.Tick() / 0x4000;
            ampStep = (ampStart - ampCurrent) / nSamples;
        }

        internal void UpdateBuffer(Sample[] samples, int nSamples)
        {
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
                    buffer[i].L += samples[i].L * ampStart * panL;
                    buffer[i].R += samples[i].R * ampStart * panR;
                    ampStart += ampStep;
                }
            }
            else
            {
                // Latency-compensated path. Ring wrap via a branch rather than a
                // per-sample integer divide (% Length); positions are always
                // < Length, so ++pos == Length is exactly the wrap case.
                int tmpLatencyBufferWritePos = latencyBufferWritePos;
                for (int i = 0; i < nSamples; i++)
                {
                    latencyBuffer[tmpLatencyBufferWritePos].L += samples[i].L * ampStart * panL;
                    latencyBuffer[tmpLatencyBufferWritePos].R += samples[i].R * ampStart * panR;
                    if (++tmpLatencyBufferWritePos == latencyBuffer.Length) tmpLatencyBufferWritePos = 0;
                    ampStart += ampStep;
                }
            }
        }

        internal void PostUpdateBufferActions(int nSamples)
        {
            // If there was latency, copy the delayed samples into the output buffer. They are read
            // from latencyBufferReadPos, which trails latencyBufferWritePos by addedLatency, so
            // what was written this chunk comes out addedLatency samples later. Both positions then
            // advance by one chunk. The ring is a pass-through when addedLatency == 0, so skip this
            // in that case.
            if (addedLatency > 0)
            {
                for (int i = 0; i < nSamples; i++)
                {
                    buffer[i] = latencyBuffer[latencyBufferReadPos];
                    if (++latencyBufferReadPos == latencyBuffer.Length) latencyBufferReadPos = 0;
                }

                // nSamples <= 256 < latencyBuffer.Length, so one wrap is enough.
                latencyBufferWritePos += nSamples;
                if (latencyBufferWritePos >= latencyBuffer.Length) latencyBufferWritePos -= latencyBuffer.Length;
            }

            burstProtection.Process(buffer, 0, nSamples, true, true);
        }

        internal void ClearBuffer(int nSamples)
        {
            for (int i = 0; i < nSamples; i++)
            {
                buffer[i].L = 0;
                buffer[i].R = 0;
            }

            if (addedLatency > 0)
            {
                int tmpLatencyBufferWritePos = latencyBufferWritePos;
                for (int i = 0; i < nSamples; i++)
                {   
                    latencyBuffer[tmpLatencyBufferWritePos].L = 0;
                    latencyBuffer[tmpLatencyBufferWritePos].R = 0;
                    if (++tmpLatencyBufferWritePos == latencyBuffer.Length) tmpLatencyBufferWritePos = 0;
                }
            }
        }

        public MachineConnectionCore(IUiDispatcher dispatcher, EngineSettings engineSettings)
        {
            interpolatorAmp.Value = amp;

            this.engineSettings = engineSettings;
            CMachineConnection = connectionHandleCounter++;
            this.dispatcher = dispatcher;

            burstProtection = new BurstProtectionEngine(Global.Buzz as ReBuzzCore, false);
        }

        public MachineConnectionCore(MachineCore source, IEnumerable<int> sourceChannels, MachineCore destination, IEnumerable<int> destinationChannels, int amp, int pan, IUiDispatcher dispatcher, EngineSettings engineSettings)
        {
            this.engineSettings = engineSettings;
            this.dispatcher = dispatcher;
            Source = source;
            Destination = destination;

            foreach (var channel in sourceChannels)
            {
                SetSourceChannel(channel, true);
            }

            foreach (var channel in destinationChannels)
            {
                SetDestinationChannel(channel, true);
            }

            Amp = amp;
            Pan = pan;
        }

        internal void ClearEvents()
        {
            Tap = null;
            PropertyChanged = null;
            burstProtection.Release();
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
