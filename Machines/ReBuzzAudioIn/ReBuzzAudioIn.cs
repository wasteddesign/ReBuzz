using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.ComponentModel;
using System.Collections.ObjectModel;
using Buzz.MachineInterface;
using BuzzGUI.Interfaces;
using BuzzGUI.Common;
using NAudio.Wave;
using System.Runtime.CompilerServices;
using NAudio.CoreAudioApi;
using System.Runtime.InteropServices;
using BuzzGUI.Common.Templates;
using NAudio;
using libsndfile;

namespace WDE.ReBuzzAudioIn
{
	[MachineDecl(Name = "ReBuzz Audio In", ShortName = "Audio In", Author = "WDE", MaxTracks = 1)]
	public class ReBuzzAudioInMachine : IBuzzMachine, INotifyPropertyChanged
	{
		IBuzzMachineHost host;
        float[] recordBuffer;
		int writeBufferPointer;
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
                writeBufferPointer = 0;
                readBufferPointer = 0;

                int bufferSize = machineState.BufferSize * 2;
                recordBuffer = new float[bufferSize];

                Global.Buzz.AudioReceived += Buzz_AudioReceived;
            }
        }

        private void Buzz_AudioReceived(float[] buffer, int n)
        {
			lock (bufferLock)
			{
				int countRemaining = n;
				int bufferOffset = 0;
				while (countRemaining > 0)
				{
					int count = countRemaining;
					if (writeBufferPointer + count > recordBuffer.Length)
					{
						count = recordBuffer.Length - writeBufferPointer;
                    }

					Buffer.BlockCopy(buffer, bufferOffset * 4, recordBuffer, writeBufferPointer * 4, count * 4);
					writeBufferPointer += count;
					bufferOffset += count;

					if (writeBufferPointer == recordBuffer.Length)
						writeBufferPointer = 0;
					countRemaining -= count;
					bufferFillLevel += count;
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
					if (machineState.NumChannels == 1)
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
			public State()
			{	
				numChannels = 1;
				bufferSize = 1024;
			}	// NOTE: parameterless constructor is required by the xml serializer

			int numChannels;
			public int NumChannels
			{
				get { return numChannels; }
				set
				{
					numChannels = value;
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
					cbChannles.SetBinding(ComboBox.SelectedItemProperty, new Binding("MachineState.NumChannels") { Source = audioInMachine, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
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
            tb = new TextBlock() { Margin = new Thickness(0, 0, 0, 4), AllowDrop = false, Text="Channels" };
            Grid.SetRow(tb, 0);
            mainGrid.Children.Add(tb);
			
            cbChannles = new ComboBox() { Margin = new Thickness(0, 0, 0, 4), AllowDrop = false };
			cbChannles.Items.Add(1);
            cbChannles.Items.Add(2);
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
