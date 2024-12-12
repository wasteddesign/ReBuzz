using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Interop;
using BuzzGUI.Common;
using BuzzGUI.Common.Settings;
using ReBuzz.Audio;
using ReBuzz.Core;
using ReBuzz.FileOps;
using ReBuzz.MachineManagement;

namespace ReBuzz.AppViews;

public class ReBuzzCoreInitialization(ReBuzzCore buzz, string buzzPath, IUiDispatcher dispatcher, IRegistryEx registryEx)
{
    internal void StartReBuzzEngineStep1(PropertyChangedEventHandler onPropertyChanged)
    {
        buzz.PropertyChanged += onPropertyChanged;
        Global.Buzz = buzz;
    }

    // Native machines need a window handle.
    internal void StartReBuzzEngineStep2(IntPtr machineViewHwnd)
    {
        var song = new SongCore(dispatcher);
        song.BuzzCore = buzz;
        buzz.SongCore = song;

        buzz.MachineViewHWND = machineViewHwnd;
        buzz.MainWindowHandle = buzz.MachineViewHWND;
        Global.MachineViewHwndSource = HwndSource.FromHwnd(buzz.MachineViewHWND);

        buzz.StartEvents();
    }

    internal void StartReBuzzEngineStep3(EngineSettings engineSettings, IInitializationObserver observer)
    {
        var machineManager = new MachineManager(buzz.SongCore, engineSettings, buzzPath, dispatcher);
        buzz.MachineManager = machineManager;

        buzz.AudioEngine = new
          AudioEngine(buzz, engineSettings, buzzPath, dispatcher, registryEx);
        buzz.AudioDriversList = buzz.AudioEngine.AudioDevices().Select(ae => ae.Name).ToList();
        observer.NotifyMachineManagerCreated(machineManager);
    }

    internal void StartReBuzzEngineStep4(
      IMachineDatabase machineDb,
      Action<string> machineDbDatabaseEvent,
      Action onPatternEditorActivated,
      Action onSequenceEditorActivated,
      Action<string> onShowSettings,
      Action<UserControl> onSetPatternEditorControl,
      Action<bool> onFullScreenChanged,
      Action<string> onThemeChanged)
    {
        buzz.ScanDlls();

        // Call after MachineDLLs are read
        machineDb.DatabaseEvent += machineDbDatabaseEvent;
        machineDb.CreateDB();
        buzz.MachineDB = machineDb;

        // Init stuff before loading anything else
        buzz.ThemeColors = Common.Utils.GetThemeColors(buzzPath);

        buzz.PatternEditorActivated += onPatternEditorActivated;
        buzz.SequenceEditorActivated += onSequenceEditorActivated;
        buzz.ShowSettings += onShowSettings;
        buzz.SetPatternEditorControl += onSetPatternEditorControl;
        buzz.FullScreenChanged += onFullScreenChanged;
        buzz.ThemeChanged += onThemeChanged;
    }

    internal void StartReBuzzEngineStep5(Action<string> onOpenFile)
    {
        buzz.OpenFile += onOpenFile;
    }


    internal void StartReBuzzEngineStep6()
    {
        // Need to get the HWND from Buzz
        buzz.MachineManager.Buzz = buzz;
        buzz.CreateMaster();

        string audioDriver = registryEx.Read("AudioDriver", "WASAPI", "Settings");
        buzz.SelectedAudioDriver = audioDriver;
    }
}

