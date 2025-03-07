using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace WDE.MultioIOEffect
{
    [MachineDecl(Name = "MultiIO Effect", ShortName = "MultiIO", Author = "WDE", MaxTracks = 8, InputCount = 4, OutputCount = 5)]
    public class MultiIOEffect : IBuzzMachine, INotifyPropertyChanged
    {
        readonly IBuzzMachineHost host;
        Sample[] dBuffer;
        int delayIndex;
        readonly Random rand = new Random(1234);

        public MultiIOEffect(IBuzzMachineHost host)
        {
            this.host = host;

            DelayTime = 300;

            Global.Buzz.Song.MachineAdded += Song_MachineAdded;
            Global.Buzz.Song.MachineRemoved += Song_MachineRemoved;
        }

        private void Song_MachineAdded(IMachine obj)
        {
            if (obj == host.Machine)
            {
                // Test to change out/in counts dynamically
                host.InputChannelCount = 3;
                host.OutputChannelCount = 3;
            }
        }

        private void Song_MachineRemoved(IMachine obj)
        {
            if (obj == host.Machine)
            {
                Global.Buzz.Song.MachineAdded -= Song_MachineAdded;
                Global.Buzz.Song.MachineRemoved -= Song_MachineRemoved;
            }
        }

        int delayTime;
        [ParameterDecl(Name = "Delay Time", Description = "Delay time in milliseconds.", MinValue = 100, MaxValue = 2000, DefValue = 300)]
        public int DelayTime
        {
            get => delayTime;
            set
            {
                int sampleRate = Global.Buzz.SelectedAudioDriverSampleRate;
                int bufferSize = (int)Math.Ceiling(value / 1000.0 * sampleRate);
                if (bufferSize < 1) bufferSize = 1;

                // Create the buffer
                this.dBuffer = new Sample[bufferSize];
            }
        }

        [ParameterDecl(Name = "Delay Feedback", Description = "Delay Feedback.", MinValue = 0, MaxValue = 100, DefValue = 50)]
        public int DelayFeedback { get; set; }

        [ParameterDecl(Name = "Noise Volume", Description = "Noise Volume.", MinValue = 0, MaxValue = 100, DefValue = 50)]
        public int NoiseVolume { get; set; }


        public bool Work(IList<Sample[]> output, IList<Sample[]> input, int n, WorkModes mode)
        {
            Sample[] noiseBuffer = null;
            Sample[] delayBuffer = null;
            Sample[] bypassBuffer = null;

            // Inputs
            // Clean input
            if (input[0] != null)
            {
                bypassBuffer = input[0];
            }

            // Noise input
            if (input[1] != null)
            {
                float min = 0, max = 0;
                noiseBuffer = input[1];
                for (int i = 0; i < n; i++)
                {
                    float v = noiseBuffer[i].L + noiseBuffer[i].R / 2.0f;
                    min = Math.Min(v, min);
                    max = Math.Max(v, max);
                }
                float level = (max - min) / 65535.0f;
                for (int i = 0; i < n; i++)
                {
                    noiseBuffer[i].L += rand.Next(100000) * NoiseVolume * level / 100.0f;
                    noiseBuffer[i].R += rand.Next(100000) * NoiseVolume * level / 100.0f;
                }
            }

            // Delay input
            if (input[2] != null)
            {
                delayBuffer = input[2];
                for (int i = 0; i < n; i++)
                {
                    Sample delaySample = delayBuffer[i] + dBuffer[delayIndex] * DelayFeedback / 100.0f;
                    dBuffer[delayIndex] = delaySample;
                    delayBuffer[i] = delaySample;
                    delayIndex++;
                    if (delayIndex >= dBuffer.Length)
                        delayIndex = 0;
                }
            }

            // Outputs
            // Mix
            if (output[0] != null)
            {
                var buf = output[0];

                for (int i = 0; i < n; i++)
                {
                    buf[i].L = 0;
                    buf[i].R = 0;
                }

                if (bypassBuffer != null)
                {
                    for (int i = 0; i < n; i++)
                    {
                        buf[i].L += bypassBuffer[i].L;
                        buf[i].R += bypassBuffer[i].R;
                    }
                }

                if (noiseBuffer != null)
                {
                    for (int i = 0; i < n; i++)
                    {
                        buf[i].L += noiseBuffer[i].L;
                        buf[i].R += noiseBuffer[i].R;
                    }
                }

                if (delayBuffer != null)
                {
                    for (int i = 0; i < n; i++)
                    {
                        buf[i].L += delayBuffer[i].L;
                        buf[i].R += delayBuffer[i].R;
                    }
                }
            }

            // Noise
            if (output[1] != null)
            {
                var buf = output[1];

                for (int i = 0; i < n; i++)
                {
                    buf[i].L = 0;
                    buf[i].R = 0;
                }

                if (noiseBuffer != null)
                {
                    for (int i = 0; i < n; i++)
                    {
                        buf[i].L += noiseBuffer[i].L;
                        buf[i].R += noiseBuffer[i].R;
                    }
                }
            }

            // Delay
            if (output[2] != null)
            {
                var buf = output[2];

                for (int i = 0; i < n; i++)
                {
                    buf[i].L = 0;
                    buf[i].R = 0;
                }

                if (delayBuffer != null)
                {
                    for (int i = 0; i < n; i++)
                    {
                        buf[i].L += delayBuffer[i].L;
                        buf[i].R += delayBuffer[i].R;
                    }
                }
            }
            return true;
        }

        public string GetChannelName(bool input, int index)
        {
            if (input)
            {
                switch (index)
                {
                    case 0:
                        return "Bypass";
                    case 1:
                        return "Noise";
                    case 2:
                        return "Delay";
                }
            }
            else
            {
                switch (index)
                {
                    case 0:
                        return "Mix";
                    case 1:
                        return "Noise";
                    case 2:
                        return "Delay";
                }
            }
            return "";
        }


        // actual machine ends here. the stuff below demonstrates some other features of the api.

        public class State : INotifyPropertyChanged
        {
            public State() {}  // NOTE: parameterless constructor is required by the xml serializer

            public event PropertyChangedEventHandler PropertyChanged;
        }

        State machineState = new State();
        public State MachineState           // a property called 'MachineState' gets automatically saved in songs and presets
        {
            get { return machineState; }
            set
            {
                machineState = value;
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("MachineState"));
            }
        }

        int checkedItem = 1;

        public IEnumerable<IMenuItem> Commands
        {
            get
            {
                yield return new MenuItemVM()
                {
                    Text = "About...",
                    Command = new SimpleCommand()
                    {
                        CanExecuteDelegate = p => true,
                        ExecuteDelegate = p => MessageBox.Show("About")
                    }
                };
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class MachineGUIFactory : IMachineGUIFactory { public IMachineGUI CreateGUI(IMachineGUIHost host) { return new MultiIOGui(); } }
    public class MultiIOGui : UserControl, IMachineGUI
    {
        IMachine machine;
        MultiIOEffect mioMachine;

        public IMachine Machine
        {
            get { return machine; }
            set
            {
                if (machine != null)
                {
                }

                machine = value;

                if (machine != null)
                {
                    mioMachine = (MultiIOEffect)machine.ManagedMachine;
                }
            }
        }

        public MultiIOGui()
        {
        }
    }
}
