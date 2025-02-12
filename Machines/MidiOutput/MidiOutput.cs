using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.ComponentModel;
using System.Collections.ObjectModel;
using Buzz.MachineInterface;
using BuzzGUI.Interfaces;
using BuzzGUI.Common;

namespace WDE.MidiOutput
{
    [MachineDecl(Name = "Midi Output", ShortName = "Midi Out", Author = "WDE", MaxTracks = 32)]
	public class MidiOutputMachine : IBuzzMachine, INotifyPropertyChanged
	{
		IBuzzMachineHost host;

		internal class NoteEvent
		{
			internal int device;
			internal Note note;
            internal int track;
            internal int velocity;
            internal int delayInSamples;
            internal int cutInSamples;
			internal int channel;

            public bool NoteOnHandled { get; internal set; }

			internal NoteEvent Copy()
			{
				NoteEvent e = new NoteEvent();
				e.device = device;
				e.note = note;
				e.velocity = velocity;
				e.channel = channel;
				e.delayInSamples = delayInSamples;
				e.cutInSamples = cutInSamples;
				e.track = track;
				return e;
			}
        }

        internal class MidiCCEvent
        {
            internal int control;
            internal int value;
            internal int channel;
            internal int device;
        }

        Dictionary<int, NoteEvent> pNote = new Dictionary<int, NoteEvent>();
        Dictionary<int, NoteEvent> noteEvents = new Dictionary<int, NoteEvent>();
        Dictionary<int, MidiCCEvent> ccEvents = new Dictionary<int, MidiCCEvent>();
        public MidiOutputMachine(IBuzzMachineHost host)
		{
			this.host = host;
        }

        NoteEvent GetNoteEvent(int track)
		{
			if (!noteEvents.ContainsKey(track))
				noteEvents.Add(track, new NoteEvent());

			return noteEvents[track];
		}

        MidiCCEvent GetCCEvent(int track)
        {
            if (!ccEvents.ContainsKey(track))
                ccEvents.Add(track, new MidiCCEvent());

            return ccEvents[track];
        }

        [ParameterDecl(MinValue = 0, MaxValue = 255, DefValue = 0)]
        public void Device(int value, int track)
        {
		}

        [ParameterDecl(IsStateless = true)]
		public void Note(Note value, int track)
		{
			GetNoteEvent(track).note = value;
        }

        [ParameterDecl(IsStateless = true, MinValue = 0, MaxValue =127, DefValue = 120)]
        public void Volume(int value, int track)
        {
        }

        [ParameterDecl(IsStateless = true, MinValue = 0, MaxValue = 127, DefValue = 0)]
        public void Delay(int value, int track)
        {
            GetNoteEvent(track).delayInSamples = (int)(value / 127.0 * host.MasterInfo.SamplesPerTick);
        }

        [ParameterDecl(IsStateless = true, MinValue = 0, MaxValue = 127, DefValue = 0)]
        public void Cut(int value, int track)
        {
            GetNoteEvent(track).cutInSamples = (int)(value / 127.0 * host.MasterInfo.SamplesPerTick);
        }

        [ParameterDecl(MinValue = 0, MaxValue = 254, DefValue = 0)]
        public void MidiCC(int value, int track)
        {	
        }

        [ParameterDecl(MinValue = 0, MaxValue = 0xfffe, DefValue = 0)]
        public void MidiCCValue(int value, int track)
        {
            GetCCEvent(track).value = value;
        }

        [ParameterDecl(MinValue = 0, MaxValue = 15, DefValue = 0)]
        public void Channel(int value, int track)
        {
        }

        public bool Work(Sample[] output, int n, WorkModes mode)
		{

			for (int t = 0; t < host.Machine.TrackCount; t++)
			{
				var pg = host.Machine.ParameterGroups[2];

				if (noteEvents.ContainsKey(t))
				{
					if (noteEvents[t].note.Value != 0)
					{
                        NoteEvent ne = noteEvents[t];
                        ne.device = pg.Parameters[0].GetValue(t);
                        ne.velocity = pg.Parameters[2].GetValue(t);
                        ne.channel = pg.Parameters[5].GetValue(t);
                    }
					else
					{
						noteEvents.Remove(t);
					}
                }

				if (ccEvents.ContainsKey(t))
				{
					var ce = ccEvents[t];
                    ce.device = pg.Parameters[0].GetValue(t);
                    ce.control = pg.Parameters[3].GetValue(t);
                    ce.channel = pg.Parameters[5].GetValue(t);
                }
			}

			foreach (var t in noteEvents.Keys.ToArray())
			{
				var ne = noteEvents[t];
				ne.delayInSamples -= n;
                ne.cutInSamples -= n;

                if (ne.delayInSamples <= 0)
				{
					if (ne.note.Value == BuzzNote.Off)
					{
						MidiNoteOff(t);
                        noteEvents.Remove(t);
                    }
					else if (!ne.NoteOnHandled)
					{
						MidiNoteOff(t);
						int midiNote = BuzzNote.ToMIDINote(ne.note.Value);
						pNote[t] = ne.Copy();
						var data = MIDI.Encode(MIDI.NoteOn, midiNote, ne.velocity);
						Global.Buzz.SendMIDIOutput(ne.device, data);
						ne.NoteOnHandled = true;

						if (ne.delayInSamples >= ne.cutInSamples)
						{
							// Noi need to cut == no need to send note off
                            noteEvents.Remove(t);
                        }
					}
					else if (ne.delayInSamples < ne.cutInSamples && ne.cutInSamples <= 0)
					{
						MidiNoteOff(t);
                        noteEvents.Remove(t);
                    }
				}
			}

			foreach (var t in ccEvents.Keys.ToArray())
			{
				var ce = ccEvents[t];
				int v = ce.value;
				if (ce.control == MIDI.PitchWheel)
				{
                    var data = MIDI.Encode(MIDI.PitchWheel, v & 127, v >> 7);
					Global.Buzz.SendMIDIOutput(ce.device, data);
                }
				else
				{
					var data = MIDI.Encode(MIDI.ControlChange, ce.control, v);
					Global.Buzz.SendMIDIOutput(ce.device, data);
                }
				
            }
			
			return false;
		}

		void MidiNoteOff(int t)
		{
            if (pNote.ContainsKey(t))
            {
                var pn = pNote[t];
                int midiNote = BuzzNote.ToMIDINote(pn.note.Value);
                var data = MIDI.Encode(MIDI.NoteOff, midiNote, pn.velocity);
                pNote.Remove(t);
                Global.Buzz.SendMIDIOutput(pn.device, data);
            }
        }
		
		public class State : INotifyPropertyChanged
		{
			public State()
			{	
			}	// NOTE: parameterless constructor is required by the xml serializer

            public event PropertyChangedEventHandler PropertyChanged;
		}

		State machineState = new State();
        private bool sendCC;

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
						ExecuteDelegate = p => MessageBox.Show("ReBuzz Midi Output 0.1 (C) 2024 WDE")
					}
				};
			}
		}

        public void ImportFinished(IDictionary<string, string> machineNameMap)
		{
		}

        public event PropertyChangedEventHandler PropertyChanged;
	}

	public class MachineGUIFactory : IMachineGUIFactory { public IMachineGUI CreateGUI(IMachineGUIHost host) { return new MidiOutputGUI(); } }
	public class MidiOutputGUI : UserControl, IMachineGUI
	{
		IMachine machine;
		MidiOutputMachine midiOutputMachine;
		

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
					midiOutputMachine = (MidiOutputMachine)machine.ManagedMachine;
                }
			}
		}

        // view model for machine list box items
        public class MidiOutsVM
        {
            public int DeviceNum { get; private set; }
            public string Name { get; private set; }

            public MidiOutsVM(int n, string name) { DeviceNum = n; Name = name; }
            public override string ToString() { return DeviceNum + ": " + Name; }
        }

        public ObservableCollection<MidiOutsVM> MidiOuts { get; private set; }

        public MidiOutputGUI()
		{
			MidiOuts = new ObservableCollection<MidiOutsVM>();

			StackPanel sp = new StackPanel();
			ListBox lb = new ListBox() { Height = 160, Margin = new Thickness(0, 0, 0, 4) };

			IsVisibleChanged += (sender, e) =>
			{
				if (Visibility == Visibility.Visible)
					UpdateMidiOuts();

            };

            lb.SetBinding(ListBox.ItemsSourceProperty, new Binding("MidiOuts") { Source = this, Mode = BindingMode.OneWay });
			sp.Children.Add(lb);

            this.Content = sp;	
		}

		void UpdateMidiOuts()
		{
            MidiOuts.Clear();

            foreach (var mo in Global.Buzz.GetMidiOuts())
                MidiOuts.Add(new MidiOutsVM(mo.Item1, mo.Item2));
        }
	}
}
