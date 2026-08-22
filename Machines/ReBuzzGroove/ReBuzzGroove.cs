using System.Windows;
using System.ComponentModel;
using Buzz.MachineInterface;
using BuzzGUI.Interfaces;
using BuzzGUI.Common;

namespace WDE.ReBuzzGroove
{
	[MachineDecl(Name = "ReBuzz Groove", ShortName = "Groove", Author = "WDE", MaxTracks = 1)]
	public class ReBuzzAudioOutMachine : IBuzzMachine, INotifyPropertyChanged
	{
        private IBuzzMachineHost host;
        int[] grooveDataParams = new int[8];

        public ReBuzzAudioOutMachine(IBuzzMachineHost host)
		{
			this.host = host;
        }

        private int numSteps;
        [ParameterDecl(Name = "NumSteps", DefValue = 0, MinValue = 0, MaxValue = 8, Description = "Number of steps in Groove. 0 = off.")]
        public int NumSteps { get => numSteps; set { numSteps = value; UpdateGroove(); } }

        [ParameterDecl(Name = "Step1", DefValue = 0, MinValue = 0, MaxValue = 127, Description = "Tick speed multiplyer = 1.0f + value * 2.0f / 127.0f")]
		public int Step1 { get => grooveDataParams[0]; set { grooveDataParams[0] = value; UpdateGroove(); } }

        [ParameterDecl(Name = "Step2", DefValue = 0, MinValue = 0, MaxValue = 127, Description = "Tick speed multiplyer = 1.0f + value * 2.0f / 127.0f")]
        public int Step2 { get => grooveDataParams[1]; set { grooveDataParams[1] = value; UpdateGroove(); } }

        [ParameterDecl(Name = "Step3", DefValue = 0, MinValue = 0, MaxValue = 127, Description = "Tick speed multiplyer = 1.0f + value * 2.0f / 127.0f")]
        public int Step3 { get => grooveDataParams[2]; set { grooveDataParams[2] = value; UpdateGroove(); } }

        [ParameterDecl(Name = "Step4", DefValue = 0, MinValue = 0, MaxValue = 127, Description = "Tick speed multiplyer = 1.0f + value * 2.0f / 127.0f")]
        public int Step4 { get => grooveDataParams[3]; set { grooveDataParams[3] = value; UpdateGroove(); } }

        [ParameterDecl(Name = "Step5", DefValue = 0, MinValue = 0, MaxValue = 127, Description = "Tick speed multiplyer = 1.0f + value * 2.0f / 127.0f")]
        public int Step5 { get => grooveDataParams[4]; set { grooveDataParams[4] = value; UpdateGroove(); } }

        [ParameterDecl(Name = "Step6", DefValue = 0, MinValue = 0, MaxValue = 127, Description = "Tick speed multiplyer = 1.0f + value * 2.0f / 127.0f")]
        public int Step6 { get => grooveDataParams[5]; set { grooveDataParams[5] = value; UpdateGroove(); } }

        [ParameterDecl(Name = "Step7", DefValue = 0, MinValue = 0, MaxValue = 127, Description = "Tick speed multiplyer = 1.0f + value * 2.0f / 127.0f")]
        public int Step7 { get => grooveDataParams[6]; set { grooveDataParams[6] = value; UpdateGroove(); } }

        [ParameterDecl(Name = "Step8", DefValue = 0, MinValue = 0, MaxValue = 127, Description = "Tick speed multiplyer = 1.0f + value * 2.0f / 127.0f")]
        public int Step8 { get => grooveDataParams[7]; set { grooveDataParams[7] = value; UpdateGroove(); } }

        public void Work()
		{
			if (updateGroove)
			{
                float[] grooveData = new float[NumSteps];

                for (int i = 0; i < grooveData.Length; i++)
                {
                    grooveData[i] = 1.0f + grooveDataParams[i] * 2.0f / 127f;
                }

				Global.Buzz.SetGroovePattern(grooveData);
                updateGroove = false;
            }
		}

		void UpdateGroove()
		{

            updateGroove = true;
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
        private bool updateGroove;

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
				yield return new MenuItemVM()
				{
					Text = "About...",
					Command = new SimpleCommand()
					{
						CanExecuteDelegate = p => true,
						ExecuteDelegate = p => MessageBox.Show(@"ReBuzz Groove 0.1 (C) 2026 WDE")
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
