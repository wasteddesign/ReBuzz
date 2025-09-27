using Buzz.MachineInterface;
using System;
using System.CodeDom;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using System.Xml.Linq;


namespace BuzzGUI.Interfaces
{
    public enum DCLogLevel { Verbose, Debug, Information, Warning, Error, Fatal }

    public enum BuzzView { PatternView, MachineView, SequenceView, WaveTableView, SongInfoView };
    public enum BuzzCommand { NewFile, OpenFile, SaveFile, SaveFileAs, Cut, Copy, Paste, Undo, Redo, Stop, Exit, About, Preferences, DebugConsole };

    public interface IBuzz : INotifyPropertyChanged
    {
        ISong Song { get; }
        BuzzView ActiveView { get; set; }

        double MasterVolume { get; set; }
        Tuple<double, double> VUMeterLevel { get; }

        int BPM { get; set; }
        int TPB { get; set; }
        int Speed { get; set; }

        bool Playing { get; set; }
        bool Recording { get; set; }
        bool Looping { get; set; }
        bool AudioDeviceDisabled { get; set; }

        ReadOnlyCollection<string> MIDIControllers { get; }
        IIndex<string, Color> ThemeColors { get; }
        IMenuItem MachineIndex { get; }

        IMachine MIDIFocusMachine { get; set; }
        bool MIDIFocusLocked { get; set; }
        bool MIDIActivity { get; }

        bool IsPianoKeyboardVisible { get; set; }
        bool IsSettingsWindowVisible { get; set; }
        bool IsCPUMonitorWindowVisible { get; set; }
        bool IsHardDiskRecorderWindowVisible { get; set; }
        bool IsFullScreen { get; set; }

        int BuildNumber { get; }

        ReadOnlyDictionary<string, IMachineDLL> MachineDLLs { get; }
        ReadOnlyCollection<IInstrument> Instruments { get; }

        ReadOnlyCollection<string> AudioDrivers { get; }
        string SelectedAudioDriver { get; set; }
        int SelectedAudioDriverSampleRate { get; }

        bool OverrideAudioDriver { get; set; }

        IEditContext EditContext { get; set; }

        BuzzPerformanceData PerformanceData { get; }

        IntPtr MachineViewHWND { get; }
        int HostVersion { get; }                                            // MI_VERSION

        ReadOnlyCollection<string> Themes { get; }
        string SelectedTheme { get; set; }


        void ExecuteCommand(BuzzCommand cmd);
        bool CanExecuteCommand(BuzzCommand cmd);

        void DCWriteLine(string s);
        void DCWriteLine(string s, DCLogLevel level);

        void ActivatePatternEditor();
        void ActivateSequenceEditor();
        void SetPatternEditorMachine(IMachine m);
        void SetPatternEditorPattern(IPattern p);
        void NewSequenceEditorActivated();

        void SendMIDIInput(int data);

        void ConfigureAudioDriver();

        int RenderAudio(float[] buffer, int nsamples, int samplerate);      // can be called when OverrideAudioDriver == true

        IMachine GetIMachineFromCMachinePtr(IntPtr pmac);

        void TakePerformanceDataSnapshot();                                 // updates IBuzz.PerformanceData and IMachine.PerformanceData

        void OpenSongFile(string filename);

        void AddMachineDLL(string path, MachineType type);

        event Action<IOpenSong> OpenSong;
        event Action<ISaveSong> SaveSong;
        event Action<int> MIDIInput;
        event Action PatternEditorActivated;

        event Action<float[], bool, SongTime> MasterTap;            // fired in the GUI thread

        // Extensions
        void SetModifiedFlag();
        // void ControlChange(IMachine machine, int group, int track, int param, int value);
        event Action<float[], int> AudioReceived;
        void AudioOut(int channel, Sample[] samples, int n);

        // Device number, device name
        IEnumerable<Tuple<int, string>> GetMidiOuts();

        // device number, MIDI data
        void SendMIDIOutput(int device, int data);

        //NativeMachineFramework does not have access to System.Windows.Media, 
        //so this method exists to lookup a theme string and return a System.Drawing.Color
        System.Drawing.Color GetThemeColour(string name);

        //Get the named profile. Returns empty template if named profile does not exist
        XElement GetModuleProfile(string modname);
        XElement GetModuleProfileInts(string modname);
        XElement GetModuleProfileBinary(string modname);
        XElement GetModuleProfileStrings(string modname);
    }
}
