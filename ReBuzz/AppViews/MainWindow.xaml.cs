using BuzzGUI.Common;
using BuzzGUI.Common.Settings;
using BuzzGUI.Interfaces;
using BuzzGUI.MachineView;
using BuzzGUI.MachineView.HDRecorder;
using BuzzGUI.PianoKeyboard;
using BuzzGUI.SequenceEditor;
using BuzzGUI.ToolBar;
using BuzzGUI.WavetableView;
using ReBuzz.Common;
using ReBuzz.Core;
using ReBuzz.FileOps;
using ReBuzz.MachineManagement;
using ReBuzz.Midi;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using ReBuzz.AppViews;

namespace ReBuzz
{
  internal interface IInitializationObserver //bug rethink the location
  {
    void NotifyMachineManagerCreated(MachineManager machineManager);
    //bug may be not needed - currently machine database is created outside. void NotifyMachineDbCreated(MachineDatabase machineDb);
  }

  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window, INotifyPropertyChanged, IInitializationObserver
  {
    ReBuzzCore Buzz { get; set; }
    EditorView EditorView { get; set; }
    SequenceEditor seqenceEditor;
    MachineView MachineView { get; set; }
    WavetableVM WavetableVM { get; set; }
    UserControl WavetableControl { get; set; }
    InfoView InfoView { get; set; }

    PreferencesWindow preferencesWindow;

    readonly DebugWindow debugWindow = new DebugWindow();

    StatusWindow statusWindow;
    HDRecorderWindow hDRecorderWindow;

    readonly SplashScreenWindow splashScreenWindow;
    KeyboardWindow keyboardWindow = null;

    WDE.ModernSequenceEditor.SequenceEditor ModernSequenceEditor { get; set; }
    WDE.ModernSequenceEditorHorizontal.SequenceEditor ModernSequenceEditorHorizontal { get; set; }

    MachineManager machineManager;
    ToolBarVM ToolBarVM { get; set; }

    IMachineDatabase MachineDB { get; set; }

    public event PropertyChangedEventHandler PropertyChanged;

    string statusBarItem1;
    public string StatusBarItem1
    {
      get { return statusBarItem1; }
      set
      {
        if (value != statusBarItem1)
        {
          statusBarItem1 = value;
          PropertyChanged.Raise(this, "StatusBarItem1");
        }
      }
    }

    string statusBarItem2;
    private WindowStyle mainWindowStyle;
    private UserControl ToolBarControl;

    public string StatusBarItem2
    {
      get { return statusBarItem2; }
      set
      {
        if (value != statusBarItem2)
        {
          statusBarItem2 = value;
          PropertyChanged.Raise(this, "StatusBarItem2");
        }
      }
    }

    void IInitializationObserver.NotifyMachineManagerCreated(MachineManager machineManager)
    {
      this.machineManager = machineManager;
    }

    void ShutDownReBuzzEngine()
    {
      Buzz.AudioEngine.FinalStop();
      Buzz.MachineManager.Buzz = null;
      Buzz.Release();
    }

    public MainWindow()
    {
      DataContext = this;

      InitializeComponent();

      var generalSettings = Global.GeneralSettings;
      var engineSettings = Global.EngineSettings;
      var buzzPath = Global.BuzzPath;
      var registryRoot = Global.RegistryRoot;
      Buzz = new ReBuzzCore(generalSettings,
        engineSettings,
        buzzPath,
        registryRoot,
        new MachineDLLScanner());

      var reBuzzCoreInitialization = new ReBuzzCoreInitialization(Buzz, buzzPath);
      reBuzzCoreInitialization.StartReBuzzEngineStep1(Buzz_PropertyChanged);

      BuzzGUIStartup.PreInit();

      splashScreenWindow = SplashScreenWindow.CreateAsync("Loading..", this);

      generalSettings.PropertyChanged += GeneralSettings_PropertyChanged;
      if (generalSettings.WPFIdealFontMetrics)
      {
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
      }
      else
      {
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
      }

      reBuzzCoreInitialization.StartReBuzzEngineStep2(new WindowInteropHelper(this).EnsureHandle());

      var buzzCore = Buzz;
      var song = buzzCore.SongCore;

      song.ShowMachineViewContextMenu += (x, y) =>
      {
        MachineView.ContextMenu.IsOpen = true;
      };

      if (MachineView.StaticSettings.ShowEngineThreads)
        Buzz.dtEngineThread.Start();

      MachineView.StaticSettings.PropertyChanged += (s, e) =>
      {
        if (e.PropertyName == "ShowEngineThreads")
        {
          if (MachineView.StaticSettings.ShowEngineThreads)
            Buzz.dtEngineThread.Start();
          else
            Buzz.dtEngineThread.Stop();
        }
      };

      reBuzzCoreInitialization.StartReBuzzEngineStep3(engineSettings, this);

      splashScreenWindow.UpdateText("Scan Plugin DLLs");
      MachineDB = new MachineDatabase(buzzCore, buzzPath); //bug maybe instead just copy or create false execs? Then move this inside step 4? Maybe test the database in isolation first?

      reBuzzCoreInitialization.StartReBuzzEngineStep4(MachineDB,
        machineDbDatabaseEvent: (str) =>
        {
          splashScreenWindow.UpdateText(str);
        },
        onPatternEditorActivated: Buzz_PatternEditorActivated,
        onSequenceEditorActivated: Buzz_SequenceEditorActivated,
        onShowSettings: (activeTab) =>
        {
          SettingsWindow.Show(MachineView,
            activeTab);
        },
        onSetPatternEditorControl: (control) =>
        {
          if (EditorView.editorBorder.Child != null)
            EditorView.editorBorder.Child.Visibility = Visibility.Collapsed;

          EditorView.editorBorder.Child = control;
          if (control != null)
            control.Visibility = Visibility.Visible;
        },
        onFullScreenChanged: (fullScreen) =>
        {
          if (fullScreen)
          {
            mainWindowStyle = mainWindow.WindowStyle;
            mainWindow.WindowState = WindowState.Maximized;
            mainWindow.WindowStyle = WindowStyle.None;
          }
          else
          {
            mainWindow.WindowStyle = mainWindowStyle;
            mainWindow.WindowState = WindowState.Normal;
          }
        },
        onThemeChanged: (theme) =>
        {
          Buzz.ActiveView = BuzzView.MachineView; // ToDo: Machine View issue without changing the ActiveView
          CreateThemedGUI();
        });



      SizeChanged += (sender, e) =>
      {
        ResizeModernSequenceEditor();
      };

      reBuzzCoreInitialization.StartReBuzzEngineStep5(onOpenFile: fileName =>
      {
        statusWindow = StatusWindow.CreateAsync(fileName, this);
        Buzz.FileEvent += Buzz_FileEvent;
      });

      this.Loaded += (sender, e) =>
      {
        splashScreenWindow.UpdateText("Init views");

        // Machine View
        var rd = Utils.GetUserControlXAML<ResourceDictionary>(Buzz.Theme.MachineView.Source, buzzPath);
        MachineView = new MachineView(buzzCore, rd);
        MachineView.MachineGraph = song;
        MachineView.Foreground = new SolidColorBrush(Global.Buzz.ThemeColors["MV Text"]);
        borderMachineView.Child = MachineView;

        /*
        MachineView.Loaded += (s, e2) =>
        {
            // Hack to force MachineView to update
            this.Width--;
            this.Width++;
        };
        */

        Buzz.ActiveView = BuzzView.MachineView;

        Utils.InitUtils(this);

        Buzz.StartTimer();

        rd = Utils.GetUserControlXAML<ResourceDictionary>(Buzz.Theme.MainWindow.Source, buzzPath);
        this.Style = rd["ThemeWindowStyle"] as Style;
        this.Resources.MergedDictionaries.Add(rd);

        // Wavetable
        WavetableVM = new WavetableVM();
        WavetableControl = Utils.GetUserControlXAML<UserControl>(Buzz.Theme.WavetableView.Source, buzzPath);
        WavetableControl.DataContext = WavetableVM;
        WavetableVM.Wavetable = song.Wavetable;

        // Info view
        InfoView = new InfoView();
        rd = Utils.GetUserControlXAML<ResourceDictionary>(Buzz.Theme.InfoView.Source, buzzPath);
        InfoView.Resources.MergedDictionaries.Add(rd);
        borderInfo.Child = InfoView;

        // Editor view
        var res = Utils.GetBuzzThemeResources(Buzz.Theme.SequenceEditor.Source, buzzPath);
        seqenceEditor = new BuzzGUI.SequenceEditor.SequenceEditor(Buzz, res);
        seqenceEditor.SetVisibility(true);
        EditorView = new EditorView(Buzz);
        EditorView.SequenceEditor = seqenceEditor;
        borderEditor.Child = EditorView;

        // Load ToolBar
        ToolBarVM = new ToolBarVM();
        ToolBarVM.Buzz = Buzz;
        ToolBarVM.Song = song;
        ToolBarControl = Common.Utils.GetUserControlXAML<UserControl>(Buzz.Theme.ToolBar.Source, buzzPath);
        ToolBarControl.DataContext = ToolBarVM;
        mainGrid.Children.Add(ToolBarControl);
        Grid.SetRow(ToolBarControl, 0);

        // All set up
        BuzzGUIStartup.Startup();
        seqenceEditor.Song = song;

        CreateSequenceView(song);

        reBuzzCoreInitialization.StartReBuzzEngineStep6();

        song.SongEnd = 16;
        song.LoopEnd = 16;

        UpdateTitle();

        SetStatusBarText("Ready!", 0);
        this.InvalidateMeasure();

        ParseArgs();

        splashScreenWindow.UpdateText("Done");
        splashScreenWindow.CloseWindow();
        Activate();
        Topmost = true;  // important
        Topmost = false; // important
        Focus();         // important

        if (generalSettings.CheckForUpdates)
        {
          BuzzGUI.BuzzUpdate.UpdateService.CheckForUpdates(Buzz);
        }
      };

      this.Closing += (sender, e) =>
      {
        if (Buzz.Modified)
        {
          var result = Utils.MessageBox("Save changes to " + (song.SongName == null ? "Untitled" : song.SongName), "ReBuzz", MessageBoxButton.YesNoCancel);
          if (result == MessageBoxResult.Yes)
          {
            Buzz.SaveSongFile(song.SongName);
          }
          else if (result == MessageBoxResult.Cancel)
          {
            e.Cancel = true;
            return;
          }
        }

        ShutDownReBuzzEngine();

        seqenceEditor.Song = null;
        seqenceEditor.Release();

        ToolBarVM.Song = null;
        ToolBarVM.Buzz = null;

        Buzz.PropertyChanged -= Buzz_PropertyChanged;
        generalSettings.PropertyChanged -= GeneralSettings_PropertyChanged;
      };

      this.Closed += (sender, e) =>
      {
        Environment.Exit(0);
      };

      Buzz.PropertyChanged += (sender, e) =>
      {
        if (e.PropertyName == "ActiveView")
        {
          UpdateTitle();
        }
        else if (e.PropertyName == "Modified")
        {
          UpdateTitle();
        }
        else if (e.PropertyName == "InfoText")
        {
          InfoView.tbInfo.Text = Buzz.InfoText;
        }
        else if (e.PropertyName == "IsHardDiskRecorderWindowVisible")
        {
          if (hDRecorderWindow == null && Buzz.IsHardDiskRecorderWindowVisible == true)
          {
            hDRecorderWindow = new HDRecorderWindow();
            var rd = Utils.GetUserControlXAML<ResourceDictionary>("MachineView\\MachineView.xaml", buzzPath);
            hDRecorderWindow.Resources.MergedDictionaries.Add(rd);

            hDRecorderWindow.MachineGraph = Buzz.SongCore;

            hDRecorderWindow.Topmost = true;
            var interop = new WindowInteropHelper(hDRecorderWindow);
            interop.Owner = Buzz.MachineViewHWND;

            hDRecorderWindow.Closed += (sender, e) =>
            {
              hDRecorderWindow.MachineGraph = null;
              hDRecorderWindow = null;
              Buzz.IsHardDiskRecorderWindowVisible = false;
            };
          }

          if (Buzz.IsHardDiskRecorderWindowVisible == true)
          {
            hDRecorderWindow.Show();
          }
          else if (hDRecorderWindow != null)
          {
            hDRecorderWindow.Close();
          }
        }
        else if (e.PropertyName == "IsPianoKeyboardVisible")
        {
          if (keyboardWindow == null)
          {
            keyboardWindow = new KeyboardWindow(Buzz);

            keyboardWindow.Topmost = true;
            var interop = new WindowInteropHelper(keyboardWindow);
            interop.Owner = Buzz.MachineViewHWND;
          }

          if (Buzz.IsPianoKeyboardVisible == true)
          {
            keyboardWindow.Show();
          }
          else
          {
            keyboardWindow.Hide();
          }
        }
      };

      Buzz.Song.PropertyChanged += (sender, e) =>
      {
        if (e.PropertyName == "Name")
        {
          UpdateTitle();
        }
      };

      Buzz.BuzzCommandRaised += (cmd) =>
      {
        switch (Buzz.ActiveView)
        {
          case BuzzView.PatternView:
            {
              if (seqenceEditor.IsKeyboardFocused)
              {
                if (cmd == BuzzCommand.Copy)
                {
                  if (seqenceEditor.CopyCommand.CanExecute(null))
                    seqenceEditor.CopyCommand.Execute(null);
                }
                else if (cmd == BuzzCommand.Cut)
                {
                  if (seqenceEditor.CutCommand.CanExecute(null))
                    seqenceEditor.CutCommand.Execute(null);
                }
                else if (cmd == BuzzCommand.Paste)
                {
                  if (seqenceEditor.PasteCommand.CanExecute(null))
                    seqenceEditor.PasteCommand.Execute(null);
                }
                else if (cmd == BuzzCommand.Undo)
                {
                  if (seqenceEditor.UndoCommand.CanExecute(null))
                    seqenceEditor.UndoCommand.Execute(null);
                }
                else if (cmd == BuzzCommand.Redo)
                {
                  if (seqenceEditor.RedoCommand.CanExecute(null))
                    seqenceEditor.RedoCommand.Execute(null);
                }
              }
              else
              {
                // Send commands to editor
                if (cmd == BuzzCommand.Copy || cmd == BuzzCommand.Cut || cmd == BuzzCommand.Paste ||
                cmd == BuzzCommand.Undo || cmd == BuzzCommand.Redo)
                {
                  Buzz.PatternEditorCommand(cmd);
                }
              }

            }
            break;
          case BuzzView.MachineView:
            {
              if (cmd == BuzzCommand.Copy)
              {
                if (MachineView.CopyCommand.CanExecute(null))
                  MachineView.CopyCommand.Execute(null);
              }
              else if (cmd == BuzzCommand.Cut)
              {
                if (MachineView.CutCommand.CanExecute(null))
                  MachineView.CutCommand.Execute(null);
              }
              else if (cmd == BuzzCommand.Paste)
              {
                if (MachineView.PasteCommand.CanExecute(null))
                  MachineView.PasteCommand.Execute(null);
              }
              else if (cmd == BuzzCommand.Undo)
              {
                if (song.CanUndo)
                  song.Undo();
              }
              else if (cmd == BuzzCommand.Redo)
              {
                if (song.CanRedo)
                  song.Redo();
              }
            }
            break;
          case BuzzView.SequenceView:
            {
              // FIXME
              if (generalSettings.SequenceView == SequenceEditorType.ModernVertical)
              {
                if (cmd == BuzzCommand.Copy)
                {
                  if (ModernSequenceEditor.CopyCommand.CanExecute(null))
                    ModernSequenceEditor.CopyCommand.Execute(null);
                }
                else if (cmd == BuzzCommand.Cut)
                {
                  if (ModernSequenceEditor.CutCommand.CanExecute(null))
                    ModernSequenceEditor.CutCommand.Execute(null);
                }
                else if (cmd == BuzzCommand.Paste)
                {
                  if (ModernSequenceEditor.PasteCommand.CanExecute(null))
                    ModernSequenceEditor.PasteCommand.Execute(null);
                }
                else if (cmd == BuzzCommand.Undo)
                {
                  if (ModernSequenceEditor.UndoCommand.CanExecute(null))
                    ModernSequenceEditor.UndoCommand.Execute(null);
                }
                else if (cmd == BuzzCommand.Redo)
                {
                  if (ModernSequenceEditor.RedoCommand.CanExecute(null))
                    ModernSequenceEditor.RedoCommand.Execute(null);
                }
              }
              else
              {
                if (cmd == BuzzCommand.Copy)
                {
                  if (ModernSequenceEditorHorizontal.CopyCommand.CanExecute(null))
                    ModernSequenceEditorHorizontal.CopyCommand.Execute(null);
                }
                else if (cmd == BuzzCommand.Cut)
                {
                  if (ModernSequenceEditorHorizontal.CutCommand.CanExecute(null))
                    ModernSequenceEditorHorizontal.CutCommand.Execute(null);
                }
                else if (cmd == BuzzCommand.Paste)
                {
                  if (ModernSequenceEditorHorizontal.PasteCommand.CanExecute(null))
                    ModernSequenceEditorHorizontal.PasteCommand.Execute(null);
                }
                else if (cmd == BuzzCommand.Undo)
                {
                  if (ModernSequenceEditorHorizontal.UndoCommand.CanExecute(null))
                    ModernSequenceEditorHorizontal.UndoCommand.Execute(null);
                }
                else if (cmd == BuzzCommand.Redo)
                {
                  if (ModernSequenceEditorHorizontal.RedoCommand.CanExecute(null))
                    ModernSequenceEditorHorizontal.RedoCommand.Execute(null);
                }
              }
            }
            break;
          case BuzzView.WaveTableView:
            {
              if (cmd == BuzzCommand.Copy)
              {
                if (WavetableVM.CopyCommand.CanExecute(null))
                  WavetableVM.CopyCommand.Execute(null);
              }
              else if (cmd == BuzzCommand.Cut)
              {
                if (WavetableVM.CutCommand.CanExecute(null))
                  WavetableVM.CutCommand.Execute(null);
              }
              else if (cmd == BuzzCommand.Paste)
              {
                if (WavetableVM.PasteCommand.CanExecute(null))
                  WavetableVM.PasteCommand.Execute(null);
              }
              else if (cmd == BuzzCommand.Undo)
              {
                if (WavetableVM.UndoCommand.CanExecute(null))
                  WavetableVM.UndoCommand.Execute(null);
              }
              else if (cmd == BuzzCommand.Redo)
              {
                if (WavetableVM.RedoCommand.CanExecute(null))
                  WavetableVM.RedoCommand.Execute(null);
              }
            }
            break;
          case BuzzView.SongInfoView:
            {
              // ToDo
            }
            break;
        }
        if (cmd == BuzzCommand.About)
        {
          AboutWindow aboutWindow = new AboutWindow("(build " + Buzz.BuildNumber + ")");
          aboutWindow.Owner = this;
          aboutWindow.Topmost = true;
          var rdw = Utils.GetUserControlXAML<Window>("ParameterWindowShell.xaml", buzzPath);

          aboutWindow.Resources.MergedDictionaries.Add(rdw.Resources);
          aboutWindow.ShowDialog();
        }
        else if (cmd == BuzzCommand.NewFile)
        {
          SequenceEditor.ViewSettings.TimeSignatureList.Set(0, 16);
          song.Associations.Clear();
          seqenceEditor.Song = song;
          song.ActionStack = new ManagedActionStack();
        }
        else if (cmd == BuzzCommand.Exit)
        {
          if (Buzz.Modified)
          {
            var result = Utils.MessageBox("Save changes to " + (Buzz.SongCore.SongName == null ? "Untitled" : Buzz.SongCore.SongName), "ReBuzz", MessageBoxButton.YesNoCancel);
            if (result == MessageBoxResult.Yes)
            {
              Buzz.SaveSongFile(Buzz.SongCore.SongName);
            }
            else if (result == MessageBoxResult.Cancel)
            {
              return;
            }
          }
          Buzz.Playing = false;
          Buzz.Release();

          Environment.Exit(0);
        }
        else if (cmd == BuzzCommand.DebugConsole)
        {
          debugWindow.Show();
          debugWindow.BringToTop();
        }
        else if (cmd == BuzzCommand.Preferences)
        {
          if (preferencesWindow == null)
          {
            preferencesWindow = new PreferencesWindow(Buzz);
            var rd = Utils.GetUserControlXAML<ResourceDictionary>("MachineView\\MVResources.xaml", buzzPath);
            preferencesWindow.Resources.MergedDictionaries.Add(rd);
            if (preferencesWindow.ShowDialog() == true)
            {
              Buzz.MidiInOutEngine.ReleaseAll();

              List<int> midiIns = new List<int>();
              foreach (var item in preferencesWindow.lbMidiInputs.Items)
              {
                var lbItem = item as PreferencesWindow.ControllerCheckboxVM;
                if (lbItem.Checked)
                  midiIns.Add(lbItem.Id);
              }
              MidiEngine.SetMidiInputDevices(midiIns);
              Buzz.MidiInOutEngine.OpenMidiInDevices();


              List<int> midiOuts = new List<int>();
              foreach (var item in preferencesWindow.lbMidiOutputs.Items)
              {
                var lbItem = item as PreferencesWindow.ControllerCheckboxVM;
                if (lbItem.Checked)
                  midiOuts.Add(lbItem.Id);
              }
              MidiEngine.SetMidiOutputDevices(midiOuts);
              Buzz.MidiInOutEngine.OpenMidiOutDevices();

              Buzz.MidiControllerAssignments.ClearAll();
              foreach (PreferencesWindow.ControllerVM item in preferencesWindow.lvControllers.Items)
              {
                Buzz.MidiControllerAssignments.Add(item.Name, item.Channel - 1, item.Controller, item.Value);
              }

              Buzz.MIDIControllers = Buzz.MidiControllerAssignments.GetMidiControllerNames().ToReadOnlyCollection();

              Buzz.AudioEngine.FinalStop();
              Buzz.AudioEngine.ReleaseAudioDriver();

              long processorAffinity = preferencesWindow.GetProcessorAffinity();
              RegistryEx.Write("ProcessorAffinity", processorAffinity, "Settings");

              int threadCount = preferencesWindow.cbAudioThreads.SelectedIndex + 1;
              RegistryEx.Write("AudioThreads", threadCount, "Settings");

              int algorithm = preferencesWindow.cbAlgorithms.SelectedIndex;
              RegistryEx.Write("WorkAlgorithm", algorithm, "Settings");

              if (Buzz.SelectedAudioDriver != null)
              {
                try
                {
                  Buzz.AudioEngine.CreateAudioOut(Buzz.SelectedAudioDriver);
                  Buzz.AudioEngine.Play();
                }
                catch { }
              }

              Utils.SetProcessorAffinityMask(true);
            }
            preferencesWindow = null;
          }
        }
      };

      this.PreviewKeyDown += (sender, e) =>
      {
        if (e.Key == Key.F1)
        {
          // Help
          var ps = new ProcessStartInfo(System.IO.Path.Combine(buzzPath, "Help/index.html"))
          {
            UseShellExecute = true,
            Verb = "open"
          };
          Process.Start(ps);
          e.Handled = true;
        }
        else if (e.Key == Key.F2)
        {
          Buzz.ActiveView = BuzzView.PatternView;
          e.Handled = true;
        }
        else if (e.Key == Key.F3)
        {
          Buzz.ActiveView = BuzzView.MachineView;
          e.Handled = true;
        }
        else if (e.Key == Key.F4)
        {
          Buzz.ActiveView = BuzzView.SequenceView;
          e.Handled = true;
        }
        else if (e.Key == Key.F5)
        {
          // Play
          Buzz.Playing = true;
          e.Handled = true;
        }
        else if (e.Key == Key.F6)
        {
          // Play from cursur pos
          seqenceEditor.PlayCursor();
          e.Handled = true;
        }
        else if (e.Key == Key.F7)
        {
          // Record
          Buzz.Recording = true;
          e.Handled = true;
        }
        else if (e.Key == Key.F8)
        {
          // Stop
          Buzz.Playing = false;
          e.Handled = true;
        }
        else if (e.Key == Key.F9)
        {
          // Wavetable
          Buzz.ActiveView = BuzzView.WaveTableView;
          e.Handled = true;
        }
        else if (e.SystemKey == Key.F10)
        {
          // Info
          Buzz.ActiveView = BuzzView.SongInfoView;
          e.Handled = true;
        }
        else if (e.Key == Key.F11)
        {
          // Dialogs
          foreach (var m in song.Machines)
          {
            var mac = m as MachineCore;
            var pw = mac.ParameterWindow;
            if (pw != null)
            {
              if (pw.Visibility == Visibility.Visible)
              {
                pw.Visibility = Visibility.Collapsed;
              }
              else
              {
                pw.Visibility = Visibility.Visible;
              }
            }

            var mgw = mac.MachineGUIWindow; ;
            if (mgw != null)
            {
              if (mgw.Visibility == Visibility.Visible)
              {
                mgw.Visibility = Visibility.Collapsed;
              }
              else
              {
                mgw.Visibility = Visibility.Visible;
              }
            }
          }

          Application.Current.Dispatcher.BeginInvoke(() =>
          {
            Focus();
          });
          e.Handled = true;
        }
        else if (e.Key == Key.F12)
        {
          // Reset Audio
          buzzCore.AudioEngine.Reset();
          e.Handled = true;
        }
        else if (e.Key == Key.Space)
        {
          if (Buzz.ActiveView == BuzzView.PatternView && Buzz.NewSequenceEditorActivate)
          {
            //e.Handled = true;
          }
        }

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
          if (e.Key == Key.C)
          {
            Buzz.ExecuteCommand(BuzzCommand.Copy);
            e.Handled = true;
          }
          else if (e.Key == Key.X)
          {
            Buzz.ExecuteCommand(BuzzCommand.Cut);
            e.Handled = true;
          }
          else if (e.Key == Key.V)
          {
            Buzz.ExecuteCommand(BuzzCommand.Paste);
            e.Handled = true;
          }
          else if (e.Key == Key.Z)
          {
            Buzz.ExecuteCommand(BuzzCommand.Undo);
            e.Handled = true;
          }
          else if (e.Key == Key.Y)
          {
            Buzz.ExecuteCommand(BuzzCommand.Redo);
            e.Handled = true;
          }
          else if (e.Key == Key.S)
          {
            Buzz.ExecuteCommand(BuzzCommand.SaveFile);
            e.Handled = true;
          }
          else if (e.Key == Key.N)
          {
            Buzz.ExecuteCommand(BuzzCommand.NewFile);
            e.Handled = true;
          }
          else if (e.Key == Key.O)
          {
            Buzz.ExecuteCommand(BuzzCommand.OpenFile);
            e.Handled = true;
          }
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
          if (e.Key == Key.S)
          {
            Buzz.ExecuteCommand(BuzzCommand.SaveFileAs);
            e.Handled = true;
          }
        }
      };
    }

    private void CreateSequenceView(ISong song)
    {
      if (ModernSequenceEditor != null)
      {
        ModernSequenceEditor.SizeChanged -= ModernSequenceEditorHorizontal_SizeChanged;
        ModernSequenceEditor.Song = null;
        ModernSequenceEditor.Release();
      }
      if (ModernSequenceEditorHorizontal != null)
      {
        ModernSequenceEditorHorizontal.SizeChanged -= ModernSequenceEditorHorizontal_SizeChanged;
        ModernSequenceEditorHorizontal.Song = null;
        ModernSequenceEditorHorizontal.Release();
      }

      if (Global.GeneralSettings.SequenceView == SequenceEditorType.ModernVertical)
      {
        ModernSequenceEditor = new WDE.ModernSequenceEditor.SequenceEditor(Buzz, Utils.GetBuzzThemeResources(Buzz.Theme.SequenceEditor.Source, Global.BuzzPath));
        borderSequenceEditor.Child = ModernSequenceEditor;
        ModernSequenceEditor.SizeChanged += ModernSequenceEditorHorizontal_SizeChanged;

        ModernSequenceEditor.Song = song;
        ModernSequenceEditor.SetVisibility(true);
        ModernSequenceEditor.MinHeight = 400;
        ModernSequenceEditor.MinWidth = 600;
        ModernSequenceEditor.Foreground = new SolidColorBrush(Global.Buzz.ThemeColors["SE Text"]);
      }
      else
      {
        ModernSequenceEditorHorizontal = new WDE.ModernSequenceEditorHorizontal.SequenceEditor(Buzz, Utils.GetBuzzThemeResources(Buzz.Theme.SequenceEditor.Source, Global.BuzzPath));
        borderSequenceEditor.Child = ModernSequenceEditorHorizontal;
        ModernSequenceEditorHorizontal.SizeChanged += ModernSequenceEditorHorizontal_SizeChanged;

        ModernSequenceEditorHorizontal.Song = song;
        ModernSequenceEditorHorizontal.SetVisibility(true);
        ModernSequenceEditorHorizontal.MinHeight = 400;
        ModernSequenceEditorHorizontal.MinWidth = 600;
        ModernSequenceEditorHorizontal.Foreground = new SolidColorBrush(Global.Buzz.ThemeColors["SE Text"]);
      }
    }

    private void ModernSequenceEditorHorizontal_SizeChanged(object sender, SizeChangedEventArgs e)
    {
      ResizeModernSequenceEditor();
    }

    private void ParseArgs()
    {
      string[] args = Environment.GetCommandLineArgs();
      for (int i = 1; i < args.Length; i++)
      {
        if (File.Exists(args[i]))
        {
          // Open after all has been initialized
          Application.Current.Dispatcher.BeginInvoke(() =>
          {
            Buzz.OpenSongFile(args[i]);
          });
          break;
        }
      }
    }

    private void GeneralSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
      if (e.PropertyName == "WPFIdealFontMetrics")
      {
        if (Global.GeneralSettings.WPFIdealFontMetrics)
        {
          TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
        }
        else
        {
          TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        }
      }
      else if (e.PropertyName == "SequenceView")
      {
        CreateSequenceView(Buzz.Song);
      }
    }

    private void Buzz_FileEvent(FileEventType type, string text, object o)
    {
      if (type == FileEventType.StatusUpdate)
      {
        statusWindow?.UpdateText(text);
        Utils.AllowUIToUpdate();
      }
      else if (type == FileEventType.Close)
      {
        var machines = (IEnumerable<MachineCore>)o;

        if (machines != null)
        {
          foreach (MachineCore machine in machines)
          {
            var mt = MachineView.Machines.FirstOrDefault(mc => mc.Machine == machine);
            if (mt != null)
            {
              mt.IsSelected = true;
            }
          }
        }
        statusWindow?.CloseWindow();
        Buzz.FileEvent -= Buzz_FileEvent;
      }
    }

    private void ResizeModernSequenceEditor()
    {
      if (ModernSequenceEditor != null)
      {
        double h = mainGrid.ActualHeight - mainGrid.RowDefinitions[0].ActualHeight - mainGrid.RowDefinitions[2].ActualHeight;
        if (h != ModernSequenceEditor.ActualHeight)
          ModernSequenceEditor.Height = h;
      }
      else if (ModernSequenceEditorHorizontal != null)
      {
        double h = mainGrid.ActualHeight - mainGrid.RowDefinitions[0].ActualHeight - mainGrid.RowDefinitions[2].ActualHeight;
        if (h != ModernSequenceEditorHorizontal.ActualHeight)
          ModernSequenceEditorHorizontal.Height = h;
      }
    }

    private void CreateThemedGUI()
    {
      if (EditorView != null)
      {
        EditorView.editorBorder.Child = null;
      }

      Buzz.ThemeColors = Utils.GetThemeColors(Global.BuzzPath);
      var song = Buzz.Song;

      // Main Window
      var rd = Utils.GetUserControlXAML<ResourceDictionary>(Buzz.Theme.MainWindow.Source, Global.BuzzPath);
      this.Style = rd["ThemeWindowStyle"] as Style;
      this.Resources.MergedDictionaries.Clear();
      this.Resources.MergedDictionaries.Add(rd);

      // Machine View
      borderMachineView.Child = null;
      if (MachineView != null)
      {
        MachineView.MachineGraph = null;
        MachineView.Release();

      }
      rd = Utils.GetUserControlXAML<ResourceDictionary>(Buzz.Theme.MachineView.Source, Global.BuzzPath);
      MachineView = new MachineView(Buzz, rd);
      MachineView.MachineGraph = song;
      MachineView.Foreground = new SolidColorBrush(Global.Buzz.ThemeColors["MV Text"]);
      borderMachineView.Child = MachineView;

      MachineView.Loaded += (s, e) =>
      {
        // Hack to force MachineView to update
        MachineView.Visibility = Visibility.Visible;
        this.Width--;
        this.Width++;
      };

      // Wavetable
      if (WavetableVM != null)
      {
        borderWavetable.Child = null;
        WavetableVM.Wavetable = null;
      }
      if (WavetableControl != null)
      {
        WavetableControl.DataContext = null;
      }
      WavetableVM = new WavetableVM();
      WavetableControl = Utils.GetUserControlXAML<UserControl>(Buzz.Theme.WavetableView.Source, Global.BuzzPath);
      WavetableControl.DataContext = WavetableVM;
      WavetableVM.Wavetable = song.Wavetable;

      // Info view
      InfoView = new InfoView();
      rd = Utils.GetUserControlXAML<ResourceDictionary>(Buzz.Theme.InfoView.Source, Global.BuzzPath);
      InfoView.Resources.MergedDictionaries.Add(rd);
      borderInfo.Child = InfoView;

      // Editor view
      if (seqenceEditor != null)
      {
        seqenceEditor.Song = null;
        seqenceEditor.Release();
      }
      var res = Utils.GetBuzzThemeResources(Buzz.Theme.SequenceEditor.Source, Global.BuzzPath);
      seqenceEditor = new BuzzGUI.SequenceEditor.SequenceEditor(Buzz, res);
      seqenceEditor.SetVisibility(true);
      EditorView.SequenceEditor = seqenceEditor;
      borderEditor.Child = EditorView;

      // Load ToolBar
      if (ToolBarControl != null)
      {
        mainGrid.Children.Remove(ToolBarControl);

        // ToDo: something is not released?
        //ToolBarVM.Buzz = null;
        //ToolBarVM.Song = null;
      }

      ToolBarVM = new ToolBarVM();
      ToolBarVM.Buzz = Buzz;
      ToolBarVM.Song = song;
      ToolBarControl = Utils.GetUserControlXAML<UserControl>(Buzz.Theme.ToolBar.Source, Global.BuzzPath);
      ToolBarControl.DataContext = ToolBarVM;
      mainGrid.Children.Add(ToolBarControl);
      Grid.SetRow(ToolBarControl, 0);

      // All set up
      seqenceEditor.Song = song;
      CreateSequenceView(song);

      UpdateTitle();
      SetStatusBarText("Ready!", 0);
    }

    private void Buzz_PatternEditorActivated()
    {
      if (Buzz.ActiveView != BuzzView.PatternView)
      {
        Buzz.ActiveView = BuzzView.PatternView;
      }
      var editor = EditorView.editorBorder.Child;
      if (editor != null)
      {
        var m = Buzz.PatternEditorPattern?.Machine as MachineCore;

        EditorView.EditorMachine = m?.EditorMachine.DLL;
        editor.Focus();
      }
      else
      {
        EditorView.Focus();
      }
    }

    private void Buzz_SequenceEditorActivated()
    {
      seqenceEditor.Focus();
    }

    private void UpdateTitle()
    {
      string currentView = "[Machines]";
      if (Buzz.ActiveView == BuzzView.SongInfoView)
        currentView = "[Info]";
      else if (Buzz.ActiveView == BuzzView.PatternView)
        currentView = "[Patterns]";
      else if (Buzz.ActiveView == BuzzView.SequenceView)
        currentView = "[Sequences]";
      else if (Buzz.ActiveView == BuzzView.WaveTableView)
        currentView = "[Wavetable]";

      this.Title = (Buzz.SongCore.SongName == null ? "Untitled" : Buzz.SongCore.SongName) + (Buzz.Modified == true ? "*" : "") + " | " + "ReBuzz Digital Audio Workstation | " + currentView;
    }


    internal void SetStatusBarText(string text, int item)
    {
      if (item == 0)
      {
        StatusBarItem1 = text;
      }
      else
      {
        StatusBarItem2 = text;
      }
    }

    private void Buzz_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
      if (e.PropertyName == "IsCPUMonitorWindowVisible")
      {
        MachineView.IsCPUMonitorVisible = Buzz.IsCPUMonitorWindowVisible;
      }
      else if (e.PropertyName == "ActiveView")
      {
        ChangeActiveview();
      }
    }

    private void ChangeActiveview()
    {
      borderEditor.Visibility = borderMachineView.Visibility = borderSequenceEditor.Visibility =
          borderWavetable.Visibility = borderInfo.Visibility = Visibility.Collapsed;

      foreach (var rd in contentGrid.RowDefinitions)
      {
        rd.Height = GridLength.Auto;
      }

      switch (Buzz.ActiveView)
      {
        case BuzzView.PatternView:
          borderEditor.Visibility = Visibility.Visible;
          contentGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
          borderEditor.Focus();

          if (Buzz.NewSequenceEditorActivate)
          {
            EditorView.gridEditorView.RowDefinitions[0].Focus();
          }
          else
          {
            var editor = EditorView.editorBorder.Child;
            if (editor != null)
            {
              editor.Focus();
            }
          }

          break;
        case BuzzView.MachineView:
          borderMachineView.Visibility = Visibility.Visible;
          // Main Grid behaves strange if row height is not "star"
          contentGrid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
          borderMachineView.Focus();
          MachineView.Focus();
          break;
        case BuzzView.SequenceView:
          borderSequenceEditor.Visibility = Visibility.Visible;
          borderSequenceEditor.Focus();
          if (Global.GeneralSettings.SequenceView == SequenceEditorType.ModernVertical)
            ModernSequenceEditor.Focus();
          else
            ModernSequenceEditorHorizontal.Focus();
          break;
        case BuzzView.WaveTableView:
          // WT is a special case...
          borderWavetable.Child = WavetableControl;
          contentGrid.RowDefinitions[3].Height = new GridLength(1, GridUnitType.Star);
          borderWavetable.Visibility = Visibility.Visible;
          borderWavetable.Focus();
          WavetableControl.Focus();
          break;
        case BuzzView.SongInfoView:
          contentGrid.RowDefinitions[4].Height = new GridLength(1, GridUnitType.Star);
          borderInfo.Visibility = Visibility.Visible;
          borderInfo.Focus();
          InfoView.Focus();
          break;
      }
    }

    internal UserControl GetActiveView()
    {
      switch (Buzz.ActiveView)
      {
        case BuzzView.PatternView:
          return seqenceEditor;
        case BuzzView.MachineView:
          return MachineView;
        case BuzzView.SequenceView:
          if (Global.GeneralSettings.SequenceView == SequenceEditorType.ModernVertical)
            return ModernSequenceEditor;
          else
            return ModernSequenceEditorHorizontal;
        case BuzzView.WaveTableView:
          return WavetableControl;
        case BuzzView.SongInfoView:
          return InfoView;
        default:
          return null;
      }
    }

    internal IActionStack GetActiveActionStack()
    {
      var view = GetActiveView();
      if (view != null)
        return view as IActionStack;
      else
        return null;
    }
  }
}