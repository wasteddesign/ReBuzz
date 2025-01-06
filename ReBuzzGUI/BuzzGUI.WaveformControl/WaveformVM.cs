using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using BuzzGUI.WaveformControl.Actions;
using BuzzGUI.WavetableView;
using System;
using System.ComponentModel;
using System.Windows.Input;
using WDE.AudioBlock;

namespace BuzzGUI.WaveformControl
{
    public class WaveformVM : INotifyPropertyChanged
    {
        public IEditContext EditContext { get; set; }

        //Commands
        public ICommand SetLoopCommand { get; private set; }
        public ICommand DeleteEditCommand { get; private set; }
        public ICommand FadeInLinearCommand { get; private set; }
        public ICommand FadeOutLinearCommand { get; private set; }
        public ICommand ReverseEditCommand { get; private set; }
        public ICommand NormalizeEditCommand { get; set; }
        public ICommand MuteCommand { get; set; }
        public ICommand GainEditCommand { get; set; }
        public ICommand PhaseInvertCommand { get; set; }
        public ICommand TrimEditCommand { get; set; }
        public ICommand SaveSelectionCommand { get; set; }
        public ICommand InsertSilenceCommand { get; set; }
        public ICommand SnapToZeroCrossingCommand { get; set; }
        public ICommand SelectLoopCommand { get; set; }
        public ICommand DCRemoveCommand { get; set; }
        public ICommand Resample44100Command { get; set; }
        public ICommand Resample48000Command { get; set; }
        public ICommand Resample88200Command { get; set; }
        public ICommand Resample96000Command { get; set; }
        public ICommand Resample176400Command { get; set; }
        public ICommand Resample192000Command { get; set; }
        public ICommand ChangePitchCommand { get; set; }
        public ICommand ChangeTempoCommand { get; set; }
        public ICommand ChangeRateCommand { get; set; }
        public ICommand AINoiseSuppressionCommand { get; set; }
        public ICommand DetectPBMCommand { get; set; }

        public SimpleCommand SelectionChangedCommand { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public WaveformVM()
        {
            SetLoopCommand = new SimpleCommand()
            {
                CanExecuteDelegate = x => IsSelection(x),
                ExecuteDelegate = x => { Do(new SetLoopAction(this, x)); }
            };

            DeleteEditCommand = new SimpleCommand()
            {
                CanExecuteDelegate = x => IsSelection(x),
                ExecuteDelegate = x => { Do(new DeleteAction(this, x)); }
            };

            TrimEditCommand = new SimpleCommand()
            {
                CanExecuteDelegate = x => IsSelection(x),
                ExecuteDelegate = x => { Do(new TrimEditAction(this, x)); }
            };
            ReverseEditCommand = new SimpleCommand()
            {
                CanExecuteDelegate = x => IsSelection(x),
                ExecuteDelegate = x => { Do(new ReverseEditAction(this, x)); }
            };

            NormalizeEditCommand = new SimpleCommand()
            {
                CanExecuteDelegate = x => IsSelection(x),
                ExecuteDelegate = x => { Do(new NormalizeAction(this, x)); }
            };
            MuteCommand = new SimpleCommand()
            {
                CanExecuteDelegate = x => IsSelection(x),
                ExecuteDelegate = x => { Do(new MuteAction(this, x)); }
            };
            GainEditCommand = new SimpleCommand()
            {
                CanExecuteDelegate = x => IsSelection(x),
                ExecuteDelegate = x => { Do(new GainEditAction(this, x)); }
            };
            PhaseInvertCommand = new SimpleCommand()
            {
                CanExecuteDelegate = x => IsSelection(x),
                ExecuteDelegate = x => { Do(new PhaseInvertAction(this, x)); }
            };

            FadeInLinearCommand = new SimpleCommand()
            {
                CanExecuteDelegate = x => IsSelection(x),
                ExecuteDelegate = x => { Do(new FadeEditAction(this, FadeEditAction.FadeType.LinIn, x)); }
            };

            FadeOutLinearCommand = new SimpleCommand()
            {
                CanExecuteDelegate = x => IsSelection(x),
                ExecuteDelegate = x => { Do(new FadeEditAction(this, FadeEditAction.FadeType.LinOut, x)); }
            };

            SaveSelectionCommand = new SimpleCommand()
            {
                CanExecuteDelegate = x => IsSelection(x),
                ExecuteDelegate = x => { Do(new SaveSelectionAction(this, x)); }
            };

            InsertSilenceCommand = new SimpleCommand()
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x => { Do(new InsertSilenceAction(this, x)); }
            };

            SnapToZeroCrossingCommand = new SimpleCommand()
            {
                CanExecuteDelegate = x => IsSelection(x),
                ExecuteDelegate = x => { Do(new SnapToZeroCrossingAction(this, x)); }
            };

            SelectLoopCommand = new SimpleCommand()
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x => { Do(new SelectLoopAction(this, x)); }
            };

            DCRemoveCommand = new SimpleCommand()
            {
                CanExecuteDelegate = x => IsSelection(x),
                ExecuteDelegate = x => { Do(new DCRemoveAction(this, x)); }
            };

            Resample44100Command = new SimpleCommand()
            {
                CanExecuteDelegate = x => !IsWaveSampleRate(44100),
                ExecuteDelegate = x => { Do(new ResampleAction(this, x, 44100)); }
            };
            Resample48000Command = new SimpleCommand()
            {
                CanExecuteDelegate = x => !IsWaveSampleRate(48000),
                ExecuteDelegate = x => { Do(new ResampleAction(this, x, 48000)); }
            };
            Resample88200Command = new SimpleCommand()
            {
                CanExecuteDelegate = x => !IsWaveSampleRate(88200),
                ExecuteDelegate = x => { Do(new ResampleAction(this, x, 88200)); }
            };
            Resample96000Command = new SimpleCommand()
            {
                CanExecuteDelegate = x => !IsWaveSampleRate(96000),
                ExecuteDelegate = x => { Do(new ResampleAction(this, x, 96000)); }
            };
            Resample176400Command = new SimpleCommand()
            {
                CanExecuteDelegate = x => !IsWaveSampleRate(176400),
                ExecuteDelegate = x => { Do(new ResampleAction(this, x, 176400)); }
            };
            Resample192000Command = new SimpleCommand()
            {
                CanExecuteDelegate = x => !IsWaveSampleRate(192000),
                ExecuteDelegate = x => { Do(new ResampleAction(this, x, 192000)); }
            };
            ChangePitchCommand = new SimpleCommand()
            {
                CanExecuteDelegate = x => IsSelection(x),
                ExecuteDelegate = x =>
                {
                    var cpa = new ChangePitchAction(this, x);
                    if (cpa.Ready) { Do(cpa); }
                }
            };
            ChangeTempoCommand = new SimpleCommand()
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    var cpa = new ChangeTempoAction(this, x);
                    if (cpa.Ready) { Do(cpa); }
                }
            };
            ChangeRateCommand = new SimpleCommand()
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    var cpa = new ChangeRateAction(this, x);
                    if (cpa.Ready) { Do(cpa); }
                }
            };
            AINoiseSuppressionCommand = new SimpleCommand()
            {
                CanExecuteDelegate = x => IsSelection(x),
                ExecuteDelegate = x =>
                {
                    var cpa = new AINoiseSuppressionAction(this, x);
                    if (cpa.Ready) { Do(cpa); }
                }
            };
            DetectPBMCommand = new SimpleCommand()
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    float bpm = Effects.DetectBpm(Wavetable, SelectedSlotIndex, SelectedLayerIndex);
                    Mouse.OverrideCursor = null;
                    BpmDialog inputDialogNumber = new BpmDialog(Effects.GetBuzzThemeResources(), bpm);
                    inputDialogNumber.ShowDialog();
                }
            };

            SelectionChangedCommand = new SimpleCommand()
            {
                CanExecuteDelegate = (x) => true,
                ExecuteDelegate = (x) =>
                {
                }
            };

        }

        private bool IsSelection(object x)
        {
            var selection = (x as Tuple<IWaveformBase, WaveformSelection>).Item2;
            if (selection == null) return false;

            return selection.IsActive();
        }

        private bool IsWaveSampleRate(int sampleRate)
        {
            if (Waveform != null && Waveform.SampleRate == sampleRate)
                return true;

            return false;
        }

        void Do(IAction a)
        {
            try
            {
                if (EditContext != null)
                    EditContext.ActionStack.Do(a);
            }
            catch { }
        }

        private void OnPropertyChanged(string field)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(field));
        }

        IWaveLayer waveform;

        IWave selectedWave;

        public IWave SelectedWave
        {
            get
            {
                return selectedWave;
            }
            set
            {
                selectedWave = value;
                if (selectedWave != null)
                {
                    SelectedSlotIndex = selectedWave.Index;
                }
                else
                {
                    SelectedSlotIndex = -1;
                }
            }
        }

        public IWaveLayer Waveform
        {
            get { return waveform; }
            set
            {
                waveform = value;
                if (waveform != null)
                    SelectedLayerIndex = WaveCommandHelpers.GetLayerIndex(waveform);
                OnPropertyChanged("Waveform");
            }
        }

        public void RaiseEditedWaveChanged()
        {
            EditedWaveChanged.Invoke(this, EventArgs.Empty);
            
        }
        public event EventHandler EditedWaveChanged;

        private IWavetable wavetable;
        public IWavetable Wavetable
        {
            get { return wavetable; }
            set
            {
                wavetable = value;
            }
        }

        public int SelectedSlotIndex { get; set; }
        public int SelectedLayerIndex { get; set; }
    }
}
