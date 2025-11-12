// ReBuzzCore.cs
// Core host implementation for ReBuzz.
//
// This file contains the main application core `ReBuzzCore` which implements
// the `IBuzz` host interface. It coordinates the song, machines, audio engine,
// MIDI engine and communication with UI and file formats. The comments added
// here are informational only and do not change any runtime behavior.

using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Common.Settings;
using BuzzGUI.Interfaces;
using Microsoft.Win32;
using NAudio.Midi;
using ReBuzz.Audio;
using ReBuzz.Common;
using ReBuzz.Core.Actions.GraphActions;
using ReBuzz.FileOps;
using ReBuzz.MachineManagement;
using ReBuzz.Midi;
using ReBuzz.Properties;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using Timer = System.Timers.Timer;

namespace ReBuzz.Core
{
    [StructLayout(LayoutKind.Sequential)]
    public struct BuzzGlobalState
    {
        // Holds global playback-related values that are also shared with native
        // code. The struct layout is sequential because it is used for
        // interop with the native engine.
        public int AudioFrame;
        public int ADWritePos;
        public int ADPlayPos;
        public int SongPosition;
        public int LoopStart;
        public int LoopEnd;
        public int SongEnd;
        public int StateFlags;
        public byte MIDIFiltering;
        public byte SongClosing;
    }

    /// <summary>
    /// Main application core that implements the IBuzz host interface.
    ///
    /// Responsibilities:
    /// - Maintain song and machine lists
    /// - Coordinate audio and MIDI engines
    /// - Handle file load/save and import/export operations
    /// - Provide services to machines (create, remove, connect, edit)
    /// - Expose UI and state events to the WPF front-end
    ///
    /// The class is large because it brings together many subsystems; comments
    /// are added to explain high-level responsibilities and key public methods.
    /// </summary>
    public class ReBuzzCore : IBuzz, INotifyPropertyChanged
    {
        // Build and runtime info
        public static int buildNumber = int.Parse(Resources.BuildNumber);
        public static MasterInfoExtended masterInfo;
        public static SubTickInfoExtended subTickInfo;
        public static readonly int SubTicsPerTick = 8;
        internal static BuzzGlobalState GlobalState;
        public static string AppDataPath = "ReBuzz";

        // Feature flags
        public readonly bool AUTO_CONVERT_WAVES = false;

        // Host/MI version
        public int HostVersion { get => 66; } // MI_VERSION

        // State flags used in GlobalState.StateFlags
        public static readonly int SF_PLAYING = 1;
        public static readonly int SF_RECORDING = 2;

        // Song core and interface
        SongCore songCore;
        public ISong Song { get => songCore; }

        public SongCore SongCore
        {
            get => songCore;
            set
            {
                songCore = value;
                MidiControllerAssignments.Song = songCore;
            }
        }

        // UI related state
        BuzzView activeView = BuzzView.MachineView;
        public ReBuzzTheme Theme { get; set; }

        // Save profiles callback registration helper
        private void RegisterProfileSaveCallback(XElement element)
        {
            element.Changed += (sender, evtargs) =>
            {
                Utils.SaveProfile(buzzPath, element);
            };
        }

        // Module profile helpers: these return XElement nodes used to store
        // per-machine persistent settings (ints, binary blobs, strings).
        public XElement GetModuleProfile(string modname)
        {
            if (!profiles.ContainsKey(modname))
            {
                XElement modElement = new XElement(modname);
                modElement.Add(new XElement("Int"));
                modElement.Add(new XElement("Binary"));
                modElement.Add(new XElement("String"));
                
                profiles.Add(modname, modElement);
                RegisterProfileSaveCallback(modElement);
                return modElement;
            }

            return profiles[modname];
        }

        public XElement GetModuleProfileInts(string modname)
        {
            XElement modElement = GetModuleProfile(modname);
            return modElement.Element("Int");
        }

        public XElement GetModuleProfileBinary(string modname)
        {
            XElement modElement = GetModuleProfile(modname);
            return modElement.Element("Binary");
        }

        public XElement GetModuleProfileStrings(string modname)
        {
            XElement modElement = GetModuleProfile(modname);
            return modElement.Element("String");
        }

        public System.Drawing.Color GetThemeColour(string name)
        {
            var wpfColour = ThemeColors[name];

            // Convert to System.Drawing.Color
            return System.Drawing.Color.FromArgb(wpfColour.A, wpfColour.R, wpfColour.G, wpfColour.B);
        }

        public BuzzView ActiveView { get { return activeView; } set { activeView = value; PropertyChanged.Raise(this, "ActiveView"); } }

        // VU meter level tuple (left,right)
        public Tuple<double, double> VUMeterLevel { get; set; }

        // MIDI and controller assignment management
        internal MidiControllerAssignments MidiControllerAssignments { get; set; }

        internal DispatcherTimer dtEngineThread;

        int speed;
        bool playing;
        bool recording;
        bool looping;
        bool audioDeviceDisabled;

        double masterVolume = 1;
        public double MasterVolume
        {
            get => masterVolume;
            set
            {
                if (value >= 0 && value <= 1 && masterVolume != value)
                {
                    masterVolume = value;
                    if (songCore != null)
                    {
                        var master = songCore.MachinesList.FirstOrDefault(m => m.DLL.Info.Type == MachineType.Master);
                        int volumeVal = (int)((1 - masterVolume) * 0x4000);
                        master.ParameterGroups[1].Parameters[0].SetValue(0, volumeVal);
                    }

                    dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            if (PropertyChanged != null)
                                PropertyChanged?.Raise(this, "MasterVolume");
                            SetModifiedFlag();
                        }
                        catch (Exception ex)
                        {
                            DCWriteLine(ex.Message);
                        }
                    });
                }
            }
        }

        int bpm = 126;
        public int BPM
        {
            get => bpm; set
            {
                if (value >= 16 && value <= 500 && bpm != value)
                {
                    bpm = value;
                    if (songCore != null)
                    {
                        var master = songCore.MachinesList.FirstOrDefault(m => m.DLL.Info.Type == MachineType.Master);
                        master.ParameterGroups[1].Parameters[1].SetValue(0, value);
                    }
                    // Actual BPM is changed in WorkManager
                    dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            if (PropertyChanged != null)
                                PropertyChanged?.Raise(this, "BPM");
                            SetModifiedFlag();
                        }
                        catch (Exception ex)
                        {
                            DCWriteLine(ex.Message);
                        }
                    });
                }
            }
        }

        int tpb = 4;
        public int TPB
        {
            get => tpb; set
            {
                if (value >= 1 && value <= 32 && tpb != value)
                {
                    tpb = value;
                    if (songCore != null)
                    {
                        var master = songCore.MachinesList.FirstOrDefault(m => m.DLL.Info.Type == MachineType.Master);
                        master.ParameterGroups[1].Parameters[2].SetValue(0, value);
                    }
                    // Actual TPB is changed in WorkManager
                    dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            if (PropertyChanged != null)
                                PropertyChanged?.Raise(this, "TPB");
                            SetModifiedFlag();
                        }
                        catch (Exception ex)
                        {
                            DCWriteLine(ex.Message);
                        }
                    });
                }
            }
        }
        public int Speed { get => speed; set { speed = value; PropertyChanged.Raise(this, "Speed"); } }

        internal bool StartPlaying()
        {
            if (preparePlaying)
            {
                preparePlaying = false;
                playing = true;
                GlobalState.StateFlags |= SF_PLAYING;
                dispatcher.BeginInvoke(() =>
                {
                    PropertyChanged.Raise(this, "Playing");
                });
                return true;
            }
            return false;
        }

        bool preparePlaying;

        /// <summary>
        /// Gets or sets whether the host is currently playing.
        /// Setting to true prepares playback synchronized on the next tick.
        /// Setting to false stops playback, stops all machines and clears solo state.
        /// </summary>
        public bool Playing
        {
            get => playing;
            set
            {
                if (!value)
                {
                    preparePlaying = false;
                    playing = false;
                    GlobalState.StateFlags &= ~SF_PLAYING;
                    songCore.WavetableCore.StopPlayingWave();

                    foreach (var machine in songCore.MachinesList)
                    {
                        MachineManager.Stop(machine);
                    }

                    if (Recording)
                    {
                        Recording = false;
                    }

                    SoloPattern = null;
                    PropertyChanged.Raise(this, "Playing");
                }

                if (value)
                {
                    // Sync with tick == 0
                    preparePlaying = true;
                }
            }
        }
        public bool Recording
        {
            get => recording;
            set
            {
                recording = value;

                if (recording)
                    GlobalState.StateFlags |= SF_RECORDING;
                else
                    GlobalState.StateFlags &= ~SF_RECORDING;

                if (recording && !playing)
                {
                    Playing = true;
                }
                PropertyChanged.Raise(this, "Recording");
            }
        }
        public bool Looping { get => looping; set { looping = value; PropertyChanged.Raise(this, "Looping"); } }
        public bool AudioDeviceDisabled
        {
            get => audioDeviceDisabled;
            set
            {
                audioDeviceDisabled = value;
                PropertyChanged.Raise(this, "AudioDeviceDisabled");
            }
        }

        private IList<string> midiControllers = new string[] { };

        public ReadOnlyCollection<string> MIDIControllers
        {
            get => new ReadOnlyCollection<string>(midiControllers);
            set
            {
                midiControllers = value;
                PropertyChanged.Raise(this, "MIDIControllers");
            }
        }

        IIndex<string, Color> themeColors;
        public IIndex<string, Color> ThemeColors { get => themeColors; set => themeColors = value; }

        public IMenuItem MachineIndex { get; set; }



        // MIDI
        IMachine midiFocusMachine;
        public IMachine MIDIFocusMachine
        {
            get => midiFocusMachine;
            set
            {
                //lock (AudioLock)
                {
                    if (midiFocusMachine != value && !MIDIFocusLocked)
                    {
                        if (midiFocusMachine != null)
                        {
                            MachineManager.LostMidiFocus(midiFocusMachine as MachineCore);
                        }

                        midiFocusMachine = value;

                        if (midiFocusMachine != null)
                        {
                            MachineManager.GotMidiFocus(midiFocusMachine as MachineCore);
                        }

                        dispatcher.Invoke(() => PropertyChanged.Raise(this, "MIDIFocusMachine"));
                    }
                }
            }
        }

        private bool midiFocusLocked;
        public bool MIDIFocusLocked { get => midiFocusLocked; set { midiFocusLocked = value; PropertyChanged.Raise(this, "MIDIFocusLocked"); } }

        private bool midiActivity;

        // Timer to set MIDIActivity to false afeter 0.5 seconds
        DispatcherTimer midiActivityTimer;
        
        public bool MIDIActivity
        {
            get => midiActivity;
            set
            {
                midiActivity = value;

                if (midiActivity)
                {
                    midiActivityTimer.Start();
                }
                PropertyChanged.Raise(this, "MIDIActivity");
            }
        }

        // Misc
        //KeyboardWindow keyboardWindow = null;
        bool isPianoKeyboardVisible;
        public bool IsPianoKeyboardVisible
        {
            get => isPianoKeyboardVisible;
            set
            {
                isPianoKeyboardVisible = value;
                PropertyChanged.Raise(this, "IsPianoKeyboardVisible");
            }
        }

        bool isSettingsWindowVisible;
        public bool IsSettingsWindowVisible
        {
            get => isSettingsWindowVisible;
            set
            {
                isSettingsWindowVisible = value;
                if (isSettingsWindowVisible)
                {
                    ShowSettings.Invoke("");
                }
            }
        }

        bool isCPUMonitorWindowVisible;
        public bool IsCPUMonitorWindowVisible { get => isCPUMonitorWindowVisible; set { isCPUMonitorWindowVisible = value; PropertyChanged.Raise(this, "IsCPUMonitorWindowVisible"); } }


        bool isHardDiskRecorderWindowVisible;

        public bool IsHardDiskRecorderWindowVisible
        {
            get => isHardDiskRecorderWindowVisible;
            set
            {
                isHardDiskRecorderWindowVisible = value;
                PropertyChanged.Raise(this, "IsHardDiskRecorderWindowVisible");
            }
        }

        public event Action<bool> FullScreenChanged;
        bool fullScreen;
        public bool IsFullScreen
        {
            get => fullScreen;
            set
            {
                fullScreen = value;
                if (FullScreenChanged != null)
                {
                    FullScreenChanged.Invoke(fullScreen);
                }
            }
        }

        public int BuildNumber { get => int.Parse(Resources.BuildNumber); }
        public string BuildString { get => "ReBuzz Build " + BuildNumber + " " + Resources.BuildDate; }

        Dictionary<string, MachineDLL> machineDLLsList = new Dictionary<string, MachineDLL>();
        internal Dictionary<string, MachineDLL> MachineDLLsList { get => machineDLLsList; set => machineDLLsList = value; }
        public BuzzGUI.Interfaces.ReadOnlyDictionary<string, IMachineDLL> MachineDLLs
        {
            get => new BuzzGUI.Interfaces.ReadOnlyDictionary<string, IMachineDLL>(MachineDLLsList
                .ToDictionary(p => p.Key, p => (IMachineDLL)p.Value));
        }

        List<Instrument> InstrumentList { get; set; }
        public ReadOnlyCollection<IInstrument> Instruments { get => InstrumentList.Cast<IInstrument>().ToReadOnlyCollection(); }

        private List<string> audioDrivers;

        public ReadOnlyCollection<string> AudioDrivers { get => audioDrivers.ToReadOnlyCollection(); }
        public List<string> AudioDriversList { get => audioDrivers; set => audioDrivers = value; }

        private string selectedAudioDriver;
        public string SelectedAudioDriver
        {
            get => selectedAudioDriver;
            set
            {
                if (SelectedAudioDriver != value)
                {
                    try
                    {
                        AudioEngine.CreateAudioOut(value);
                        if (AudioEngine.SelectedOutDevice != null)
                        {
                            selectedAudioDriver = AudioEngine.SelectedOutDevice.Name;
                            registryEx.Write("AudioDriver", selectedAudioDriver, "Settings");
                            AudioEngine.Play();
                            PropertyChanged.Raise(this, "SelectedAudioDriver");
                        }
                    }
                    catch (Exception e)
                    {
                        Utils.MessageBox("Selected Audio Driver Error: " + e.Message, "Selected Audio Driver Error");
                    }
                }
            }
        }

        public int SelectedAudioDriverSampleRate
        {
            get => masterInfo.SamplesPerSec;
            set
            {
                lock (AudioLock)
                {
                    masterInfo.SamplesPerSec = value;
                    UpdateMasterInfo();
                    MachineManager.ResetMachines();         // Some machines need this?
                }
                PropertyChanged.Raise(this, "SelectedAudioDriverSampleRate");
            }
        }

        readonly int SubTickSize = 260;
        internal void UpdateMasterInfo()
        {
            if (masterInfo.SamplesPerSec > 0)
            {
                masterInfo.BeatsPerMin = bpm;
                masterInfo.TicksPerBeat = tpb;

                masterInfo.AverageSamplesPerTick = 60.0 * masterInfo.SamplesPerSec / (masterInfo.BeatsPerMin * (double)masterInfo.TicksPerBeat);
                masterInfo.SamplesPerTick = (int)masterInfo.AverageSamplesPerTick;
                masterInfo.TicksPerSec = masterInfo.BeatsPerMin * masterInfo.TicksPerBeat / 60.0f;
                masterInfo.PosInTick = 0;

                int subTickCount = masterInfo.SamplesPerTick / SubTickSize;
                subTickInfo.AverageSamplesPerSubTick = masterInfo.SamplesPerTick / (double)subTickCount;
                subTickInfo.SamplesPerSubTick = (int)(subTickInfo.AverageSamplesPerSubTick);
                subTickInfo.SubTicksPerTick = subTickCount;
                subTickInfo.CurrentSubTick = 0;
                subTickInfo.PosInSubTick = 0;

                subTickInfo.SubTickReminderCounter = 0;// masterInfo.SamplesPerTick % subTickInfo.SubTicksPerTick;
            }
        }

        bool overrideAudioDriver;

        public bool OverrideAudioDriver
        {
            get => overrideAudioDriver;
            set
            {
                overrideAudioDriver = value;
            }
        }

        public IEditContext EditContext { get; set; }

        long performanceCountTime;
        internal BuzzPerformanceData PerformanceCurrent { get; set; }
        public BuzzPerformanceData PerformanceData { get; set; }

        public IntPtr MachineViewHWND { get; set; }

        public event Action<string> ThemeChanged;
        readonly List<string> themes;
        readonly Dictionary<string, XElement> profiles;

        public ReadOnlyCollection<string> Themes { get => themes.ToReadOnlyCollection(); }

        public System.Collections.ObjectModel.ReadOnlyDictionary<string, XElement> ModuleProfiles { get => System.Collections.Generic.CollectionExtensions.AsReadOnly<string, XElement>(profiles); }

        public string SelectedTheme
        {
            get => registryEx.Read("Theme", "<default>", "Settings");
            set
            {
                registryEx.Write("Theme", value, "Settings");
                if (ThemeChanged != null)
                {
                    ThemeChanged.Invoke(value);
                }
            }
        }
        public IntPtr MainWindowHandle { get; internal set; }
        public MachineManager MachineManager { get; internal set; }

        IMachineDatabase machineDB;
        internal IMachineDatabase MachineDB
        {
            get => machineDB;
            set
            {
                machineDB = value;
                MachineIndex = machineDB.IndexMenu;
                UpdateInstrumentList(MachineDB);
            }
        }

        bool modified;
        public bool Modified
        {
            get => modified;
            internal set
            {
                if (masterLoading)
                    return;

                modified = value;
                dispatcher.BeginInvoke(() =>
                  PropertyChanged.Raise(this, "Modified"));
            }
        }
        public IPattern PatternEditorPattern { get; private set; }
        internal PatternCore SoloPattern { get; set; }

        public event Action<IOpenSong> OpenSong;
        public event Action<ISaveSong> SaveSong;
        public event Action<int> MIDIInput;
        public event Action PatternEditorActivated;
        public event Action SequenceEditorActivated;
        public event Action<float[], bool, SongTime> MasterTap;
        public event PropertyChangedEventHandler PropertyChanged;

        public event Action<string> ShowSettings;

        public event Action<BuzzCommand> BuzzCommandRaised;

        internal MidiEngine MidiInOutEngine { get; }

        DispatcherTimer dtVUMeter;

        readonly Timer timerAutomaticBackups;

        /// <summary>
        /// Constructor - initializes the ReBuzz engine core and subsystems.
        /// This wiring includes engine settings, MIDI devices, themes, profiles,
        /// and registering the assembly resolve handler used to load
        /// unmanaged/managed machine DLLs at runtime.
        /// </summary>
        internal ReBuzzCore(
            GeneralSettings generalSettings,
            EngineSettings engineSettings,
            string buzzPath,
            string registryRoot,
            IMachineDLLScanner machineDllScanner,
            IUiDispatcher dispatcher,
            IRegistryEx registryEx,
            IFileNameChoice fileNameToLoadChoice,
            IFileNameChoice fileNameToSaveChoice,
            IUserMessages userMessages, 
            IKeyboard keyboard)
        {
            this.fileNameToLoadChoice = fileNameToLoadChoice;
            this.fileNameToSaveChoice = fileNameToSaveChoice;
            this.registryEx = registryEx;
            this.generalSettings = generalSettings;
            this.engineSettings = engineSettings;
            this.buzzPath = buzzPath;
            this.registryRoot = registryRoot;
            this.machineDllScanner = machineDllScanner;
            this.dispatcher = dispatcher;
            this.userMessages = userMessages;

            // Init process and thread priorities
            ProcessAndThreadProfile.SetProfile(engineSettings.PriorityProfile);

            DefaultPatternEditor = "Modern Pattern Editor";

            midiActivityTimer = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(0.5) };
            midiActivityTimer.Tick += (sender, e) =>
            {
                MIDIActivity = false;
                PropertyChanged.Raise(this, "MIDIActivity");
                midiActivityTimer.Stop();
            };

            generalSettings.PropertyChanged += GeneralSettings_PropertyChanged;
            engineSettings.PropertyChanged += EngineSettings_PropertyChanged;

            masterInfo = new MasterInfoExtended
            {
                SamplesPerSec = 44100,
                SamplesPerTick = (int)((60 * 44100) / (126 * 4.0)),
                TicksPerSec = (float)(126 * 4 / 60.0)
            };

            subTickInfo = new SubTickInfoExtended
            {
                CurrentSubTick = 0,
                PosInSubTick = 0
            };

            UpdateMasterInfo();

            GlobalState.LoopStart = 0;
            GlobalState.LoopEnd = 16;
            GlobalState.SongEnd = 16;

            PerformanceData = new BuzzPerformanceData();
            PerformanceCurrent = new BuzzPerformanceData();

            VUMeterLevel = new Tuple<double, double>(0, 0);
            maxSampleLeft = -1;
            maxSampleRight = -1;

            Gear = Gear.LoadGearFile(buzzPath + "\\Gear\\gear_defaults.xml");
            var moreGear = Gear.LoadGearFile(buzzPath + "\\Gear\\gear.xml");
            Gear.Merge(moreGear);
            
            Theme = ReBuzzTheme.LoadCurrentTheme(this, buzzPath);

            DCWriteLine(BuildString);

            MidiInOutEngine = new MidiEngine(this, registryEx);
            MidiInOutEngine.OpenMidiInDevices();
            MidiInOutEngine.OpenMidiOutDevices();

            MidiControllerAssignments = new MidiControllerAssignments(this, registryEx, registryRoot);
            MIDIControllers = MidiControllerAssignments.GetMidiControllerNames().ToReadOnlyCollection();

            themes = Utils.GetThemes(buzzPath);
            profiles = Utils.GetProfiles(buzzPath);

            // Register change callback on all profiles so edits get persisted
            foreach(var p in profiles)
            {
                RegisterProfileSaveCallback(p.Value);
            }

            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += MyResolveEventHandler;

            timerAutomaticBackups = new Timer();
            timerAutomaticBackups.Interval = 10000;
            timerAutomaticBackups.AutoReset = true;
            timerAutomaticBackups.Elapsed += (sender, e) =>
            {
                if (!Playing && SongCore.SongName != null && Modified)
                {
                    try
                    {
                        string backupName = Path.Combine(Path.GetDirectoryName(SongCore.SongName), Path.GetFileNameWithoutExtension(SongCore.SongName) + ".backup");
                        File.Copy(SongCore.SongName, backupName, true);
                    }
                    catch (Exception) { }
                }
            };

            if (generalSettings.WPFSoftwareRendering)
            {
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            }

            // These are not good for real time audio. Just use defaults.
            /*
            if (engineSettings.LowLatencyGC)
            {
                // GCLatencyMode.LowLatency is bad for performance
                // Seems that this has little positive effects on real time audio
                // SustainedLowLatency might be the way to go...
                GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
            }
            */

            Process.GetCurrentProcess().PriorityClass = ProcessAndThreadProfile.ProcessPriorityClassMainProcess;
            Utils.SetProcessorAffinityMask(registryEx, true);

            dtEngineThread = new DispatcherTimer();
            dtEngineThread.Interval = TimeSpan.FromSeconds(1 / 30.0);
            dtEngineThread.Tick += (s, e) =>
            {
                foreach (var m in Song.Machines)
                {
                    var machine = m as MachineCore;
                    machine.UpdateLastEngineThread();
                }
            };
            this.keyboard = keyboard;
        }

        /// <summary>
        /// Start periodic events managed by the core (automatic backups etc.).
        /// Called after initialization once UI and main window are ready.
        /// </summary>
        public void StartEvents()
        {
            if (generalSettings.AutomaticBackups)
            {
                timerAutomaticBackups.Start();
            }
        }

        private void EngineSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // These are not good for real time audio. Just use defaults.
            /*
            if (e.PropertyName == "LowLatencyGC")
            {
                if (engineSettings.LowLatencyGC)
                {
                    GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
                }
                else
                {
                    GCSettings.LatencyMode = GCLatencyMode.Interactive;
                }
            }
            */

            if (e.PropertyName == "MachineDelayCompensation")
            {
                UpdateMachineDelayCompensation();
            }
        }

        private void DeleteBackup()
        {
            if (SongCore.SongName != null)
            {
                timerAutomaticBackups.Stop();
                int len = SongCore.SongName.Length;
                string backupName = SongCore.SongName.Remove(len - 4, 4) + ".backup";
                if (File.Exists(backupName))
                {
                    File.Delete(backupName);
                }
                timerAutomaticBackups.Start();
            }

        }

        private void GeneralSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "AutomaticBackups")
            {
                if (generalSettings.AutomaticBackups)
                {
                    timerAutomaticBackups.Start();
                }
                else
                {
                    timerAutomaticBackups.Stop();
                }
            }
            else if (e.PropertyName == "WPFSoftwareRendering")
            {
                if (generalSettings.WPFSoftwareRendering)
                {
                    RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
                }
                else
                {
                    RenderOptions.ProcessRenderMode = RenderMode.Default;
                }
            }
        }

        /// <summary>
        /// Assembly resolve handler used to locate and load assemblies (e.g.
        /// machine DLLs) from the buzzPath. This allows managed plugin DLLs
        /// shipped with the application to be found at runtime.
        /// </summary>
        private Assembly MyResolveEventHandler(object sender, ResolveEventArgs args)
        {
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            var loadedAssembly = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.FullName == args.Name).FirstOrDefault();
            if (loadedAssembly != null)
            {
                return loadedAssembly;
            }

            string strTempAssmbPath = "";

            strTempAssmbPath = args.Name.Substring(0, args.Name.IndexOf(","));

            string folderPath = buzzPath;
            string rawAssemblyFile = new AssemblyName(args.Name).Name;
            string rawAssemblyPath = Path.Combine(folderPath, rawAssemblyFile);

            string assemblyPath = rawAssemblyPath + ".dll";
            Assembly assembly = null;

            if (File.Exists(assemblyPath))
            {
                assembly = Assembly.LoadFile(assemblyPath);
            }

            return assembly;
        }

        public void ScanDlls()
        {
            // Use the configured scanner to populate available machine DLLs.
            MachineDLLsList = machineDllScanner.GetMachineDLLs(this, buzzPath);
        }

        internal void UpdateInstrumentList(IMachineDatabase mdb)
        {
            InstrumentManager instrumentManager = new InstrumentManager();
            InstrumentList = instrumentManager.CreateInstrumentsList(this, mdb);
            PropertyChanged.Raise(this, "Instruments");
        }

        internal void AddInstrument(MachineDLL mDll)
        {
            Instrument inst = InstrumentManager.CreateFromMoreMachines(mDll);
            InstrumentList.Add(inst);
            PropertyChanged.Raise(this, "Instruments");
        }

        public void StartTimer()
        {
            dtVUMeter = new DispatcherTimer();
            dtVUMeter.Interval = TimeSpan.FromMilliseconds(1000 / 30);
            dtVUMeter.Tick += (sender, e) =>
            {
                if (maxSampleLeft >= 0)
                {
                    var db = Math.Min(Math.Max(Decibel.FromAmplitude(maxSampleLeft), -VUMeterRange), 0.0);
                    double left = (db + VUMeterRange) / VUMeterRange;
                    db = Math.Min(Math.Max(Decibel.FromAmplitude(maxSampleRight), -VUMeterRange), 0.0);
                    double right = (db + VUMeterRange) / VUMeterRange;

                    maxSampleLeft = 0;
                    maxSampleRight = 0;

                    if ((left >= 0) && (right >= 0) && (left != VUMeterLevel.Item1 || right != VUMeterLevel.Item2))
                    {
                        VUMeterLevel = new Tuple<double, double>(left, right);
                        PropertyChanged.Raise(this, "VUMeterLevel");
                    }
                }
            };
            dtVUMeter.Start();
        }

        public void ActivatePatternEditor()
        {
            NewSequenceEditorActivate = false;
            if (ActiveView != BuzzView.PatternView)
            {
                ActiveView = BuzzView.PatternView;
            }
            PatternEditorActivated?.Invoke();

            if (PatternEditorPattern != null)
            {
                var mc = PatternEditorPattern.Machine as MachineCore;
                var em = mc.EditorMachine;
                MachineManager.ActivateEditor(em);
            }
        }

        public void ActivateSequenceEditor()
        {
            NewSequenceEditorActivate = true;
            if (ActiveView != BuzzView.PatternView)
            {
                ActiveView = BuzzView.PatternView;
            }
            SequenceEditorActivated?.Invoke();
        }

        public void AddMachineDLL(string path, MachineType type)
        {
            // AddMachineDLL is called by MDBTab to add "More Machines" during start
            try
            {
                string libName = Path.GetFileNameWithoutExtension(path);
                string libPath = path;

                if (!machineDLLsList.ContainsKey(libName))
                {
                    XMLMachineDLL mDll = new XMLMachineDLL();
                    mDll.Name = libName;
                    mDll.Path = libPath;
                    mDll.MachineInfo = new XMLMachineInfo();
                    mDll.MachineInfo.Name = libName;
                    mDll.MachineInfo.ShortName = libName;
                    mDll.MachineInfo.Type = type;

                    XMLMachineDLL[] mdxmlArray = [mDll];
                    machineDllScanner.AddMachineDllsToDictionary(this, mdxmlArray, machineDLLsList);

                    if (machineDLLsList.ContainsKey(libName))
                    {
                        var dll = machineDLLsList[libName];
                        dll.Buzz = this;
                        AddInstrument(machineDLLsList[libName]);
                        //PropertyChanged.Raise(this, "MachineDLLs");
                    }
                }
            }
            catch { }
        }

        public bool CanExecuteCommand(BuzzCommand cmd)
        {
            if (cmd == BuzzCommand.Stop)
            {
                return true;
            }
            return true;
        }

        public void ConfigureAudioDriver()
        {
            AudioEngine.ShowControlPanel();
        }

        public void DCWriteLine(string s)
        {
            DCWriteLine(s, DCLogLevel.Information);
        }

        public void DCWriteLine(string s, DCLogLevel level)
        {
            if (s != null)
            {
                s = s.Replace("\x01", "");
                switch (level)
                {
                    case DCLogLevel.Verbose:
                        Log.Verbose(s);
                        break;
                    case DCLogLevel.Debug:
                        Log.Debug(s);
                        break;
                    case DCLogLevel.Information:
                    default:
                        Log.Information(s);
                        break;
                    case DCLogLevel.Warning:
                        Log.Warning(s);
                        break;
                    case DCLogLevel.Error:
                        Log.Error(s);
                        break;
                    case DCLogLevel.Fatal:
                        Log.Fatal(s);
                        break;
                }
            }
        }

        public void ExecuteCommand(BuzzCommand cmd)
        {
            if (cmd == BuzzCommand.DebugConsole)
            {
                BuzzCommandRaised.Invoke(cmd);
            }
            else if (cmd == BuzzCommand.About)
            {
                BuzzCommandRaised.Invoke(cmd);
            }
            else if (cmd == BuzzCommand.Exit)
            {
                BuzzCommandRaised.Invoke(cmd);
            }
            else if (cmd == BuzzCommand.Stop)
            {
                //lock (AudioLock)
                {
                    Playing = Recording = false;
                    if (SoloPattern != null)
                    {
                        SoloPattern.IsPlayingSolo = false;
                    }
                }
            }
            else if (cmd == BuzzCommand.OpenFile)
            {
                ChosenValue<string> fileName = fileNameToLoadChoice.SelectFileName();

                if (fileName.HasValue)
                {
                    OpenSongFile(fileName.Value());
                    BuzzCommandRaised?.Invoke(cmd);
                }
            }
            else if (cmd == BuzzCommand.SaveFile)
            {
                SaveSongFile(SongCore.SongName);
            }
            else if (cmd == BuzzCommand.SaveFileAs)
            {
                SaveSongFile(null);
            }
            else if (cmd == BuzzCommand.NewFile)
            {
                if (CheckSaveSong())
                {
                    NewSong();
                    BuzzCommandRaised?.Invoke(cmd);
                }
            }
            else if (cmd == BuzzCommand.Undo || cmd == BuzzCommand.Redo || cmd == BuzzCommand.Copy || cmd == BuzzCommand.Cut || cmd == BuzzCommand.Paste)
            {
                BuzzCommandRaised?.Invoke(cmd);
            }
            /*
            else if (cmd == BuzzCommand.Redo)
            {

            }
            else if (cmd == BuzzCommand.Copy)
            {

            }
            else if (cmd == BuzzCommand.Cut)
            {

            }
            else if (cmd == BuzzCommand.Paste)
            {
            }
            */
            else if (cmd == BuzzCommand.Preferences)
            {
                BuzzCommandRaised?.Invoke(cmd);
            }
        }

        internal void Release()
        {
            engineSettings.PropertyChanged -= EngineSettings_PropertyChanged;
            generalSettings.PropertyChanged -= GeneralSettings_PropertyChanged;

            MidiControllerAssignments.Song = null;

            timerAutomaticBackups.Stop();
            MidiInOutEngine.ReleaseAll();
            AudioEngine.FinalStop();
            DeleteBackup();
        }

        private bool CheckSaveSong()
        {
            if (Modified)
            {
                var result = Utils.MessageBox("Save changes to " + (SongCore.SongName == null ? "Untitled" : SongCore.SongName), "ReBuzz", MessageBoxButton.YesNoCancel);
                if (result == MessageBoxResult.Yes)
                {
                    SaveSongFile(SongCore.SongName);
                    return true;
                }

                if (result == MessageBoxResult.Cancel)
                {
                    return false;
                }
            }

            return true;
        }

        private void UpdateRecentFilesList(string fileName)
        {
            var files = registryEx.ReadNumberedList<string>("File", "Recent File List").ToList();
            foreach (var file in files.ToArray())
            {
                if (file == fileName)
                {
                    files.Remove(file);
                }
            }

            // Move to top
            files.Insert(0, fileName);

            try
            {
                registryEx.DeleteCurrentUserSubKey(registryRoot + "\\" + "Recent File List");
            }
            catch { }

            var regKey = registryEx.CreateCurrentUserSubKey(registryRoot + "\\" + "Recent File List");
            int maxFiles = Math.Min(files.Count, 10);
            for (int i = 0; i < maxFiles; i++)
            {
                regKey.SetValue("File" + (i + 1), files[i]);
            }
        }

        public IMachine GetIMachineFromCMachinePtr(IntPtr pmac)
        {
            return Song.Machines.FirstOrDefault(m => m.CMachinePtr == pmac);
        }

        internal bool NewSequenceEditorActivate { get; set; }
        public void NewSequenceEditorActivated()
        {
            NewSequenceEditorActivate = true;
        }

        internal event Action<FileEventType, string, object> FileEvent;
        internal event Action<string> OpenFile;

        /// <summary>
        /// Open and load a song file into the current session. Handles backup,
        /// audio stop/play, song creation and sends events for UI and machines.
        /// </summary>
        public void OpenSongFile(string filename)
        {
            if (CheckSaveSong())
            {
                masterLoading = true;
                AudioEngine.Stop();

                DeleteBackup();
                NewSong();

                lock (AudioLock)
                {
                    SkipAudio = true;
                    bool playing = Playing;
                    Playing = false;
                }

                IReBuzzFile bmxFile = GetReBuzzFile(filename);

                try
                {
                    bmxFile.FileEvent += (type, eventText, o) =>
                    {
                        FileEvent?.Invoke(type, eventText, o);
                    };

                    OpenFile.Invoke(filename);
                    bmxFile.Load(filename);
                }
                catch (Exception e)
                {
                    userMessages.Error(e.InnerException == null ? e.Message : e.InnerException.Message, "Error loading " + filename, e);
                    bmxFile.EndFileOperation(false);
                    NewSong();
                    SkipAudio = false;
                    return;
                }

                SongCore.SongName = filename;
                UpdateRecentFilesList(filename);
                if (OpenSong != null)
                {
                    OpenSongCore os = new OpenSongCore();
                    os.Song = songCore;
                    var subSelections = bmxFile.GetSubSections();
                    foreach (var sub in subSelections)
                    {
                        os.AddStream(sub.Key, sub.Value);
                    }

                    OpenSong.Invoke(os);
                }
                SongCore.PlayPosition = 0;

                SkipAudio = false;
                Modified = false;
                AudioEngine.Play();

                dispatcher.BeginInvoke(() =>
                {
                    masterLoading = false;
                });

                //Playing = playing;
            }
        }

        internal void ImportSong(float x, float y)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "All songs (*.bmw, *.bmx, *bmxml)|*.bmw;*.bmx;*.bmxml|Songs with waves (*.bmx)|*.bmx|Songs without waves (*.bmw)|*.bmw|ReBuzz XML (*.bmxml)|*.bmxml";
            if (openFileDialog.ShowDialog() == true)
            {
                IReBuzzFile rebuzzFile = GetReBuzzFile(openFileDialog.FileName);
                var filename = openFileDialog.FileName;

                var impotAction = new ImportSongAction(this, rebuzzFile, filename, x, y, this.dispatcher, engineSettings);
                songCore.ActionStack.Do(impotAction);
            }
        }

        IReBuzzFile GetReBuzzFile(string path)
        {
            IReBuzzFile file;
            string extension = Path.GetExtension(path);
            if (extension == ".bmx" || extension == ".bmw")
            {
                file = new BMXFile(this, buzzPath, dispatcher, keyboard, engineSettings);
            }
            else
            {
                file = new BMXMLFile(this, buzzPath, dispatcher, engineSettings);
            }
            return file;
        }

        /// <summary>
        /// Save the current song to disk. If filename is null prompts the user
        /// for a file path via the configured save handler.
        /// </summary>
        public void SaveSongFile(string filename)
        {

            DeleteBackup();

            // Check filename
            if (filename == null)
            {
                ChosenValue<string> saveFileName = fileNameToSaveChoice.SelectFileName();

                if (saveFileName.HasValue)
                {
                    filename = saveFileName.Value();
                    songCore.SongName = filename;
                    UpdateRecentFilesList(filename);
                }
                else
                {
                    return;
                }
            }

            // Do save
            IReBuzzFile file = GetReBuzzFile(filename);

            SaveSongCore ss = new SaveSongCore();
            ss.Song = songCore;
            SaveSong.Invoke(ss);

            file.SetSubSections(ss);

            AudioEngine.Stop();
            lock (AudioLock)
            {
                try
                {
                    file.Save(filename);
                }
                catch (Exception ex)
                {
                    Utils.MessageBox("Error saving file " + filename + "\n\n" + ex, "Error saving file.");
                }
            }

            AudioEngine.Play();
            Modified = false;
        }

        public int RenderAudio(float[] buffer, int nsamples, int samplerate)
        {
            var ap = AudioEngine.GetAudioProvider();
            if (ap != null)
            {
                ap.ReadOverride(buffer, 0, nsamples);
                return nsamples;
            }
            return 0;
        }

        /// <summary>
        /// Entry point for MIDI input from devices. Routes incoming MIDI messages
        /// to the focused machine/editor and other machines depending on settings.
        /// </summary>
        public void SendMIDIInput(int data)
        {
            if (midiFocusMachine != null && !midiFocusMachine.DLL.IsMissing)
            {
                var focusMachine = midiFocusMachine as MachineCore;
                var editor = (midiFocusMachine as MachineCore).EditorMachine;
                bool polacConversion = (midiFocusMachine.DLL.Name == "Polac VSTi 1.1") ||
                    (midiFocusMachine.DLL.Name == "Polac VST 1.1");

                int b = MIDI.DecodeStatus(data);
                int channel = 0;

                if ((b & 0xF0) == 0xF0)
                {
                    // both bytes are used for command code in this case
                }
                else
                {
                    channel = (b & 0x0F);
                }

                // Don't double send MIDI to machine
                MachineCore machineHandled = null;

                // Route channel 1 MIDI input to the active machine.
                if (Global.MIDISettings.MasterKeyboardMode)
                {
                    if (channel == 0)
                    {
                        SendMidiInput(focusMachine, editor, data, polacConversion);
                        machineHandled = focusMachine;
                    }

                    foreach (var m in Song.Machines)
                    {
                        var mc = m as MachineCore;
                        if (mc.DLL.Info.Type != MachineType.Master && (mc.MIDIInputChannel == 0 || mc.MIDIInputChannel == channel + 1) && mc != machineHandled)
                        {
                            SendMidiInput(mc, mc.EditorMachine, data, polacConversion);
                        }
                    }
                }
                else
                {
                    // Don't send all MIDI to all machines like old Buzz used to do.
                    if (Global.MIDISettings.MIDIFiltering)
                    {
                        foreach (var m in Song.Machines)
                        {
                            var mc = m as MachineCore;
                            if (mc.DLL.Info.Type != MachineType.Master && (mc.MIDIInputChannel == 0 || mc.MIDIInputChannel == channel + 1) && mc != machineHandled)
                            {
                                SendMidiInput(mc, mc.EditorMachine, data, polacConversion);
                            }
                        }
                    }
                    else if (!Global.MIDISettings.MIDIFiltering)
                    {
                        foreach (var m in Song.Machines)
                        {
                            var mc = m as MachineCore;
                            if (mc.DLL.Info.Type != MachineType.Master && mc != machineHandled)
                                SendMidiInput(mc, mc.EditorMachine, data, polacConversion);
                        }
                    }
                }
            }

            // Some UI components require MIDIInput.Invoke to be called from UI thread (Midi Keyboard)
            dispatcher.Invoke(new Action(() =>
            {
                MIDIActivity = true;
                MIDIInput?.Invoke(data);
                }
            ));
        }

        void SendMidiInput(MachineCore machine, MachineCore editor, int data, bool polacConversion)
        {
            if (editor == null)
            {
                // Send to machine
                MachineManager.SendMidiInput(machine, data, polacConversion);
            }
            else
            {
                // Send to editor. Editor will send midi message to machine.
                MachineManager.SendMidiInput(editor, data, polacConversion);
            }
        }

        public IEnumerable<Tuple<int, string>> GetMidiOuts()
        {
            var mos = MidiInOutEngine.GetMidiOutputDevices();
            List<Tuple<int, string>> moList = new List<Tuple<int, string>>();
            foreach (var mo in mos)
            {
                var di = MidiOut.DeviceInfo(mo);
                moList.Add(new Tuple<int, string>(mo, di.ProductName));
            }

            return moList;
        }

        public void SendMIDIOutput(int device, int data)
        {
            MidiInOutEngine.SendMidiOut(device, data);
        }

        public event Action<UserControl> SetPatternEditorControl;
        public event Action<IPattern> SetPatternEditorPatternChanged;
        public event Action<IMachine> SelectedMachineChanged;


        public void SetPatternEditorMachine(IMachine m)
        {
            PatternEditorMachine = m;
            UserControl patternEditorControl;
            MachineCore mc;
            if (m != null)
            {
                mc = m as MachineCore;
            }
            else
            {
                mc = Song.Machines.Last() as MachineCore;
            }


            var em = mc.EditorMachine;
            patternEditorControl = MachineManager.GetPatternEditorControl(em);

            if (SetPatternEditorControl != null)
            {
                SetPatternEditorControl.Invoke(patternEditorControl);
            }

            if (SelectedMachineChanged != null)
            {
                SelectedMachineChanged.Invoke(mc);
            }
        }

        public void SetPatternEditorPattern(IPattern p)
        {
            PatternEditorPattern = p;
            if (p != null)
            {
                var mc = p.Machine as MachineCore;

                if (!mc.Ready)
                    return;

                var em = mc.EditorMachine;
                if (em == null)
                {
                    // Create editor
                    CreateEditor(mc, null, null);
                    em = mc.EditorMachine;
                }

                UserControl patternEditorControl = MachineManager.GetPatternEditorControl(em);

                // Calling this first time will link editor machine to machine owning pattern p.
                // Make "AssingEditorToMachineMethod"?
                MachineManager.SetPatternEditorPattern(em, p);
                if (SetPatternEditorControl != null && patternEditorControl != null)
                {
                    SetPatternEditorControl.Invoke(patternEditorControl);
                }
            }
            else
            {
                SetPatternEditorControl.Invoke(null);
            }
            SetPatternEditorPatternChanged?.Invoke(p);
        }


        public void TakePerformanceDataSnapshot()
        {
            lock (AudioLock)
            {
                PerformanceData = new BuzzPerformanceData();
                foreach (var m in Song.Machines)
                {
                    var machine = m as MachineCore;
                    var pd = new MachinePerformanceData();
                    var pdc = machine.PerformanceDataCurrent;
                    pd.MaxEngineLockTime = pdc.MaxEngineLockTime;
                    pd.PerformanceCount = pdc.PerformanceCount;
                    pd.CycleCount = pdc.CycleCount * 1000;
                    pd.SampleCount = pdc.SampleCount;

                    machine.PerformanceData = pd;
                    pdc.MaxEngineLockTime = 0;
                }

                long now = DateTime.Now.Ticks;
                PerformanceCurrent.PerformanceCount += now - performanceCountTime;
                performanceCountTime = now;

                PerformanceData.EnginePerformanceCount = PerformanceCurrent.EnginePerformanceCount;
                PerformanceData.PerformanceCount = PerformanceCurrent.PerformanceCount;
            }
        }

        const double VUMeterRange = 80.0;
        float maxSampleLeft;
        float maxSampleRight;

        internal Gear Gear { get; }
        internal void MasterTapSamples(float[] resSamples, int offset, int count)
        {
            var s = GetSongTime();
            float scale = (1.0f / 32768.0f);
            for (int i = 0; i < count; i += 2)
            {
                maxSampleLeft = Math.Max(maxSampleLeft, Math.Abs(resSamples[offset + i]));
                maxSampleRight = Math.Max(maxSampleRight, Math.Abs(resSamples[offset + i + 1]));
            }

            maxSampleLeft *= scale;
            maxSampleRight *= scale;

            float[] samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                samples[i] = resSamples[offset + i];
            }

            dispatcher.BeginInvoke(() =>
            {
                if (MasterTap != null)
                {
                    MasterTap.Invoke(samples, true, s);
                }
            });
        }

        internal MachineCore CloneMachine(MachineCore machineToClone, float x, float y)
        {
            MachineCore machine = null;
            var instInfo = MachineDB.DictLibRef.Values.FirstOrDefault(i => i.InstrumentFullName == machineToClone.InstrumentName);

            var MachineDll = MachineDLLs[instInfo.libName];

            Modified = true;
            return machine;
        }

        internal MachineCore CreateMachine(int id, float x, float y)
        {
            MachineCore machine = null;
            if (MachineDB.DictLibRef.ContainsKey(id))
            {
                var instInfo = MachineDB.DictLibRef[id];
                var MachineDll = MachineDLLs[instInfo.libName];
                machine = MachineManager.CreateMachine(MachineDll.Name, MachineDll.Path, instInfo.InstrumentFullName,
                    null, MachineDll.Info.MinTracks, x, y, false);

                // CreateEditor(machine, null, null);

                MIDIFocusMachine = machine;
                Modified = true;
            }

            return machine;
        }

        internal MachineCore CreateMachine(string machine, string instrument, string name, byte[] data,
            string patternEditor, byte[] patterneditordata, int trackcount, float x, float y, string editorName = null)
        {
            if (machine == "Master")
                return null;

            string path = null;
            if (MachineDLLs.ContainsKey(machine))
            {
                var MachineDll = MachineDLLs[machine];
                path = MachineDll.Path;

                if (trackcount < 0)
                    trackcount = MachineDll.Info.MinTracks;
            }

            if (trackcount <= 0)
                trackcount = 1;

            MachineCore machineCore = MachineManager.CreateMachine(machine, path, instrument, data, trackcount, x, y, false, name);

            //if (songCore.Importing)
            //{
            //    songCore.DictInitData[machineCore] = new MachineInitData() { data = data, tracks = trackcount };
            //}

            if (machineCore != null && patternEditor != null)
            {
                CreateEditor(machineCore, patternEditor, patterneditordata, editorName);
            }

            MIDIFocusMachine = machineCore;

            Modified = true;
            return machineCore;
        }

        internal void CreateEditor(MachineCore machineToUseEditor, string patternEditor, byte[] patterneditordata, string editorName = null)
        {
            // Create pattern editor but keep it hidden
            MachineCore peMachine = null;
            IMachineDLL editorMachineDll = null;

            if (patternEditor != null)
            {
                if (MachineDLLs.ContainsKey(patternEditor))
                {
                    editorMachineDll = MachineDLLs[patternEditor];
                }
                else
                {
                    if (!Gear.HasSameDataFormat(patternEditor, DefaultPatternEditor))
                    {
                        // Clear pattern editor data if not compatible with MPE
                        patterneditordata = null;
                    }
                    editorMachineDll = MachineDLLs[DefaultPatternEditor];
                }
            }
            else
            {
                editorMachineDll = MachineDLLs[DefaultPatternEditor];
            }
            string name = editorName == null ? GetNewEditorName() : editorName;
            peMachine = MachineManager.CreateMachine(editorMachineDll.Name, editorMachineDll.Path, null, patterneditordata, editorMachineDll.Info.MinTracks, 0, 0, true, name);

            // Connect editor to master
            var master = SongCore.Machines.FirstOrDefault(m => m.DLL.Info.Type == MachineType.Master);
            new ConnectMachinesAction(this, peMachine, master, 0, 0, 0x4000, 0x4000, dispatcher, engineSettings).Do();

            // Link machine to editor. Maybe specific call?
            MachineManager.SetPatternEditorPattern(peMachine, machineToUseEditor.Patterns.FirstOrDefault());
            machineToUseEditor.EditorMachine = peMachine;
        }

        public string GetNewEditorName()
        {
            string name = ((char)1) + "pe";
            int num = 1;
            while (songCore.MachinesList.FirstOrDefault(m => m.Name == (name + num)) != null)
            {
                num++;
            }

            return name + num;
        }

        public static Lock AudioLock = new();
        public MachineCore CreateMaster()
        {
            var machine = MachineManager.GetMaster(this);
            MIDIFocusMachine = machine;
            AddMachine(machine);

            // Create editor for Master
            CreateEditor(machine, DefaultPatternEditor, null);

            var masterGlobalParameters = machine.ParameterGroups[1].Parameters;
            masterGlobalParameters[0].SubscribeEvents(0, MasterVolumeChanged, null);
            masterGlobalParameters[1].SubscribeEvents(0, BPMChanged, null);
            masterGlobalParameters[2].SubscribeEvents(0, TPBChanged, null);

            Modified = false;
            return machine;
        }

        private void TPBChanged(IParameter parameter, int track)
        {
            TPB = parameter.GetValue(track);
        }

        private void BPMChanged(IParameter parameter, int track)
        {
            BPM = parameter.GetValue(track);
        }

        private void MasterVolumeChanged(IParameter parameter, int track)
        {
            MasterVolume = (parameter.MaxValue - parameter.GetValue(track)) / (double)parameter.MaxValue;
        }

        public MachineCore GetMachineFromHostID(long id)
        {
            return SongCore.MachinesList.FirstOrDefault(m => m.CMachineHost == id);
        }

        internal bool RenameMachine(MachineCore machine, string name)
        {
            lock (AudioLock)
            {
                if (machine.Name != name)
                {
                    string oldName = machine.Name;
                    machine.Name = MachineManager.GetNewMachineName(name);
                    Modified = true;

                    return true;
                }
                return false;
            }
        }

        internal void SetModifyFlag(MachineCore machine)
        {
            Modified = true;
        }

        internal void AddMachine(MachineCore machine)
        {
            lock (AudioLock)
            {
                if (!SongCore.MachinesList.Contains(machine))
                {
                    SongCore.MachinesList.Add(machine);
                }

                if (!machine.Hidden)
                {
                    // Make visible in UI
                    SongCore.InvokeMachineAdded(machine);
                }

                if (machine.Ready)
                    machine.Latency = MachineManager.GetMachineLatency(machine);
            }
        }

        internal void InvokeMachineAdded(MachineCore machine)
        {
            if (!machine.Hidden)
            {
                // Make visible in UI
                SongCore.InvokeMachineAdded(machine);
            }
        }

        internal void RemoveAndDeleteMachine(MachineCore machine)
        {
            if (machine.DLL.Info.Type == MachineType.Master)
                return;

            lock (AudioLock)
            {
                foreach (var seq in SongCore.Sequences.Where(s => s.Machine == machine).ToArray())
                {
                    SongCore.RemoveSequence(seq);
                }
                machine.CloseWindows();

                // Disconnect editor machine
                if (machine.EditorMachine != null)
                {
                    foreach (var mc in machine.EditorMachine.AllOutputs.ToArray())
                    {
                        (mc.Destination as MachineCore).AllInputs.Remove(mc);
                    }
                    machine.EditorMachine.AllOutputs.Clear();
                }

                foreach (var pattern in machine.PatternsList.ToArray())
                {
                    machine.DeletePattern(pattern);
                    pattern.ClearEvents();
                }
            }

            SongCore.RemoveMachine(machine.EditorMachine);
            SongCore.RemoveMachine(machine);

            MachineManager.DeleteMachine(machine.EditorMachine);
            MachineManager.DeleteMachine(machine);
            machine.EditorMachine = null;
        }

        internal void RemoveMachine(MachineCore machine)
        {
            if (machine.DLL.Info.Type == MachineType.Master)
                return;

            RemoveAndDeleteMachine(machine);

            // If this is a pattern editor machine, then make sure the current pattern editor
            // isn't the machine being removed.
            if (machine.DLL.Info.Flags.HasFlag(MachineInfoFlags.PATTERN_EDITOR))
            {
                SetPatternEditorMachine(SongCore.Machines.Last());
            }
        }

        internal void RemovePatternEditorMachine(MachineCore patEdMach, MachineCore newEditorMachine)
        {
            if (patEdMach.DLL.Info.Type == MachineType.Master)
                return;

            RemoveAndDeleteMachine(patEdMach);

            // If this is a pattern editor machine, then make sure the current pattern editor
            // isn't the machine being removed.
            if (patEdMach.DLL.Info.Flags.HasFlag(MachineInfoFlags.PATTERN_EDITOR))
            {
                if (newEditorMachine == null)
                    SetPatternEditorMachine(SongCore.Machines.Last());
                else
                    SetPatternEditorMachine(newEditorMachine);
            }
        }

        public void SetModifiedFlag()
        {
            Modified = true;
        }

        internal void RecordControlChange(ParameterCore parameter, int track, int value)
        {
            var machine = parameter.Group.Machine as MachineCore;
            var peMachine = machine.EditorMachine;
            if (Recording && peMachine != null)
            {
                // No need to do this?
                //parameter.SetValue(track | 1 << 16, value);
                MachineManager.RecordContolChange(peMachine, parameter, track, value);
            }
        }

        internal void PlayCurrentEditorPattern()
        {
            PatternEditorPattern.IsPlayingSolo = true;
            Playing = true;
        }

        // Clean up song 
        internal void NewSong()
        {
            SkipAudio = true;
            Playing = false;
            InfoText = "";

            // Create status window
            OpenFile.Invoke("Closing song...");

            DeleteBackup();
            lock (AudioLock)
            {
                AudioEngine.ClearAudioBuffer();

                // Remove groups
                SongCore.RemoveAllGroups();

                var master = SongCore.MachinesList.First();

                FileEvent?.Invoke(FileEventType.StatusUpdate, "Remove Connections...", null);
                // Remove connections
                foreach (var machine in SongCore.MachinesList)
                {
                    // Remove connections
                    foreach (var input in machine.AllInputs.ToArray())
                    {
                        new DisconnectMachinesAction(this, input, dispatcher, engineSettings).Do();
                    }
                }

                // Remove machines
                foreach (var machine in SongCore.MachinesList.ToArray())
                {
                    machine.CloseWindows();
                    if (machine != master)
                    {
                        if (!machine.Hidden)
                            FileEvent?.Invoke(FileEventType.StatusUpdate, "Remove Machine " + machine.Name, null);

                        // Remove machine
                        RemoveMachine(machine);

                        if (machine == master.EditorMachine)
                        {
                            master.EditorMachine = null;
                        }
                    }
                    else
                    {
                        // Master
                        foreach (var pattern in machine.Patterns.ToArray())
                            machine.DeletePattern(pattern);

                        foreach (var seq in SongCore.Sequences.Where(s => s.Machine == machine).ToArray())
                        {
                            SongCore.RemoveSequence(seq);
                        }
                    }
                }


                FileEvent?.Invoke(FileEventType.StatusUpdate, "Clear Wavetable...", null);
                // Clear Wavetable
                for (int i = 0; i < songCore.WavetableCore.WavesList.Count; i++)
                {
                    var wave = songCore.WavetableCore.WavesList[i];
                    if (wave != null)
                    {
                        songCore.WavetableCore.LoadWave(i, null, null, false);
                    }
                }

                // Clear pattern and sequece
                master.PatternsList.Clear();
                SetPatternEditorPattern(null);

                FileEvent?.Invoke(FileEventType.Close, "Done.", null);
            }

            // Center Master position
            List<Tuple<IMachine, Tuple<float, float>>> moveList = new List<Tuple<IMachine, Tuple<float, float>>>
            {
                new Tuple<IMachine, Tuple<float, float>>(Song.Machines.First(), new Tuple<float, float>(0, 0))
            };
            SongCore.MoveMachines(moveList);
            songCore.SongName = null;

            SongCore.ActionStack = new ManagedActionStack();
            SongCore.PlayPosition = 0;
            SongCore.LoopStart = 0;
            SongCore.LoopEnd = 16;
            SongCore.SongEnd = 16;
            TPB = 4;
            BPM = 126;
            Modified = false;

            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
            GC.WaitForFullGCComplete();
            SkipAudio = false;
        }


        string infoText;
        private bool masterLoading;
        private readonly string registryRoot;
        private readonly GeneralSettings generalSettings;
        private readonly EngineSettings engineSettings;
        private readonly string buzzPath;
        private readonly IMachineDLLScanner machineDllScanner;
        internal readonly IUiDispatcher dispatcher;
        private readonly IRegistryEx registryEx;
        private readonly IFileNameChoice fileNameToLoadChoice;
        private readonly IFileNameChoice fileNameToSaveChoice;
        private readonly IUserMessages userMessages;
        private readonly IKeyboard keyboard;

        public string InfoText { get => infoText; set
            {
                if (infoText != value)
                {
                    infoText = value; PropertyChanged.Raise(this, "InfoText");
                }
            }
        }

        public AudioEngine AudioEngine { get; internal set; }
        public string DefaultPatternEditor { get; internal set; }
        public static bool SkipAudio;
        internal IMachine PatternEditorMachine { get; set; }

        internal SongTime GetSongTime()
        {
            SongTime songTime = new SongTime();
            songTime.CurrentTick = Song.PlayPosition;
            songTime.CurrentSubTick = subTickInfo.CurrentSubTick;
            songTime.PosInSubTick = subTickInfo.PosInSubTick;
            songTime.SamplesPerSec = SelectedAudioDriverSampleRate;
            songTime.BeatsPerMin = BPM;
            songTime.PosInTick = masterInfo.PosInTick;
            songTime.SamplesPerSubTick = subTickInfo.SamplesPerSubTick;
            songTime.TicksPerBeat = TPB;
            songTime.SubTicksPerTick = subTickInfo.SubTicksPerTick;
            return songTime;
        }

        internal void PatternEditorCommand(BuzzCommand cmd)
        {
            if (PatternEditorPattern != null)
            {
                var machine = PatternEditorPattern.Machine as MachineCore;

                MachineManager.Command(machine.EditorMachine, (int)cmd);
            }
        }

        public event Action<float[], int> AudioReceived;
        internal void AudioInputAvalable(float[] samples, int n)
        {
            AudioReceived?.Invoke(samples, n);
        }

        public void AudioOut(int channel, Sample[] samples, int n)
        {
            var AudioProvider = AudioEngine.GetAudioProvider();
            AudioProvider?.AudioSampleProvider.FillChannel(channel, samples, n);
        }

        internal bool SetEditorMachineForCurrent(IMachineDLL editorMachine)
        {
            bool ret = false;
            if (PatternEditorPattern != null && PatternEditorPattern.Machine.PatternEditorDLL != editorMachine)
            {
                ret = false;
                var machine = PatternEditorPattern.Machine as MachineCore;
                byte[] data = null;
                var currentEditorMachine = machine.EditorMachine;
                // Change it
                if (Gear.HasSameDataFormat(machine.EditorMachine.DLL.Info.Name, editorMachine.Info.Name))
                    data = machine.EditorMachine.Data;

                lock (AudioLock)
                {
                    try
                    {
                        CreateEditor(machine, editorMachine.Name, data);
                        // Do we need to do this?
                        foreach (var p in machine.Patterns)
                        {
                            MachineManager.SetPatternEditorPattern(machine.EditorMachine, p);
                        }

                        // Remove connections
                        foreach (var output in currentEditorMachine.AllOutputs.ToArray())
                        {
                            new DisconnectMachinesAction(this, output, dispatcher, engineSettings).Do();
                        }

                        //Remove and delete old pattern editor, and replace it with the new one
                        RemovePatternEditorMachine(currentEditorMachine, machine);
                    }
                    catch (Exception)
                    {
                        machine.EditorMachine = currentEditorMachine;
                        ret = true;
                    }
                }

                if (PatternEditorPattern != null)
                    SetPatternEditorPattern(PatternEditorPattern);
            }
            return ret;
        }

        internal void NotifyOpenFile(string filename)
        {
            OpenFile.Invoke(filename);
        }

        internal void NotifyFileEvent(FileEventType type, string eventText, object o)
        {
            FileEvent?.Invoke(type, eventText, o);
        }

        internal MachineGroupCore CreateMachineGroup(string name, float x, float y)
        {
            var group = new MachineGroupCore(songCore);
            group.Position = new Tuple<float, float>(x, y);
            group.Name = name;
            songCore.MachineGroupsList.Add(group);
            songCore.InvokeMachineGroupAdded(group);
            return group;
        }

        internal void RemoveMachineGroup(MachineGroupCore group)
        {
            group.machines.Clear();

            songCore.MachineGroupsList.Remove(group);
            songCore.InvokeMachineGroupRemoved(group);

            songCore.RemoveGroupFromDictionary(group);
        }

        internal int totalLatency = 0;
        internal void UpdateMachineDelayCompensation()
        {
            totalLatency = HandleLatencyCalcRecursive(songCore.Machines[0] as MachineCore);
        }

        internal int HandleLatencyCalcRecursive(MachineCore machine)
        {
            int maxLatency = 0;
            Dictionary<MachineCore, int> inputMachineLatency = new Dictionary<MachineCore, int>();

            // Get cumulative latency from all inputs
            foreach (var input in machine.Inputs)
            {
                var sourceMachine = input.Source as MachineCore;
                int cumulativeInputLatency = HandleLatencyCalcRecursive(sourceMachine);
                maxLatency = Math.Max(cumulativeInputLatency, maxLatency);
                inputMachineLatency.Add(sourceMachine, cumulativeInputLatency);
            }

            // All inputs handled (if any). Add latency to inputs that are have lower latency than max.
            foreach (var input in machine.Inputs)
            {
                var inputMachine = input.Source as MachineCore;
                var inputConnectionCore = input as MachineConnectionCore;
                int addLatency = 0;
                if (engineSettings.MachineDelayCompensation)
                {
                    addLatency = maxLatency - inputMachineLatency[inputMachine];
                    if (addLatency < 0) addLatency = 0;
                }
                inputConnectionCore.UpdateLatencyBuffers(addLatency);
            }

            int machineLatency = machine.OverrideLatency >= 0 ? machine.OverrideLatency : machine.Latency;

            if (machine.IsBypassed)
                machineLatency = 0;

            return engineSettings.MachineDelayCompensation ? maxLatency + machineLatency : 0;
        }

        public string GetInfoText()
        {
            return InfoText;
        }

        public void SetInfoText(string text)
        {
            InfoText = text;
        }
    }

    public class MasterInfoExtended : MasterInfo
    {
        public double AverageSamplesPerTick { get; internal set; }
    }

    public class SubTickInfoExtended : SubTickInfo
    {
        public double AverageSamplesPerSubTick { get; internal set; }
        public int SubTickReminderCounter { get; internal set; }
    }
}
