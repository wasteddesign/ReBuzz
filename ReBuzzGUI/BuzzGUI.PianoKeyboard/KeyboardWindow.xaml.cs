using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BuzzGUI.PianoKeyboard
{
    /// <summary>
    /// Interaction logic for KeyboardWindow.xaml
    /// </summary>
    public partial class KeyboardWindow : Window
    {
        readonly IBuzz buzz;
        readonly PianoKeyboard pianoKeyboard;

        readonly Dictionary<int, int> keysDown = new Dictionary<int, int>();

        bool sendPitchWheel = true;
        bool sendModWheel = true;
        bool sendAftertouch = true;

        public KeyboardWindow(IBuzz buzz)
        {
            new PersistentWindowPlacement(this);

            this.buzz = buzz;
            InitializeComponent();

            this.Closing += (sender, e) =>
            {
                Hide();
                e.Cancel = true;

                buzz.IsPianoKeyboardVisible = false;
            };

            this.IsVisibleChanged += (sender, e) =>
            {
                if (IsVisible)
                {
                    buzz.MIDIInput += MIDIInput;
                    buzz.PropertyChanged += buzz_PropertyChanged;

                    UpdateMIDIFocusMachine();
                }
                else
                {
                    buzz.MIDIInput -= MIDIInput;
                    buzz.PropertyChanged -= buzz_PropertyChanged;
                }
            };

            pianoKeyboard = new PianoKeyboard(this);
            Grid.SetRow(pianoKeyboard, 1);
            Grid.SetColumn(pianoKeyboard, 2);
            grid.Children.Add(pianoKeyboard);

            pianoKeyboard.OnAftertouch += (val) =>
            {
                channelAftertouchSlider.Value = val;
            };

            pianoKeyboard.OnPianoKeyDown += (key) =>
            {
                buzz.SendMIDIInput(MIDI.Encode(MIDI.NoteOn, key, velocity));
            };

            pianoKeyboard.OnPianoKeyUp += (key) =>
            {
                buzz.SendMIDIInput(MIDI.Encode(MIDI.NoteOff, key, velocity));
            };

            this.PreviewKeyDown += (sender, e) =>
            {
                if (e.KeyboardDevice.Modifiers == ModifierKeys.None)
                {
                    int i = PianoKeys.GetPianoKeyIndex(e);

                    if (i != -1 && !e.IsRepeat && !keysDown.ContainsKey(i))
                    {
                        int k = pianoKeyboard.dim.FirstMidiNote + 12 * pianoKeyboard.BaseOctave + i;

                        buzz.SendMIDIInput(MIDI.Encode(MIDI.NoteOn, k, velocity));

                        keysDown[i] = k;
                        e.Handled = true;
                    }
                }
            };

            this.PreviewKeyUp += (sender, e) =>
            {
                if (e.KeyboardDevice.Modifiers == ModifierKeys.None)
                {
                    int i = PianoKeys.GetPianoKeyIndex(e);

                    if (i != -1)
                    {
                        int k;

                        if (keysDown.ContainsKey(i))
                        {
                            k = keysDown[i];
                            keysDown.Remove(i);
                        }
                        else
                        {
                            k = pianoKeyboard.dim.FirstMidiNote + 12 * pianoKeyboard.BaseOctave + i;
                        }

                        buzz.SendMIDIInput(MIDI.Encode(MIDI.NoteOff, k, 0));

                        e.Handled = true;
                    }
                }
            };

            this.TextInput += (sender, e) =>
            {
                switch (e.Text)
                {
                    case "*":
                        if (midiFocusMachine != null && midiFocusMachine.BaseOctave < 9) midiFocusMachine.BaseOctave++;
                        break;

                    case "/":
                        if (midiFocusMachine != null && midiFocusMachine.BaseOctave > 0) midiFocusMachine.BaseOctave--;
                        break;
                }
            };

            this.LostKeyboardFocus += (sender, e) =>
            {
                foreach (var k in keysDown)
                    buzz.SendMIDIInput(MIDI.Encode(MIDI.NoteOff, k.Value, 0));

                keysDown.Clear();
            };

            pitchWheel.LostMouseCapture += (sender, e) =>
            {
                pitchWheel.Value = 0;
            };

            pitchWheel.ValueChanged += (sender, e) =>
            {
                if (sendPitchWheel)
                {
                    int v = (int)pitchWheel.Value + 8192;
                    buzz.SendMIDIInput(MIDI.Encode(MIDI.PitchWheel, v & 127, v >> 7));
                }
            };

            modWheel.ValueChanged += (sender, e) =>
            {
                if (sendModWheel)
                {
                    int v = (int)modWheel.Value;
                    buzz.SendMIDIInput(MIDI.Encode(MIDI.ControlChange, MIDI.CCModWheel, v));
                }
            };

            velocitySlider.ValueChanged += (sender, e) => { velocity = (int)velocitySlider.Value; };

            channelAftertouchSlider.ValueChanged += (sender, e) =>
            {
                if (sendAftertouch)
                {
                    int v = (int)channelAftertouchSlider.Value;
                    buzz.SendMIDIInput(MIDI.Encode(MIDI.ChannelAftertouch, v, 0));
                }
            };

            allSoundOff.Click += (sender, e) => { buzz.SendMIDIInput(MIDI.Encode(MIDI.ControlChange, MIDI.CMMAllSoundOff, 0)); };
            allNotesOff.Click += (sender, e) => { buzz.SendMIDIInput(MIDI.Encode(MIDI.ControlChange, MIDI.CMMAllNotesOff, 0)); };

            sustain.Click += (sender, e) =>
            {
                buzz.SendMIDIInput(MIDI.Encode(MIDI.ControlChange, MIDI.CCSustain, (bool)sustain.IsChecked ? 127 : 0));
            };

            pianoKeyboard.BaseOctave = 4;
        }

        IMachine midiFocusMachine;

        void UpdateMIDIFocusMachine()
        {
            if (midiFocusMachine != null)
            {
                midiFocusMachine.PropertyChanged -= midiFocusMachine_PropertyChanged;
            }

            midiFocusMachine = buzz.MIDIFocusMachine;

            if (midiFocusMachine != null)
            {
                midiFocusMachine.PropertyChanged += midiFocusMachine_PropertyChanged;

                pianoKeyboard.BaseOctave = midiFocusMachine.BaseOctave;
            }

        }

        void buzz_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "MIDIFocusMachine":
                    UpdateMIDIFocusMachine();

                    break;
            }

        }

        void midiFocusMachine_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "BaseOctave":
                    pianoKeyboard.BaseOctave = midiFocusMachine.BaseOctave;
                    break;
            }

        }


        int velocity = 100;

        void MIDIInput(int mididata)
        {
            int status = mididata & 0xff;
            int data1 = (mididata >> 8) & 0xff;
            int data2 = (mididata >> 16) & 0xff;

            if (status == MIDI.NoteOn)
            {
                if (data2 > 0)
                    pianoKeyboard.PianoKeyDown(data1);
                else
                    pianoKeyboard.PianoKeyUp(data1);
            }
            else if (status == MIDI.NoteOff)
            {
                pianoKeyboard.PianoKeyUp(data1);
            }
            else if (status == MIDI.ControlChange)
            {
                switch (data1)
                {
                    case MIDI.CCSustain:
                        sustain.IsChecked = data2 >= 64;
                        break;

                    case MIDI.CCModWheel:
                        if (!modWheel.IsMouseCaptureWithin)
                        {
                            if (data2 != modWheel.Value)
                            {
                                sendModWheel = false;
                                modWheel.Value = data2;
                                sendModWheel = true;
                            }
                        }
                        break;
                }
            }
            else if (status == MIDI.PitchWheel)
            {
                if (!pitchWheel.IsMouseCaptureWithin)
                {
                    int v = (data1 | (data2 << 7)) - 8192;
                    if (v != pitchWheel.Value)
                    {
                        sendPitchWheel = false;
                        pitchWheel.Value = v;
                        sendPitchWheel = true;
                    }
                }

            }
            else if (status == MIDI.ChannelAftertouch)
            {
                if (data1 != channelAftertouchSlider.Value)
                {
                    sendPitchWheel = false;
                    channelAftertouchSlider.Value = data1;
                    sendPitchWheel = true;
                }
            }
        }
    }
}
