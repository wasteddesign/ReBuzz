using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace WDE.ModernPatternEditor.Chords
{
    /// <summary>
    /// Interaction logic for ChordsWindow.xaml
    /// </summary>
    public partial class ChordsWindow : Window, INotifyPropertyChanged
    {
        int transpose = 0;
        public int Transpose
        {
            get
            {
                return transpose;
            }
            set
            {
                transpose = value;
                PropertyChanged.Raise(this, "Transpose");
                currentLine = 0;
                chordsGrid.Children.Clear();
                dispatchTimer.Start();
            }
        }
        ChordMapping[] ChordMappings { get; set; }
        public PatternEditor Editor { get; private set; }
        public int MidiChannel { get; private set; }
        public int MidiVolume { get; private set; }

        public List<StepMapping> StepMappings { get; set; }
        public StepMapping SelectedStepMapping { get; set; }

        public ChordSet[] ChordSets { get; internal set; }
        
        internal ChordSet selectedCholdSet;

        public ChordSet SelectedChordSet { get { return selectedCholdSet; }
            set
            {
                selectedCholdSet = value;
                if (selectedCholdSet != null)
                    ChordMappings = selectedCholdSet.Mappings;
                RebuildChordsView();
            }
        }

        DispatcherTimer dispatchTimer;
        int currentLine = 0;
        private readonly int MidiAllNotesOff = 123;

        public ChordsWindow(PatternEditor editor)
        {
            this.Editor = editor;
            MidiChannel = 0;
            MidiVolume = 120;
            DataContext = this;
            dispatchTimer = new DispatcherTimer();

            ResourceDictionary rd = GetBuzzThemeResources();
            if (rd != null) this.Resources.MergedDictionaries.Add(rd);

            InitializeComponent();

            this.Title = "Chords - " + Editor.SelectedMachine.Name;

            this.Loaded += (sender, e) =>
            {
                //ChordMappings = ChordMappingFile.Default.ChordSets[0].Mappings;

                ChordSets = ChordMappingFile.Default.ChordSets;
                if(ChordSets != null)
                {
                    SelectedChordSet = ChordSets[0];
                    ChordMappings = SelectedChordSet.Mappings;
                    PropertyChanged.Raise(this, "ChordSets");
                    PropertyChanged.Raise(this, "SelectedChordSet");
                }

                if (ChordMappingFile.Default.StepMappings != null)
                {
                    StepMappings = new List<StepMapping>(ChordMappingFile.Default.StepMappings);
                    SelectedStepMapping = ChordMappingFile.Default.DefaultStepMapping;
                    PropertyChanged.Raise(this, "StepMappings");
                    PropertyChanged.Raise(this, "SelectedStepMapping");
                }

                RebuildChordsView();
            };

            this.Closing += (sender, e) =>
            {
                Stop();
                dispatchTimer.Stop();
                dispatchTimer = null;
            };

            btStop.Click += (sender, e) =>
            {
                Stop();
            };
        }

        private void RebuildChordsView()
        {
            chordsGrid.RowDefinitions.Clear();
            chordsGrid.Children.Clear();

            if (ChordMappings != null)
            {
                for (int i = 0; i < ChordMappings.Length; i++)
                    chordsGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(30) });

                currentLine = 0;
                dispatchTimer.Stop();
                dispatchTimer.Interval = TimeSpan.FromMilliseconds(0);
                dispatchTimer.Tick += DispatchTimer_Tick;
                dispatchTimer.Start();
            }
        }

        private void Stop()
        {
            IMachine mac = Editor.SelectedMachine.Machine;
            if (mac != null)
            {
                foreach (int iMidiNote in playingNotesDict.Keys)
                {
                    mac.SendMIDINote(MidiChannel, iMidiNote, 0);
                }
                playingNotesDict.Clear();
            }
        }

        private void DispatchTimer_Tick(object sender, EventArgs e)
        {
            UpdateChordsGrid();
        }

        private void UpdateChordsGrid()
        {

            //scrollViewer.Content = null;

            int counter = 0;
            for (int i = currentLine; i < ChordMappings.Length; i++)
            {
                TextBlock tbIndex = new TextBlock() { Text = i.ToString("X"), FontSize = 16 };
                //tbIndex.MouseEnter += TbChord_MouseEnter;
                //tbIndex.MouseLeave += TbChord_MouseLeave;
                Grid.SetColumn(tbIndex, 0);
                Grid.SetRow(tbIndex, i);
                chordsGrid.Children.Add(tbIndex);

                string[] chord = ChordMappings[i].GetNoteNames();
                TextBlock tbChord = new TextBlock() { Text = ChordMappings[i].Name, FontWeight = FontWeights.Bold, FontSize = 16 };
                tbChord.Tag = new Tuple<int, int>(i, 0);
                tbChord.MouseLeftButtonDown += TbChord_MouseLeftButtonDown;
                tbChord.MouseRightButtonDown += TbChord_MouseRightButtonDown;
                tbChord.MouseLeftButtonUp += TbChord_MouseLeftButtonUp;
                //tbChord.MouseRightButtonUp += TbChord_MouseRightButtonUp;
                tbChord.MouseEnter += TbChord_MouseEnter;
                tbChord.MouseLeave += TbChord_MouseLeave;
                Grid.SetColumn(tbChord, 1);
                Grid.SetRow(tbChord, i);
                chordsGrid.Children.Add(tbChord);


                for (int j = 0; j < chord.Length; j++)
                {
                    string txt = chord[j];

                    if (txt == "-")
                    {
                        txt = "Off";
                    }
                    else
                    {
                        int iNote = BuzzNote.Parse(chord[j]);
                        int iMidiNote = BuzzNote.ToMIDINote(iNote);
                        iMidiNote += (int)Transpose;
                        iNote = BuzzNote.FromMIDINote(iMidiNote);
                        txt = BuzzNote.TryToString(iNote);
                    }
                    TextBlock tbNote = new TextBlock { Text = txt, FontSize = 16 };
                    tbNote.Tag = new Tuple<int, int>(i, j + 1);
                    tbNote.MouseLeftButtonDown += TbChord_MouseLeftButtonDown;
                    tbNote.MouseRightButtonDown += TbChord_MouseRightButtonDown;
                    tbNote.MouseLeftButtonUp += TbChord_MouseLeftButtonUp;
                    //tbNote.MouseRightButtonUp += TbChord_MouseRightButtonUp;
                    tbNote.MouseEnter += TbChord_MouseEnter;
                    tbNote.MouseLeave += TbChord_MouseLeave;
                    Grid.SetColumn(tbNote, j + 2);
                    Grid.SetRow(tbNote, i);
                    chordsGrid.Children.Add(tbNote);
                }
                /*
                Button btPlay = new Button()
                {
                    Content = "▶",
                    Tag = i,
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Width = 60,
                    Height = 28,
                    Margin = new Thickness(1)
                };
                btPlay.Click += BtPlay_Click;
                Grid.SetColumn(btPlay, 8);
                Grid.SetRow(btPlay, i);
                chordsGrid.Children.Add(btPlay);

                Button btCopy = new Button() { Content = "Copy", Tag = i, FontSize = 14 };
                btCopy.Click += BtCopy_Click;
                Grid.SetColumn(btCopy, 8);
                Grid.SetRow(btCopy, i);
                // chordsGrid.Children.Add(btCopy);
                */
                counter++;
                currentLine++;
                if (currentLine >= ChordMappings.Length)
                {
                    dispatchTimer.Stop();
                }

                if (counter > 10)
                    break;
            }
        }

        private void TbChord_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Stop();
        }

        private void TbChord_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // SavingChord = true;

            var tb = (TextBlock)sender;
            int chordRow = ((Tuple<int, int>)tb.Tag).Item1;
            int chordCol = ((Tuple<int, int>)tb.Tag).Item2;
            if (chordCol == 0)
            {
                PlayChord(chordRow);
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    int[] buzzNotes = ChordMappings[chordRow].GetNotes(transpose);
                    int[] steppings = SelectedStepMapping.GetSteps();
                    Editor.patternControl.InsertChord(buzzNotes, steppings);
                }
            }
            else
                PlayNote(chordRow, chordCol);


        }

        private void TbChord_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            TextBlock tb = (TextBlock)sender;
            int chordCol = ((Tuple<int, int>)tb.Tag).Item2;
            //SavingChord = false;

            var fadeInAnimation = new DoubleAnimation(0d, new TimeSpan(0, 0, 0, 0, 500));

            if (Keyboard.Modifiers == ModifierKeys.Control && chordCol == 0)
            {
                SolidColorBrush bgBrush = (TryFindResource("MouseOverChordBrush") as SolidColorBrush);
                if (bgBrush == null)
                    bgBrush = new SolidColorBrush(Colors.YellowGreen);
                else
                    bgBrush = bgBrush.Clone();

                tb.Background = bgBrush;
            }
            else
            {
                SolidColorBrush bgBrush = (TryFindResource("MouseOverNoteBrush") as SolidColorBrush);
                if (bgBrush == null)
                    bgBrush = new SolidColorBrush(Colors.DarkSlateGray);
                else
                    bgBrush = bgBrush.Clone();
                tb.Background = bgBrush;
            }

            tb.Background.Opacity = 1;
            tb.Background.BeginAnimation(SolidColorBrush.OpacityProperty, fadeInAnimation);

            Mouse.OverrideCursor = null;
        }

        private void TbChord_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            TextBlock tb = (TextBlock)sender;
            int chordCol = ((Tuple<int, int>)tb.Tag).Item2;

            var fadeInAnimation = new DoubleAnimation(1d, new TimeSpan(0, 0, 0, 0, 500));

            if (Keyboard.Modifiers == ModifierKeys.Control && chordCol == 0)
            {
                SolidColorBrush bgBrush = (TryFindResource("MouseOverChordBrush") as SolidColorBrush);
                if (bgBrush == null)
                    bgBrush = new SolidColorBrush(Colors.YellowGreen);
                else
                    bgBrush = bgBrush.Clone();

                tb.Background = bgBrush;
            }
            else
            {
                SolidColorBrush bgBrush = (TryFindResource("MouseOverNoteBrush") as SolidColorBrush);
                if (bgBrush == null)
                    bgBrush = new SolidColorBrush(Colors.DarkSlateGray);
                else
                    bgBrush = bgBrush.Clone();
                tb.Background = bgBrush;
            }

            tb.Background.Opacity = 0;
            tb.Background.BeginAnimation(SolidColorBrush.OpacityProperty, fadeInAnimation);

            Mouse.OverrideCursor = Cursors.Hand;

            if (Mouse.LeftButton == MouseButtonState.Pressed)
            {
                int chordRow = ((Tuple<int, int>)tb.Tag).Item1;
                //int chordCol = ((Tuple<int, int>)tb.Tag).Item2;
                if (chordCol == 0)
                    PlayChord(chordRow);
                else
                    PlayNote(chordRow, chordCol);
            }
        }

        private void TbChord_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // SavingChord = true;

            //foreach (IParameter par in gcbm.host.Machine.ParameterGroups[1].Parameters)
            //    if (par.Name == "Transpose")
            //    {
            //        par.SetValue(0, Transpose + 12);
            //        if (par.Group.Machine.DLL.Info.Version >= 42)
            //            par.Group.Machine.SendControlChanges();
            //        break;
            //    }

            //SendNote("String E", previousNotes[0], 0);
            //SendNote("String A", previousNotes[1], 0);
            //SendNote("String D", previousNotes[2], 0);
            //SendNote("String G", previousNotes[3], 0);
            //SendNote("String B", previousNotes[4], 0);
            //SendNote("String E2", previousNotes[5], 0);
        }

        Dictionary<int, int> playingNotesDict = new Dictionary<int, int>();

        private void PlayNote(int chordRow, int noteNumber)
        {
            int buzzNote = ChordMappings[chordRow].GetNotes(transpose)[noteNumber - 1];
            int iMidiNote = BuzzNote.ToMIDINote(buzzNote);
            IMachine mac = Editor.SelectedMachine.Machine;
            mac.SendMIDINote(MidiChannel, iMidiNote, MidiVolume);
            playingNotesDict[iMidiNote] = iMidiNote;
            //if (mac.DLL.Info.Version >= 42)
            //    mac.SendControlChanges();
        }

        private void PlayChord(int chordRow)
        {
            int[] buzzNotes = ChordMappings[chordRow].GetNotes(transpose);

            Stop();

            IMachine mac = Editor.SelectedMachine.Machine;
            for (int i = 0; i < buzzNotes.Length; i++)
            {
                int buzzNote = buzzNotes[i];
                int iMidiNote = BuzzNote.ToMIDINote(buzzNote);
                playingNotesDict[iMidiNote] = iMidiNote;
                mac.SendMIDINote(MidiChannel, iMidiNote, MidiVolume);
            }
            //if (mac.DLL.Info.Version >= 42)
            //    mac.SendControlChanges();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        internal ResourceDictionary GetBuzzThemeResources()
        {
            ResourceDictionary skin = new ResourceDictionary();

            try
            {
                string selectedTheme = Global.Buzz.SelectedTheme == "<default>" ? "Default" : Global.Buzz.SelectedTheme;
                string skinPath = Global.BuzzPath + "\\Themes\\" + selectedTheme + "\\ModernPatternEditor\\ChordsWindow.xaml";
                //string skinPath = "..\\..\\..\\Themes\\" + selectedTheme + "\\ModernPatternEditor\\ModernPatternEditor.xaml";

                //skin.Source = new Uri(skinPath, UriKind.Absolute);
                skin = (ResourceDictionary)XamlReaderEx.LoadHack(skinPath);
            }
            catch (Exception)
            {
                string skinPath = Global.BuzzPath + "\\Themes\\Default\\ModernPatternEditor\\ChordsWindow.xaml";
                skin.Source = new Uri(skinPath, UriKind.Absolute);
            }

            return skin;
        }
    }
}
