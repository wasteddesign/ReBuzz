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

namespace WDE.ReBuzzAudioOut
{
	[MachineDecl(Name = "ReBuzz Audio Out", ShortName = "Audio Out", Author = "WDE", MaxTracks = 1)]
	public class ReBuzzAudioOutMachine : IBuzzMachine, INotifyPropertyChanged
	{
		IBuzzMachineHost host;

		object bufferLock = new object();

        public ReBuzzAudioOutMachine(IBuzzMachineHost host)
		{
			this.host = host;
        }

        [ParameterDecl(Name = "Dummy", DefValue = false)]
		public bool Dummy { get; set; }

        public bool Work(Sample[] output, Sample[] input, int n, WorkModes mode)
		{
			if (MachineState.Channel > 0)
			{
				Global.Buzz.AudioOut(MachineState.Channel, input, n);
                
				for (int i = 0; i < n; i++)
				{
					output[i].L = output[i].R = 0;
				}
            }
            else
            {
                for (int i = 0; i < n; i++)
                {
                    output[i].L = input[i].L;
                    output[i].R = input[i].R;
                }
            }
			return true;
		}
		
		// actual machine ends here. the stuff below demonstrates some other features of the api.
	
		public class State : INotifyPropertyChanged
		{
			public int Channel { get; set; }
			public State()
			{	
			}	// NOTE: parameterless constructor is required by the xml serializer

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
						ExecuteDelegate = p => MessageBox.Show("ReBuzz Audio Out 0.1 (C) 2024 WDE")
					}
				};
			}
		}

        public void ImportFinished(IDictionary<string, string> machineNameMap)
		{
		}

        public event PropertyChangedEventHandler PropertyChanged;
	}
}
