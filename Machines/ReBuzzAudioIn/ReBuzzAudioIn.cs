using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.ComponentModel;
using Buzz.MachineInterface;
using BuzzGUI.Interfaces;
using BuzzGUI.Common;

namespace WDE.ReBuzzAudioIn
{
	[MachineDecl(Name = "ReBuzz Audio In", ShortName = "Audio In", Author = "WDE", MaxTracks = 1)]
	public class ReBuzzAudioInMachine : IBuzzMachine, INotifyPropertyChanged
	{
		IBuzzMachineHost host;
        float[] recordBuffer;
		int writeStereoBufferPointer;
		int readBufferPointer;
		int bufferFillLevel = 0;

		object bufferLock = new object();

        public ReBuzzAudioInMachine(IBuzzMachineHost host)
		{
			this.host = host;
			InitCapture();
        }

		internal void InitCapture()
		{
			lock (bufferLock)
			{
				ReleaseCapture();

                bufferFillLevel = 0;
                writeStereoBufferPointer = 0;
                readBufferPointer = 0;

                int bufferSize = machineState.BufferSize * 2;
                recordBuffer = new float[bufferSize];

                Global.Buzz.AudioReceived += Buzz_AudioReceived;
            }
        }

        private void Buzz_AudioReceived(float[] buffer, int frames, int channels)
        {
            lock (bufferLock)
            {
                int stereoChannels = channels >> 1;
                int pairIndex = Math.Min(stereoChannels - 1, Math.Max(machineState.Channel, 0));

                int chL = pairIndex * 2;
                int chR = chL + 1;

                int framesRemaining = frames;
                int bufferFrameOffset = 0;

                while (framesRemaining > 0)
                {
                    int frameCount = framesRemaining;

                    // How many stereo samples fit before wrap?
                    int samplesAvailable = recordBuffer.Length - writeStereoBufferPointer;
                    int framesAvailable = samplesAvailable / 2;

                    if (frameCount > framesAvailable)
                        frameCount = framesAvailable;

                    // Copy selected stereo pair
                    int writePos = writeStereoBufferPointer;

                    for (int i = 0; i < frameCount; i++)
                    {
                        int srcFrame = bufferFrameOffset + i;
                        int srcBase = srcFrame * channels;

                        recordBuffer[writePos] = buffer[srcBase + chL];
                        recordBuffer[writePos + 1] = buffer[srcBase + chR];

                        writePos += 2;
                    }

                    writeStereoBufferPointer = writePos;

                    // Wrap if needed
                    if (writeStereoBufferPointer >= recordBuffer.Length)
                        writeStereoBufferPointer = 0;

                    bufferFrameOffset += frameCount;
                    framesRemaining -= frameCount;

                    bufferFillLevel += frameCount * 2;
                    if (bufferFillLevel > recordBuffer.Length)
                        bufferFillLevel = recordBuffer.Length;
                }
            }
        }

        internal void ReleaseCapture()
		{
			Global.Buzz.AudioReceived -= Buzz_AudioReceived;
        }

        [ParameterDecl(ValueDescriptions = new[] { "no", "yes" })]
		public bool Bypass { get; set; }

		public unsafe bool Work(Sample[] output, int n, WorkModes mode)
		{
			if (n > bufferFillLevel / 2)
			{
				return false;
			}
			if (Bypass)
			{
                return false;
            }

			lock (bufferLock)
			{
				for (int i = 0; i < n; i++)
				{
					if (machineState.NumChannels == 0)
					{
						output[i].L = output[i].R = recordBuffer[readBufferPointer] * 32768.0f;
                        readBufferPointer++;
						bufferFillLevel--;

						if (readBufferPointer >= recordBuffer.Length)
							readBufferPointer = 0;

                        readBufferPointer++;
						bufferFillLevel--;

                        if (readBufferPointer >= recordBuffer.Length)
                            readBufferPointer = 0;
                    }
					else
					{
                        output[i].L = recordBuffer[readBufferPointer] * 32768.0f;
                        readBufferPointer++;
                        bufferFillLevel--;

                        if (readBufferPointer >= recordBuffer.Length)
                            readBufferPointer = 0;

                        output[i].R = recordBuffer[readBufferPointer] * 32768.0f;
                        readBufferPointer++;
                        bufferFillLevel--;

                        if (readBufferPointer >= recordBuffer.Length)
                            readBufferPointer = 0;
                    }
				}
            }

			return true;
		}
		
		// actual machine ends here. the stuff below demonstrates some other features of the api.
	
		public class State : INotifyPropertyChanged
		{
            // Input stereo channel to capture from
            public int Channel { get; set; }
            public State()
			{	
				numChannels = 0;
				bufferSize = 1024;
			}   // NOTE: parameterless constructor is required by the xml serializer

            // Mono or stereo input. 1 = mono, 2 = stereo
            int numChannels;
			public int NumChannels
			{
				get { return numChannels; }
				set
				{
					numChannels = Math.Min(1, Math.Max(0, value));
                    if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("NumChannels"));
                }
			}

            int bufferSize;

            public int BufferSize
            {
                get { return bufferSize; }
                set
                {
                    bufferSize = value;
                    if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("BufferSize"));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
		}

		State machineState = new State();
		public State MachineState			// a property called 'MachineState' gets automatically saved in songs and presets
		{
			get { return machineState; }
			set
			{
				machineState = value;
				if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("MachineState"));
                InitCapture();
            }
		}		
		
		public IEnumerable<IMenuItem> Commands
		{
			get
			{
                var g = new MenuItemVM.Group();

                yield return new MenuItemVM()
                {
                    Text = "Channel",
                    Children = Enumerable.Range(0, 32).Select(i => new MenuItemVM()
                    {
                        Text = "" + i,
                        IsCheckable = true,
                        CheckGroup = g,
                        StaysOpenOnClick = true,
                        IsChecked = i == MachineState.Channel,
                        CommandParameter = i,
                        Command = new SimpleCommand()
                        {
                            CanExecuteDelegate = p => true,
                            ExecuteDelegate = p => MachineState.Channel = (int)p
                        }
                    })
                };

                yield return new MenuItemVM() 
				{ 
					Text = "About...", 
					Command = new SimpleCommand()
					{
						CanExecuteDelegate = p => true,
						ExecuteDelegate = p => MessageBox.Show("ReBuzz Audio In 0.2 (C) 2024 WDE")
					}
				};
			}
		}

        public void ImportFinished(IDictionary<string, string> machineNameMap)
		{
			InitCapture();
		}

        public event PropertyChangedEventHandler PropertyChanged;
	}

	public class MachineGUIFactory : IMachineGUIFactory { public IMachineGUI CreateGUI(IMachineGUIHost host) { return new ReBuzzAudioInGUI(); } }
	public class ReBuzzAudioInGUI : UserControl, IMachineGUI
	{
		IMachine machine;
		ReBuzzAudioInMachine audioInMachine;
		
		ComboBox cbChannles;
        ComboBox cbLatency;

        public IMachine Machine
		{
			get { return machine; }
			set
			{
				if (machine != null)
				{
					BindingOperations.ClearBinding(cbChannles, ComboBox.SelectedItemProperty);
                    BindingOperations.ClearBinding(cbLatency, ComboBox.SelectedItemProperty);
                }

				machine = value;

				if (machine != null)
				{
					audioInMachine = (ReBuzzAudioInMachine)machine.ManagedMachine;
					cbChannles.SetBinding(ComboBox.SelectedIndexProperty, new Binding("MachineState.NumChannels") { Source = audioInMachine, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
                    cbLatency.SetBinding(ComboBox.SelectedItemProperty, new Binding("MachineState.BufferSize") { Source = audioInMachine, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

                    cbLatency.SelectedItem = audioInMachine.MachineState.BufferSize;

					cbChannles.SelectionChanged += (s, e) =>
					{
						audioInMachine.InitCapture();
                    };
					cbLatency.SelectionChanged += (s, e) =>
					{
                        audioInMachine.InitCapture();
                    };

                }
			}
		}

        public ReBuzzAudioInGUI()
		{
			Grid mainGrid = new Grid();
			mainGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(140) });
			mainGrid.ColumnDefinitions.Add(new ColumnDefinition() { });

			mainGrid.RowDefinitions.Add(new RowDefinition());
			mainGrid.RowDefinitions.Add(new RowDefinition());

			TextBlock tb;
            tb = new TextBlock() { Margin = new Thickness(0, 0, 0, 4), AllowDrop = false, Text="Input Type" };
            Grid.SetRow(tb, 0);
            mainGrid.Children.Add(tb);
			
            cbChannles = new ComboBox() { Margin = new Thickness(0, 0, 0, 4), AllowDrop = false };
			cbChannles.Items.Add("Mono");
            cbChannles.Items.Add("Stereo");
			Grid.SetColumn(cbChannles, 1);
            Grid.SetRow(cbChannles, 0);
            mainGrid.Children.Add(cbChannles);

            tb = new TextBlock() { Margin = new Thickness(0, 0, 0, 4), AllowDrop = false, Text = "Buffer Size" };
            Grid.SetRow(tb, 1);
            mainGrid.Children.Add(tb);

            cbLatency = new ComboBox() { Margin = new Thickness(0, 0, 0, 4), AllowDrop = false };
			for (int i = 16; i <= 1024*64; i*=2)
				cbLatency.Items.Add(i);
			
            Grid.SetColumn(cbLatency, 1);
            Grid.SetRow(cbLatency, 2);
            mainGrid.Children.Add(cbLatency);

            this.Content = mainGrid;	
		}
	}
}
