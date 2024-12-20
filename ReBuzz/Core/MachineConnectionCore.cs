using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using ReBuzz.Common;
using System;
using System.ComponentModel;
using System.Windows;

namespace ReBuzz.Core
{
    internal class MachineConnectionCore : IMachineConnection
    {
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
                        panL = Utils.FlushDenormalToZero((float)(2.0 / Math.Sqrt(2.0) * Math.Cos(pPan)));
                        panR = Utils.FlushDenormalToZero((float)(2.0 / Math.Sqrt(2.0) * Math.Sin(pPan)));

                        if (Global.EngineSettings.EqualPowerPanning)
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

        public void DoTap(Sample[] sampleBuffer, bool stereo, SongTime songTime)
        {
            if (Tap != null)
            {
                float[] samples = new float[sampleBuffer.Length * 2];
                int j = 0;
                for (int i = 0; i < sampleBuffer.Length; i++)
                {
                    samples[j] = sampleBuffer[i].L;
                    j++;
                    samples[j] = sampleBuffer[i].R;
                    j++;
                }

                dispatcher.BeginInvoke(() =>
                {
                    if (Tap != null)
                    {
                        Tap.Invoke(samples, stereo, songTime);
                    }
                });
            }
        }

        public void DoTap(int numSamples, SongTime songTime)
        {
            if (Tap != null)
            {
                float[] samples = new float[numSamples * 2];
                int j = 0;
                for (int i = 0; i < numSamples; i++)
                {
                    samples[j] = buffer[i].L;
                    j++;
                    samples[j] = buffer[i].R;
                    j++;
                }
                Tap.Invoke(samples, HasPan, songTime);
            }
        }

        internal void UpdateBuffer(Sample[] samples)
        {
            float ampStart = Utils.FlushDenormalToZero(interpolatorAmp.Value / 0x4000);
            float ampCurrent = (int)interpolatorAmp.Tick();
            float ampStep = Utils.FlushDenormalToZero(((ampStart - ampCurrent / 0x4000) / 0x4000) / samples.Length);

            for (int i = 0; i < samples.Length; i++)
            {
                Buffer[i].L = samples[i].L * ampStart * panL;
                Buffer[i].R = samples[i].R * ampStart * panR;
                ampStart += ampStep;
            }
            Utils.FlushDenormalToZero(Buffer);
        }

        internal void ClearBuffer()
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i].L = 0;
                buffer[i].R = 0;
            }
        }

        public MachineConnectionCore(IUiDispatcher dispatcher)
        {
            interpolatorAmp.Value = amp;
            this.dispatcher = dispatcher;
        }

        public MachineConnectionCore(MachineCore source, int sourceChannel, MachineCore destination, int destinationChannel, int amp, int pan, IUiDispatcher dispatcher)
        {
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

        Sample[] buffer = new Sample[256];
        private readonly IUiDispatcher dispatcher;
        public Sample[] Buffer { get => buffer; set => buffer = value; }

        public event Action<float[], bool, SongTime> Tap;
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
