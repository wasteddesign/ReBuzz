using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace BuzzGUI.ToolBar
{
    public class ToolBarVM : CommandSink, INotifyPropertyChanged
    {
        IBuzz buzz;
        ISong song;
        RegistryMonitor regmon;

        public IBuzz Buzz
        {
            get { return buzz; }
            set
            {
                if (buzz != null)
                {
                    Global.GeneralSettings.PropertyChanged -= new PropertyChangedEventHandler(GeneralSettings_PropertyChanged);
                    buzz.PropertyChanged -= new PropertyChangedEventHandler(BuzzPropertyChanged);
                    regmon.Stop();
                    regmon.RegChanged -= BuzzRegistryChanged;
                    regmon.Dispose();
                    regmon = null;
                }

                buzz = value;

                if (buzz != null)
                {
                    Global.GeneralSettings.PropertyChanged += new PropertyChangedEventHandler(GeneralSettings_PropertyChanged);
                    buzz.PropertyChanged += new PropertyChangedEventHandler(BuzzPropertyChanged);
                    regmon = RegistryEx.CreateMonitor("");
                    regmon.RegChanged += BuzzRegistryChanged;
                    regmon.Start();
                }

            }
        }

        void GeneralSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "WPFIdealFontMetrics":
                    PropertyChanged.Raise(this, "TextFormattingMode");
                    break;
            }
        }

        void BuzzRegistryChanged(object sender, EventArgs e)
        {
            PropertyChanged.Raise(this, "MainMenu");
        }

        public ISong Song
        {
            get { return song; }
            set
            {
                if (song != null)
                {
                    song.PropertyChanged -= SongPropertyChanged;
                    song.MachineAdded -= song_MachineAdded;
                    song.MachineRemoved -= song_MachineRemoved;
                }

                song = value;

                if (song != null)
                {
                    song.PropertyChanged += SongPropertyChanged;
                    song.MachineAdded += song_MachineAdded;
                    song.MachineRemoved += song_MachineRemoved;
                }

            }
        }

        bool guiPlaying = false;
        DateTime songPlayStart;

        void BuzzPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "ActiveView":
                    PropertyChanged.Raise(this, "ShowPatternView");
                    PropertyChanged.Raise(this, "ShowMachineView");
                    PropertyChanged.Raise(this, "ShowSequenceView");
                    PropertyChanged.Raise(this, "ShowWaveTableView");
                    PropertyChanged.Raise(this, "ShowSongInfoView");
                    break;

                case "Song":
                    Song = buzz.Song;
                    break;

                case "BPM":
                    UpdateLoopTime();
                    PropertyChanged.Raise(this, e.PropertyName);
                    break;

                case "TPB":
                    UpdateLoopTime();
                    PropertyChanged.Raise(this, e.PropertyName);
                    break;

                case "Playing":
                    if (buzz.Playing)
                    {
                        if (!guiPlaying)
                        {
                            songPlayStart = DateTime.Now;
                            guiPlaying = true;
                        }
                    }
                    else
                    {
                        guiPlaying = false;
                    }
                    PropertyChanged.Raise(this, e.PropertyName);

                    break;

                case "MIDIFocusMachine":
                    PropertyChanged.Raise(this, e.PropertyName);
                    break;

                case "IsPianoKeyboardVisible":
                    PropertyChanged.Raise(this, e.PropertyName);
                    break;

                case "IsSettingsWindowVisible":
                    break;

                case "IsCPUMonitorWindowVisible":
                    PropertyChanged.Raise(this, "MainMenu");
                    break;

                case "IsHardDiskRecorderWindowVisible":
                    PropertyChanged.Raise(this, "MainMenu");
                    break;

                default:
                    // FIXME
                    if (e.PropertyName != "MachineDLLs" && e.PropertyName != "Modified" && e.PropertyName != "InfoText")
                        PropertyChanged.Raise(this, e.PropertyName);
                    break;
            }
        }

        int lastPlayPosition = -1;

        void SongPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "LoopStart": UpdateLoopTime(); break;
                case "LoopEnd": UpdateLoopTime(); break;
                case "PlayPosition":
                    if (song.PlayPosition != lastPlayPosition)
                    {
                        lastPlayPosition = song.PlayPosition;
                        PropertyChanged.Raise(this, "CurrentTime");
                    }
                    PropertyChanged.Raise(this, "ElapsedTime");
                    break;
            }
        }

        void song_MachineAdded(IMachine m)
        {
            m.PropertyChanged += MachinePropertyChanged;
            machineList.Add(new MachineVM(m));
            PropertyChanged.Raise(this, "MachineList");
        }

        void song_MachineRemoved(IMachine m)
        {
            m.PropertyChanged -= MachinePropertyChanged;
            var vm = machineList.First(x => x.Machine == m);
            machineList.Remove(vm);
            PropertyChanged.Raise(this, "MachineList");
        }

        void MachinePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Name" || e.PropertyName == "PatternEditorDLL")
            {
                var vm = machineList.First(x => x.Machine == sender as IMachine);
                PropertyChanged.Raise(vm, "Name");
            }

        }

        void UpdateLoopTime() { PropertyChanged.Raise(this, "LoopTime"); }

        string Format(TimeSpan t) { return string.Format("{0}:{1:D2}:{2:D2}:{3}", t.Hours, t.Minutes, t.Seconds, t.Milliseconds / 100); }
        public string ElapsedTime { get { return Format(guiPlaying ? DateTime.Now - songPlayStart : TimeSpan.Zero); } }
        public string CurrentTime { get { return song != null ? Format(TimeSpan.FromMinutes((double)(song.PlayPosition) / (buzz.BPM * buzz.TPB))) : ""; } }
        public string LoopTime { get { return song != null ? Format(TimeSpan.FromMinutes((double)(song.LoopEnd - song.LoopStart) / (buzz.BPM * buzz.TPB))) : ""; } }

        public SimpleCommand NewFileCommand { get; private set; }
        public SimpleCommand OpenFileCommand { get; private set; }
        public SimpleCommand SaveFileCommand { get; private set; }
        public SimpleCommand SaveFileAsCommand { get; private set; }
        public SimpleCommand ExitCommand { get; private set; }
        public SimpleCommand AboutCommand { get; private set; }
        public SimpleCommand CutCommand { get; private set; }
        public SimpleCommand CopyCommand { get; private set; }
        public SimpleCommand PasteCommand { get; private set; }
        public SimpleCommand UndoCommand { get; private set; }
        public SimpleCommand RedoCommand { get; private set; }
        public SimpleCommand StopCommand { get; private set; }
        public SimpleCommand ConfigureAudioDriverCommand { get; private set; }
        public SimpleCommand TapTempoCommand { get; private set; }
        public SimpleCommand SetThemeCommand { get; private set; }
        public SimpleCommand SetWPFThemeCommand { get; private set; }
        public SimpleCommand SettingsCommand { get; private set; }
        public SimpleCommand PreferencesCommand { get; private set; }
        public SimpleCommand HelpCommand { get; private set; }
        public SimpleCommand CPUMonitorCommand { get; private set; }
        public SimpleCommand HardDiskRecorderCommand { get; private set; }
        public SimpleCommand DebugConsoleCommand { get; private set; }
        public SimpleCommand OpenCommand { get; private set; }

        public Tuple<double, double> VUMeterLevel { get { return buzz.VUMeterLevel; } }
        public double MasterVolume { get { return buzz.MasterVolume; } set { buzz.MasterVolume = value; } }
        public int BPM { get { return buzz.BPM; } set { buzz.BPM = value; } }
        public int TPB { get { return buzz.TPB; } set { buzz.TPB = value; } }
        public int Speed { get { return buzz.Speed; } set { buzz.Speed = value; } }
        public bool Playing { get { return buzz.Playing; } set { buzz.Playing = value; } }
        public bool Recording { get { return buzz.Recording; } set { buzz.Recording = value; } }
        public bool Looping { get { return buzz.Looping; } set { buzz.Looping = value; } }
        public bool AudioDeviceDisabled { get { return buzz.AudioDeviceDisabled; } set { buzz.AudioDeviceDisabled = value; } }
        public bool IsFullScreen { get { return buzz.IsFullScreen; } set { buzz.IsFullScreen = value; } }

        public class MachineVM : INotifyPropertyChanged
        {
            public IMachine Machine { get; set; }
            public MachineVM(IMachine m) { Machine = m; }
            public string Name
            {
                get
                {
                    var pedll = Machine.PatternEditorDLL;
                    return pedll != null ? string.Format("{0} ({1})", Machine.Name, pedll.Info.ShortName) : Machine.Name;
                }
            }

            #region INotifyPropertyChanged Members

#pragma warning disable 67
            public event PropertyChangedEventHandler PropertyChanged;

            #endregion
        }

        readonly List<MachineVM> machineList = new List<MachineVM>();
        public IEnumerable<MachineVM> MachineList { get { return machineList.OrderBy(m => m.Machine.Name); } }
        public MachineVM MIDIFocusMachine
        {
            get { return machineList.FirstOrDefault(m => m.Machine == buzz.MIDIFocusMachine); }
            set
            {
                if (buzz.MIDIFocusLocked)
                {
                    buzz.MIDIFocusLocked = false;
                    buzz.MIDIFocusMachine = value.Machine;
                    buzz.MIDIFocusLocked = true;
                }
                else
                {
                    buzz.MIDIFocusMachine = value != null ? value.Machine : null;
                }
            }
        }

        public bool MIDIFocusLocked { get { return buzz.MIDIFocusLocked; } set { buzz.MIDIFocusLocked = value; } }
        public bool MIDIActivity { get { return buzz.MIDIActivity; } }

        public bool IsPianoKeyboardVisible { get { return buzz.IsPianoKeyboardVisible; } set { buzz.IsPianoKeyboardVisible = value; } }

        public IEnumerable<string> AudioDrivers { get { return buzz.AudioDrivers; } }
        public string SelectedAudioDriver { get { return buzz.SelectedAudioDriver; } set { buzz.SelectedAudioDriver = value; } }

        public ToolBarVM()
        {
            NewFileCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => { return buzz.CanExecuteCommand(BuzzCommand.NewFile); },
                ExecuteDelegate = x => { buzz.ExecuteCommand(BuzzCommand.NewFile); }
            };

            OpenFileCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => { return buzz.CanExecuteCommand(BuzzCommand.OpenFile); },
                ExecuteDelegate = x =>
                {
                    if (x == null)
                    {
                        buzz.ExecuteCommand(BuzzCommand.OpenFile);
                    }
                    else
                    {
                        buzz.OpenSongFile(x as string);
                        PropertyChanged.Raise(this, "MainMenu");
                    }
                }
            };

            SaveFileCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => { return buzz.CanExecuteCommand(BuzzCommand.SaveFile); },
                ExecuteDelegate = x => { buzz.ExecuteCommand(BuzzCommand.SaveFile); }
            };

            SaveFileAsCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => { return buzz.CanExecuteCommand(BuzzCommand.SaveFileAs); },
                ExecuteDelegate = x => { buzz.ExecuteCommand(BuzzCommand.SaveFileAs); }
            };

            ExitCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => { return buzz.CanExecuteCommand(BuzzCommand.Exit); },
                ExecuteDelegate = x => { buzz.ExecuteCommand(BuzzCommand.Exit); }
            };

            AboutCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => { return buzz.CanExecuteCommand(BuzzCommand.About); },
                ExecuteDelegate = x => { buzz.ExecuteCommand(BuzzCommand.About); }
            };

            CutCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => { return buzz.CanExecuteCommand(BuzzCommand.Cut); },
                ExecuteDelegate = x => { buzz.ExecuteCommand(BuzzCommand.Cut); }
            };

            CopyCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => { return buzz.CanExecuteCommand(BuzzCommand.Copy); },
                ExecuteDelegate = x => { buzz.ExecuteCommand(BuzzCommand.Copy); }
            };

            PasteCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => { return buzz.CanExecuteCommand(BuzzCommand.Paste); },
                ExecuteDelegate = x => { buzz.ExecuteCommand(BuzzCommand.Paste); }
            };

            UndoCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => { return buzz.CanExecuteCommand(BuzzCommand.Undo); },
                ExecuteDelegate = x => { buzz.ExecuteCommand(BuzzCommand.Undo); }
            };

            RedoCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => { return buzz.CanExecuteCommand(BuzzCommand.Redo); },
                ExecuteDelegate = x => { buzz.ExecuteCommand(BuzzCommand.Redo); }
            };

            StopCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => { return buzz.CanExecuteCommand(BuzzCommand.Stop); },
                ExecuteDelegate = x => { buzz.ExecuteCommand(BuzzCommand.Stop); }
            };

            ConfigureAudioDriverCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => { return true; },
                ExecuteDelegate = x => { buzz.ConfigureAudioDriver(); }
            };

            long lastTapTime = 0;

            TapTempoCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => { return true; },
                ExecuteDelegate = x =>
                {
                    var t = DateTime.Now.Ticks;

                    if (lastTapTime > 0 && t > lastTapTime)
                    {
                        var tempo = (int)Math.Round((60.0 * 10000000) / (t - lastTapTime));
                        if (tempo >= 16 && tempo <= 500)
                            buzz.BPM = tempo;
                    }

                    lastTapTime = t;
                }
            };

            SetThemeCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => { return true; },
                ExecuteDelegate = x =>
                {
                    buzz.SelectedTheme = x as string;
                    PropertyChanged.Raise(this, "MainMenu");
                }
            };

            SetWPFThemeCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => { return true; },
                ExecuteDelegate = theme =>
                {
                    RegistryEx.Write("WPFTheme", theme as string, "Settings");
                    Application.Current.LoadWPFTheme();
                    PropertyChanged.Raise(this, "MainMenu");
                }
            };

            SettingsCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => { return true; },
                ExecuteDelegate = x => { Buzz.IsSettingsWindowVisible = true; }
            };

            PreferencesCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => { return true; },
                ExecuteDelegate = x => { Buzz.ExecuteCommand(BuzzCommand.Preferences); }
            };

            HelpCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => { return true; },
                ExecuteDelegate = x =>
                {
                    var ps = new ProcessStartInfo(System.IO.Path.Combine(Global.BuzzPath, "Help/index.html"))
                    {
                        UseShellExecute = true,
                        Verb = "open"
                    };
                    Process.Start(ps);
                }
            };

            CPUMonitorCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => { return true; },
                ExecuteDelegate = x => { Buzz.IsCPUMonitorWindowVisible ^= true; }
            };

            HardDiskRecorderCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => { return true; },
                ExecuteDelegate = x => { Buzz.IsHardDiskRecorderWindowVisible ^= true; }
            };

            DebugConsoleCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => { return true; },
                ExecuteDelegate = x => { Buzz.ExecuteCommand(BuzzCommand.DebugConsole); }
            };

        }

        public bool ShowPatternView
        {
            get { return buzz.ActiveView == BuzzView.PatternView; }
            set { buzz.ActiveView = BuzzView.PatternView; }
        }

        public bool ShowMachineView
        {
            get { return buzz.ActiveView == BuzzView.MachineView; }
            set { buzz.ActiveView = BuzzView.MachineView; }
        }

        public bool ShowSequenceView
        {
            get { return buzz.ActiveView == BuzzView.SequenceView; }
            set { buzz.ActiveView = BuzzView.SequenceView; }
        }

        public bool ShowWaveTableView
        {
            get { return buzz.ActiveView == BuzzView.WaveTableView; }
            set { buzz.ActiveView = BuzzView.WaveTableView; }
        }

        public bool ShowSongInfoView
        {
            get { return buzz.ActiveView == BuzzView.SongInfoView; }
            set { buzz.ActiveView = BuzzView.SongInfoView; }
        }

        // menu

        public IEnumerable<MenuItemVM> ThemeMenuItems
        {
            get
            {
                var g = new MenuItemVM.Group();

                return Buzz.Themes.Select(s => new MenuItemVM()
                {
                    Text = s,
                    IsCheckable = true,
                    IsChecked = s == Buzz.SelectedTheme,
                    CheckGroup = g,
                    Command = SetThemeCommand,
                    CommandParameter = s
                });
            }
        }

        readonly string[] WPFThemes = { "Aero", "Aero Lite", "Classic", "Luna", "Luna Olive Green", "Luna Silver", "Royale" };

        public IEnumerable<MenuItemVM> MainMenu
        {
            get
            {
                yield return new MenuItemVM()
                {
                    Text = "_File",
                    Children = new MenuItemVM[]
                    {
                        new MenuItemVM() { Text = "_New", GestureText = "Ctrl+N", Command = NewFileCommand },
                        new MenuItemVM() { Text = "_Open", GestureText = "Ctrl+O", Command = OpenFileCommand },
                        new MenuItemVM() { Text = "_Save", GestureText = "Ctrl+S", Command = SaveFileCommand },
                        new MenuItemVM() { Text = "Save _As", GestureText = "Ctrl+Shift+S", Command = SaveFileAsCommand  },
                        new MenuItemVM() { IsSeparator = true }
                    }
                    .Concat(RegistryEx.ReadNumberedList<string>("File", "Recent File List")
                        .Select(fn => new MenuItemVM() { Text = Win32.CompactPath(fn, 50), Command = OpenFileCommand, CommandParameter = fn }))
                    .Concat(new MenuItemVM[]
                    {
                        new MenuItemVM() { IsSeparator = true },
                        new MenuItemVM() { Text = "E_xit", Command = ExitCommand }
                    })
                };

                yield return new MenuItemVM()
                {
                    Text = "_Edit",
                    Children = new MenuItemVM[]
                    {
                        new MenuItemVM() { Text = "_Undo", GestureText = "Ctrl+Z", Command = UndoCommand },
                        new MenuItemVM() { Text = "_Redo", GestureText = "Ctrl+Y", Command = RedoCommand },
                        new MenuItemVM() { IsSeparator = true },
                        new MenuItemVM() { Text = "Cu_t", GestureText = "Ctrl+X", Command = CutCommand },
                        new MenuItemVM() { Text = "_Copy", GestureText = "Ctrl+C", Command = CopyCommand },
                        new MenuItemVM() { Text = "_Paste", GestureText = "Ctrl+V", Command = PasteCommand },
                    }

                };

                yield return new MenuItemVM()
                {
                    Text = "_View",
                    Children = new MenuItemVM[]
                    {
                        new MenuItemVM() { Text = "_CPU Monitor", IsCheckable = true, IsChecked = Buzz.IsCPUMonitorWindowVisible, Command = CPUMonitorCommand },
                        new MenuItemVM() { Text = "_Debug Console", Command = DebugConsoleCommand },
                        new MenuItemVM() { Text = "_Hard Disk Recorder", IsCheckable = true, IsChecked = Buzz.IsHardDiskRecorderWindowVisible, Command = HardDiskRecorderCommand },
                        new MenuItemVM() { Text = "_Status Bar", IsCheckable = true, IsEnabled = false },
                        new MenuItemVM() { IsSeparator = true },
                        new MenuItemVM() { Text = "S_ettings...", Command = SettingsCommand },
                        new MenuItemVM() { Text = "_Preferences...", Command = PreferencesCommand },
                    }
                };

                var wpfTheme = RegistryEx.Read<string>("WPFTheme", "", "Settings");

                yield return new MenuItemVM()
                {
                    Text = "_Theme",
                    Children = ThemeMenuItems
                    .Concat(LinqExtensions.Return(new MenuItemVM() { IsSeparator = true }))
                    .Concat(LinqExtensions.Return(new MenuItemVM() { Text = "Windows Default", IsCheckable = true, IsChecked = wpfTheme == "", Command = SetWPFThemeCommand, CommandParameter = "" }))
                    .Concat(WPFThemes.Select(s => new MenuItemVM() { Text = s, IsCheckable = true, IsChecked = wpfTheme == s, Command = SetWPFThemeCommand, CommandParameter = s }))
                };

                yield return new MenuItemVM()
                {
                    Text = "_Help",
                    Children = new MenuItemVM[]
                    {
                        new MenuItemVM() { Text = "_Contents...", Command = HelpCommand },
                        new MenuItemVM() { IsSeparator = true },
                        new MenuItemVM() { Text = "_About ReBuzz...", Command = AboutCommand },
                    }
                };

            }
        }

        public TextFormattingMode TextFormattingMode { get { return Global.GeneralSettings.WPFIdealFontMetrics ? TextFormattingMode.Ideal : TextFormattingMode.Display; } }

        public event PropertyChangedEventHandler PropertyChanged;


    }
}
