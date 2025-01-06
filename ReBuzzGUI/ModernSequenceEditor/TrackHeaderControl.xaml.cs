using BuzzGUI.Common;
using BuzzGUI.Common.DSP;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace WDE.ModernSequenceEditor
{
    /// <summary>
    /// Interaction logic for TrackHeaderControl.xaml
    /// </summary>
    public partial class TrackHeaderControl : UserControl, INotifyPropertyChanged
    {
        ViewSettings viewSettings;

        DispatcherTimer timer;
        float maxSampleL;
        float maxSampleR;

        public string VUMeterToolTip { get; set; }
        public double VUMeterLevelL { get; set; }
        public double VUMeterLevelR { get; set; }

        const double VUMeterRange = 80.0;

        private Visibility vUMeterVisibility;
        public Visibility VUMeterVisibility
        {
            set
            {
                vUMeterVisibility = value;
                PropertyChanged.Raise(this, "VUMeterVisibility");
                UpdateConnectionTapEvent();
            }
            get { return vUMeterVisibility; }
        }

        private IMachineConnection selectedConnection;
        public IMachineConnection SelectedConnection
        {
            get { return selectedConnection; }
            set
            {
                if (selectedConnection != null)
                    selectedConnection.Tap -= SelectedConnection_Tap;

                selectedConnection = value;
                viewSettings.VUMeterMachineConnection[sequence] = selectedConnection;
                viewSettings.VUMeterMachineConnectionEvent();

                UpdateConnectionTapEvent();
                UpdateToolTip();
            }
        }

        public ViewSettings ViewSettings
        {
            set
            {
                viewSettings = value;
            }
        }

        ISequence sequence;
        public ISequence Sequence
        {
            get { return sequence; }
            set
            {
                if (sequence != null)
                {
                    sequence.PropertyChanged -= sequence_PropertyChanged;
                    sequence.Machine.PropertyChanged -= Machine_PropertyChanged;
                    sequence.Machine.Graph.ConnectionAdded -= Graph_ConnectionAdded;
                    sequence.Machine.Graph.ConnectionRemoved -= Graph_ConnectionRemoved;

                    VUMeterLevelL = 0;
                    maxSampleL = -1;
                    VUMeterLevelR = 0;
                    maxSampleR = -1;
                    PropertyChanged.Raise(this, "VUMeterLevelL");
                    PropertyChanged.Raise(this, "VUMeterLevelR");
                }


                sequence = value;
                // DataContext = sequence;
                DataContext = this;
                UpdatePatternList();

                if (sequence != null)
                {
                    sequence.PropertyChanged += sequence_PropertyChanged;
                    sequence.Machine.PropertyChanged += Machine_PropertyChanged;
                    sequence.Machine.Graph.ConnectionAdded += Graph_ConnectionAdded;
                    sequence.Machine.Graph.ConnectionRemoved += Graph_ConnectionRemoved;

                    if (viewSettings.VUMeterMachineConnection.ContainsKey(sequence))
                        SelectedConnection = viewSettings.VUMeterMachineConnection[sequence];
                    else
                        SelectedConnection = null;

                    PropertyChanged.Raise(this, "Sequence");
                }

                ValidateConnection();
            }
        }

        private void Graph_ConnectionRemoved(IMachineConnection obj)
        {
            ValidateConnection();
        }

        private void Graph_ConnectionAdded(IMachineConnection obj)
        {
            ValidateConnection();
        }

        void Machine_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Patterns")
                UpdatePatternList();
        }

        void sequence_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
        }

        void SetTimer()
        {
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(20);
            timer.Tick += (sender, e) =>
            {
                if (maxSampleL >= 0)
                {
                    var db = Math.Min(Math.Max(Decibel.FromAmplitude(maxSampleL), -VUMeterRange), 0.0);
                    VUMeterLevelL = (db + VUMeterRange) / VUMeterRange;
                    PropertyChanged.Raise(this, "VUMeterLevelL");
                    maxSampleL = -1;
                }
                if (maxSampleR >= 0)
                {
                    var db = Math.Min(Math.Max(Decibel.FromAmplitude(maxSampleR), -VUMeterRange), 0.0);
                    VUMeterLevelR = (db + VUMeterRange) / VUMeterRange;
                    PropertyChanged.Raise(this, "VUMeterLevelR");
                    maxSampleR = -1;
                }
            };
        }

        void ValidateConnection()
        {
            if (sequence != null && sequence.Machine != null)
            {
                if (SelectedConnection == null)
                {
                    if (sequence.Machine.Outputs.Count > 0)
                    {
                        SelectedConnection = sequence.Machine.Outputs[0];
                    }
                }
                else
                {
                    if (sequence.Machine.Outputs.IndexOf(SelectedConnection) == -1)
                    {
                        SelectedConnection = sequence.Machine.Outputs.Count > 0 ? sequence.Machine.Outputs[0] : null;
                    }
                }
            }
            UpdateToolTip();
        }

        private void UpdateToolTip()
        {
            if (SelectedConnection != null)
            {
                VUMeterToolTip = SelectedConnection.Source.Name + " -> " + SelectedConnection.Destination.Name;
                PropertyChanged.Raise(this, "VUMeterToolTip");
            }
            else
            {
                VUMeterToolTip = null;
                PropertyChanged.Raise(this, "VUMeterToolTip");
            }
        }

        void UpdateConnectionTapEvent()
        {
            if (SelectedConnection != null)
            {
                if (VUMeterVisibility == Visibility.Visible)
                {
                    SelectedConnection.Tap += SelectedConnection_Tap;
                    timer.Start();
                }
                else
                {
                    SelectedConnection.Tap -= SelectedConnection_Tap;
                    timer.Stop();
                }
            }
            else
            {
                timer.Stop();
            }
        }

        private void SelectedConnection_Tap(float[] arg1, bool arg2, SongTime arg3)
        {
            if (!arg2) // Mono
            {
                maxSampleL = Math.Max(maxSampleL, DSP.AbsMax(arg1) * (1.0f / 32768.0f));
                maxSampleR = maxSampleL;
            }
            else
            {
                float[] L = new float[arg1.Length / 2];
                float[] R = new float[arg1.Length / 2];
                for (int i = 0; i < arg1.Length / 2; i++)
                {
                    L[i] = arg1[i * 2];
                    R[i] = arg1[i * 2 + 1];
                }

                maxSampleL = Math.Max(maxSampleL, DSP.AbsMax(L) * (1.0f / 32768.0f));
                maxSampleR = Math.Max(maxSampleR, DSP.AbsMax(R) * (1.0f / 32768.0f));
            }
        }

        bool isSelected;
        public bool IsSelected
        {
            get { return isSelected; }
            set
            {
                isSelected = value;
                PropertyChanged.Raise(this, "IsSelected");
                Background = IsSelected ? backgroundSelectedBrush : backgroundBrush;
            }
        }



        List<PatternListItem> patternList;
        public IList<PatternListItem> PatternList { get { return patternList; } }

        void UpdatePatternList()
        {
            patternList = new List<PatternListItem>();
            if (sequence == null) return;

            char ch = '0';

            foreach (var p in sequence.Machine.Patterns.OrderBy(x => x.Name))
            {
                patternList.Add(new PatternListItem(p, ch));

                ch++;
                if (ch - 1 == '9')
                    ch = 'a';

                if (ch - 1 == 'z')
                    ch = 'A';
            }

            PropertyChanged.Raise(this, "PatternList");
        }

        public IPattern GetPatternByChar(char ch)
        {
            if (sequence == null) return null;
            return patternList.Where(i => i.Char == ch).Select(i => i.Pattern).FirstOrDefault();
        }

        public SequenceEditor Editor { get; private set; }

        Brush backgroundBrush;
        Brush backgroundSelectedBrush;

        public TrackHeaderControl(SequenceEditor se)
        {
            Editor = se;
            if (Editor.ResourceDictionary != null) this.Resources.MergedDictionaries.Add(se.ResourceDictionary);
            InitializeComponent();

            backgroundBrush = TryFindResource("SeqEdTrackHeaderBackgroundBrush") as Brush;
            backgroundSelectedBrush = TryFindResource("SeqEdTrackHeaderSelectedBackgroundBrush") as Brush;

            this.Unloaded += TrackHeaderControl_Unloaded;
            SequenceEditor.Settings.PropertyChanged += Settings_PropertyChanged;
            SetTimer();
            VUMeterVisibility = SequenceEditor.Settings.VUMeterLevels == true ? Visibility.Visible : Visibility.Hidden;


            this.MouseDown += (sender, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    if (e.ClickCount == 1)
                    {
                        Editor.SelectColumn(this);
                        Editor.UpdateMenu();
                    }
                    else if (e.ClickCount == 2)
                    {
                        if (sequence != null)
                        {
                            sequence.Machine.DoubleClick();
                            e.Handled = true;
                        }
                    }
                }
                else if (e.ChangedButton == MouseButton.Right)
                {
                    if (e.ClickCount == 1)
                    {
                        Editor.SelectColumn(this);
                        Editor.UpdateMenu();
                    }
                }
            };
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "VUMeterLevels")
            {
                if (SequenceEditor.Settings.VUMeterLevels)
                    VUMeterVisibility = Visibility.Visible;
                else
                    VUMeterVisibility = Visibility.Hidden;
            }
        }

        private void TrackHeaderControl_Unloaded(object sender, RoutedEventArgs e)
        {
            SequenceEditor.Settings.PropertyChanged -= Settings_PropertyChanged;
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }

    public class PatternListItem
    {
        public IPattern Pattern { get; private set; }
        public char Char { get; private set; }

        public PatternListItem(IPattern pattern, char ch)
        {
            Pattern = pattern;
            Char = ch;
        }

        public override string ToString()
        {
            return string.Format("{0}. {1}", Char, Pattern.Name);
        }
    }
}
