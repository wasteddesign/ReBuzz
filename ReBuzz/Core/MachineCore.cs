﻿using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using BuzzGUI.MachineView;
using BuzzGUI.ParameterWindow;
using ReBuzz.Common;
using ReBuzz.Common.Interfaces;
using ReBuzz.ManagedMachine;
using ReBuzz.NativeMachine;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace ReBuzz.Core
{
    public class MachineCore : IMachine, IMachineExtension
    {
        public static long machineHostId = 1;
        public readonly object workLock = new object();
        //internal int level;

        IMachineGraph graph;
        public IMachineGraph Graph { get => graph; set => graph = value; }

        MachineDLL machineDLL;
        public IMachineDLL DLL { get => machineDLL; }

        internal Dictionary<ParameterCore, int> parametersChanged = new Dictionary<ParameterCore, int>();

        internal MachineDLL MachineDLL { get => machineDLL; set => machineDLL = value; }

        readonly List<IMachineConnection> inputs = new List<IMachineConnection>();
        public ReadOnlyCollection<IMachineConnection> Inputs
        {
            get
            {
                // Return only input connections that are coming from a) visible machines and b) non control machines
                return inputs.Where(x => x.Source.OutputChannelCount > 0 && !(x.Source as MachineCore).Hidden).ToReadOnlyCollection();
            }
        }

        public List<IMachineConnection> AllInputs { get => inputs; }

        readonly List<IMachineConnection> outputs = new List<IMachineConnection>();
        public ReadOnlyCollection<IMachineConnection> Outputs
        {
            get
            {
                return outputs.Where(x => x.Destination.InputChannelCount > 0 && !(x.Destination as MachineCore).Hidden).ToReadOnlyCollection();
            }
        }

        public List<IMachineConnection> AllOutputs { get => outputs; }

        readonly Dictionary<int, string> inputChannelNames = new Dictionary<int, string>();
        readonly Dictionary<int, string> outputChannelNames = new Dictionary<int, string>();

        int inputChannelCount = 0;
        public int InputChannelCount
        {
            get => inputChannelCount;
            set
            {
                //if (inputChannelCount != value)
                //lock (ReBuzzCore.AudioLock)
                {
                    inputChannelNames.Clear();
                    if (graph != null)
                    {
                        var bc = graph.Buzz as ReBuzzCore;

                        inputChannelCount = value;

                        for (int i = 0; i < inputChannelCount; i++)
                        {
                            // Get channel name
                            string name = bc.MachineManager.GetChannelName(this, true, i);
                            inputChannelNames[i] = name;
                        }
                    }
                    else
                    {
                        inputChannelCount = value;
                    }

                    // Update inputs
                    foreach (var input in inputs)
                    {
                        if (input.DestinationChannel >= inputChannelCount)
                            (input as MachineConnectionCore).DestinationChannel = 0;
                    }
                    PropertyChanged.Raise(this, "InputChannelCount");
                }
            }
        }

        int outputChannelCount = 0;
        public int OutputChannelCount
        {
            get => outputChannelCount;
            set
            {
                //if (outputChannelCount != value)
                //lock (ReBuzzCore.AudioLock)
                {
                    outputChannelNames.Clear();
                    if (graph != null)
                    {
                        // Needs a lock since audio might be running?
                        var bc = graph.Buzz as ReBuzzCore;

                        outputChannelCount = value;
                        for (int i = 0; i < outputChannelCount; i++)
                        {
                            // Get channel name

                            string name = bc.MachineManager.GetChannelName(this, false, i);
                            outputChannelNames[i] = name;
                        }
                    }
                    else
                    {
                        outputChannelCount = value;
                    }

                    // Update inputs
                    foreach (var output in outputs)
                    {
                        if (output.SourceChannel >= outputChannelCount)
                            (output as MachineConnectionCore).SourceChannel = 0;
                    }

                    PropertyChanged.Raise(this, "OutputChannelCount");
                }
            }
        }


        string name;
        public string Name
        {
            get => name;
            set
            {
                string oldName = name;
                name = value;
                /*
                Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                {
                    PropertyChanged?.Raise(this, "Name");
                }));
                */
                PropertyChanged.Raise(this, "Name");
                var bc = graph.Buzz as ReBuzzCore;
                foreach (var o in outputs)
                {
                    bc.MachineManager.RenameInput(o.Destination as MachineCore, oldName, name);
                }
            }
        }

        Tuple<float, float> position = new Tuple<float, float>(0, 0);
        public Tuple<float, float> Position { get => position; set { position = value; PropertyChanged.Raise(this, "Position"); graph.Buzz.SetModifiedFlag(); } }

        int overSampleFactor = 1;
        public int OversampleFactor { get => overSampleFactor; set { overSampleFactor = value; PropertyChanged.Raise(this, "OversampleFactor"); graph.Buzz.SetModifiedFlag(); } }

        int midiInputChannel = -1;
        public int MIDIInputChannel { get => midiInputChannel; set { midiInputChannel = value; PropertyChanged.Raise(this, "MIDIInputChannel"); graph.Buzz.SetModifiedFlag(); } }

        List<ParameterGroup> parameterGroups;
        public ReadOnlyCollection<IParameterGroup> ParameterGroups { get => parameterGroups.Cast<IParameterGroup>().ToReadOnlyCollection(); }
        internal List<ParameterGroup> ParameterGroupsList { get => parameterGroups; set => parameterGroups = value; }

        List<AttributeCore> attributes = new List<AttributeCore>();
        public ReadOnlyCollection<IAttribute> Attributes { get => attributes.Cast<IAttribute>().ToReadOnlyCollection(); }

        List<IMenuItem> commands = new List<IMenuItem>();
        public IEnumerable<IMenuItem> Commands
        {
            get
            {
                SetCommands(); return commands;
            }
        }

        readonly List<string> envelopeNames = new List<string>();
        public ReadOnlyCollection<string> EnvelopeNames { get => envelopeNames.ToReadOnlyCollection(); }

        List<PatternCore> patterns = new List<PatternCore>();
        public ReadOnlyCollection<IPattern> Patterns
        {
            get
            {
                return patterns.Cast<IPattern>().ToReadOnlyCollection();
            }
        }

        internal List<PatternCore> PatternsList { get => patterns; set => patterns = value; }

        public bool IsControlMachine { get; set; }

        bool isActive;
        public bool IsActive
        {
            get => isActive;
            set
            {
                if (isActive != value)
                {
                    isActive = value;
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        PropertyChanged.Raise(this, "IsActive");
                    });
                }
            }
        }

        bool muted;
        public bool IsMuted
        {
            get => muted;
            set
            {
                muted = value; PropertyChanged.Raise(this, "IsMuted");
                graph.Buzz.SetModifiedFlag();
            }
        }

        bool soloed;
        public bool IsSoloed
        {
            get => soloed;
            set
            {
                soloed = value;
                var buzz = graph.Buzz as ReBuzzCore;
                buzz.SongCore.UpdateSoloMode();
                PropertyChanged.Raise(this, "IsSoloed");
                buzz.SetModifiedFlag();
            }
        }

        bool bypassed;
        public bool IsBypassed
        {
            get => bypassed;
            set
            {
                bypassed = value; PropertyChanged.Raise(this, "IsBypassed");
                graph.Buzz.SetModifiedFlag();
            }
        }
        bool wireless;
        public bool IsWireless
        {
            get => wireless;
            set
            {
                wireless = value; PropertyChanged.Raise(this, "IsWireless");
                graph.Buzz.SetModifiedFlag();
            }
        }

        bool hasStereoInput;
        public bool HasStereoInput { get => hasStereoInput; set { hasStereoInput = value; PropertyChanged.Raise(this, "HasStereoInput"); } }

        bool hasStereoOutput;
        public bool HasStereoOutput { get => hasStereoOutput; set { hasStereoOutput = value; PropertyChanged.Raise(this, "HasStereoOutput"); } }

        int lastEngineThread = 0;
        public int LastEngineThread
        {
            get => lastEngineThread;
            set
            {
                int oldValue = lastEngineThread;
                int newValue = value % 7;
                lastEngineThread = newValue;

                if (MachineView.StaticSettings.ShowEngineThreads)
                {
                    if (newValue != oldValue)
                        //Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                        //{

                        PropertyChanged?.Raise(this, "LastEngineThread");
                    //}));
                }
            }
        }

        public int EngineThreadId { get; internal set; }

        internal void UpdateLastEngineThread()
        {
            LastEngineThread = EngineThreadId;
        }

        readonly int latency = 0;
        public int Latency { get => latency; }

        int overrideLatency = 0;
        public int OverrideLatency { get => overrideLatency; set => overrideLatency = value; }

        //IMachineDLL patternEditorDLL;
        public IMachineDLL PatternEditorDLL
        {
            get
            {
                if (EditorMachine != null)
                {
                    return EditorMachine.DLL;
                }

                return null;
            }
            /*
            set
            {
                if (value == null)
                    return;

                var buzz = Global.Buzz as ReBuzzCore;

                // Remove current editor machine
                buzz.RemoveMachine(EditorMachine);

                var MachineDll = value;
                EditorMachine = buzz.MachineManager.CreateMachine(MachineDll.Name, MachineDll.Path, null,
                    null, MachineDll.Info.MinTracks, 0, 0, false);
            }
            */
        }

        int baseOctave = 4;
        public int BaseOctave { get => baseOctave; set => baseOctave = value; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public byte[] Data
        {
            get => (graph.Buzz as ReBuzzCore).MachineManager.GetMachineData(this);
            set => (graph.Buzz as ReBuzzCore).MachineManager.SetMachineData(this, value);
        }

        public byte[] PatternEditorData
        {
            get
            {
                var buzz = graph.Buzz as ReBuzzCore;
                if (EditorMachine != null)
                {
                    return buzz.MachineManager.GetMachineData(EditorMachine);
                }
                else
                {
                    return null;
                }
            }
        }

        IntPtr cMachinePtr;
        public IntPtr CMachinePtr { get => cMachinePtr; set => cMachinePtr = value; }

        public Window ParameterWindow { get => parameterWindow; }

        readonly MachinePerformanceData performanceDataCurrent = new MachinePerformanceData();
        public MachinePerformanceData PerformanceDataCurrent { get => performanceDataCurrent; }

        MachinePerformanceData performanceData = new MachinePerformanceData();
        public MachinePerformanceData PerformanceData { get => performanceData; set => performanceData = value; }

        IBuzzMachine managedMachine;
        public IBuzzMachine ManagedMachine { get => managedMachine; set => managedMachine = value; }

        int trackCount = 0;
        public int TrackCount
        {
            get => trackCount;
            set
            {
                if (value < DLL.Info.MinTracks || value > DLL.Info.MaxTracks)
                {
                    return;
                }
                if (trackCount != value)
                {
                    //var bc = graph.Buzz as ReBuzzCore;
                    //SetMachineTrackCount(value);

                    trackCount = value;
                    if (parameterGroups.Count == 3)
                    {
                        parameterGroups[2].TrackCount = value;

                        if (graph != null)
                        {
                            var bc = graph.Buzz as ReBuzzCore;

                            // Notify native machines
                            bc.MachineManager.SetNumTracks(this, trackCount);
                            PropertyChanged?.Raise(this, "TrackCount");
                        }
                    }

                    graph.Buzz.SetModifiedFlag();
                }
            }
        }

        internal void SetTrackCount(int tracks)
        {
            trackCount = tracks;
            var bc = graph.Buzz as ReBuzzCore;

            // Notify native machines
            bc.MachineManager.SetNumTracks(this, trackCount);
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                PropertyChanged?.Raise(this, "TrackCount");
                bc.SetModifiedFlag();
            });
        }

        internal List<AttributeCore> AttributesList
        {
            get => attributes;
            set { attributes = value; PropertyChanged.Raise(this, "Attributes"); }
        }
        public bool Ready { get; internal set; }
        public long CMachineHost { get; internal set; }
        public List<CMachineEvent> CMachineEventType { get; internal set; }

        public event Action<IPattern> PatternAdded;
        public event Action<IPattern> PatternRemoved;
        public event PropertyChangedEventHandler PropertyChanged;

        public ICommand MachineMenuCommand { get; private set; }
        public string InstrumentName { get; internal set; }
        public bool Hidden { get; internal set; }
        public MachineCore EditorMachine { get; internal set; }

        public MachineCore(SongCore machineGraph, bool is64Bit = false)
        {
            graph = machineGraph;

            parameterGroups = new List<ParameterGroup>();
            parameterGroups.Add(ParameterGroup.CreateInputGroup(this)); // Inputs

            MachineDLL = new MachineDLL();
            MachineDLL.Is64Bit = is64Bit;
            MachineDLL.IsOutOfProcess = (IntPtr.Size == 4 && is64Bit) || (IntPtr.Size == 8 && !is64Bit);

            CMachineEventType = new List<CMachineEvent>();
            CMachineHost = machineHostId;
            machineHostId++;

            MachineMenuCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    //if (this.DLL.Info.Name.StartsWith("Polac") && (int)x == 2)
                    //{
                    //    if (MessageBox.Show("VST Midi Send is not working correctly and should be used only for development purposes. Are you sure?", "Warning", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    //        (Graph.Buzz as ReBuzzCore).MachineManager.Command(this, (int)x);
                    //}
                    //else
                    //{
                    (Graph.Buzz as ReBuzzCore).MachineManager.Command(this, (int)x);
                    //}
                }
            };
        }

        public void ClonePattern(string name, IPattern p)
        {
            if (p != null)
            {
                lock (ReBuzzCore.AudioLock)
                {
                    PatternCore newp = new PatternCore(this, name, p.Length);
                    this.patterns.Add(newp);
                    var buzz = graph.Buzz as ReBuzzCore;
                    buzz.MachineManager.CreatePatternCopy(EditorMachine, newp, p);
                    PatternAdded?.Invoke(newp);
                    PropertyChanged.Raise(this, "Patterns");
                    graph.Buzz.SetPatternEditorPattern(newp);
                    graph.Buzz.SetModifiedFlag();
                }
            }
        }

        public void CopyParameters()
        {

        }

        public void CreatePattern(string name, int length)
        {
            // Don't call these from "Work()"
            lock (ReBuzzCore.AudioLock)
            {
                PatternCore pc = new PatternCore(this, name, length);
                this.patterns.Add(pc);
                PatternAdded?.Invoke(pc);
                PropertyChanged.Raise(this, "Patterns");
                graph.Buzz.SetPatternEditorPattern(pc);
                graph.Buzz.SetModifiedFlag();
            }
        }

        public void DeletePattern(IPattern p)
        {
            lock (ReBuzzCore.AudioLock)
            {
                patterns.Remove(p as PatternCore);
                PatternRemoved?.Invoke(p);
                PropertyChanged.Raise(this, "Patterns");
                (p as PatternCore).ClearEvents();

                var buzz = graph.Buzz as ReBuzzCore;
                // Remove from sequences
                foreach (var seq in buzz.SongCore.SequencesList.Where(s => s.Machine == p.Machine))
                {
                    bool seqChanged = false;
                    foreach (var e in seq.Events.ToArray())
                    {
                        if (e.Value.Pattern == p)
                        {
                            seqChanged = true;
                            seq.EventsList.Remove(e.Key);
                        }
                    }
                    if (seqChanged)
                        seq.InvokeEvents();
                }
            }
        }

        public void RenamePattern(IPattern p, string newName)
        {
            //lock (ReBuzzCore.AudioLock)
            {
                if (p != null)
                {
                    (p as PatternCore).Name = newName;
                    PropertyChanged?.Raise(this, "Patterns");

                    foreach (var sequence in Global.Buzz.Song.Sequences)
                    {
                        if (sequence.Events.Values.FirstOrDefault(e => e.Pattern == p) != null)
                        {
                            (sequence as SequenceCore).InvokeEvents();
                        }
                    }
                }
            }
        }

        public void DoubleClick()
        {
            if (DLL.IsCrashed)
                return;

            if (!DLL.IsManaged)
            {
                var me = CMachineEventType.FirstOrDefault(e => e.Type == BEventType.DoubleClickMachine);
                if (me.Event_Handler != IntPtr.Zero)
                {
                    // Let native machine handle double click
                    var buzzCore = (ReBuzzCore)graph.Buzz;

                    bool handled = buzzCore.MachineManager.MachineEvent(this, me, 0);
                    if (!handled)
                    {
                        // Open parameter windows
                        OpenParameterWindow();
                    }
                }
                else
                {
                    if (DLL.GUIFactoryDecl != null && DLL.GUIFactoryDecl.PreferWindowedGUI)
                    {
                        OpenWindowedGUI();
                    }
                    else
                    {
                        OpenParameterWindow();
                    }
                }
            }
            else
            {
                if (DLL.GUIFactoryDecl != null && DLL.GUIFactoryDecl.PreferWindowedGUI)
                {
                    OpenWindowedGUI();
                }
                else
                {
                    OpenParameterWindow();
                }
            }
        }

        internal MachineGUIHostWindow MachineGUIWindow { get => machineGUIWindow; }
        MachineGUIHostWindow machineGUIWindow;
        Window parameterWindow;

        internal void OpenParameterWindow(Rect rect = default)
        {
            if (parameterWindow == null)
            {
                ParameterWindowVM pWindowVM = new ParameterWindowVM();
                pWindowVM.Machine = this;

                parameterWindow = Utils.GetUserControlXAML<Window>("ParameterWindow.xaml");
                Window window = (Window)HwndSource.FromHwnd(graph.Buzz.MachineViewHWND).RootVisual;
                parameterWindow.Owner = window;

                parameterWindow.DataContext = pWindowVM;
                parameterWindow.Closing += (s, e) =>
                {
                    if (parameterWindow != null)
                    {
                        parameterWindow.Hide();
                    }
                    e.Cancel = true;
                };
            }

            if (rect.Width != 0)
            {
                parameterWindow.Left = rect.Left;
                parameterWindow.Top = rect.Top;
                parameterWindow.Width = rect.Width;
                parameterWindow.Height = rect.Height;
            }
            parameterWindow.Show();

        }

        internal void OpenWindowedGUI(Rect rect = default)
        {
            if (!(DLL.GUIFactoryDecl != null && DLL.GUIFactoryDecl.PreferWindowedGUI))
            {
                return;
            }

            if (!DLL.IsManaged)
            {
                try
                {
                    if (machineGUIWindow == null)
                    {
                        machineGUIWindow = new MachineGUIHostWindow();

                        machineGUIWindow.SizeToContent = SizeToContent.WidthAndHeight;
                        var gui = DLL.GUIFactory.CreateGUI(machineGUIWindow);
                        var guiUIElement = gui as UserControl;
                        Viewbox vb = new Viewbox();
                        vb.Stretch = System.Windows.Media.Stretch.Fill;
                        vb.Child = guiUIElement;
                        //TextOptions.SetTextFormattingMode(window, TextFormattingMode.Ideal);
                        //window.Content = gui;
                        machineGUIWindow.Content = vb;
                        machineGUIWindow.Title = Name;

                        double scale = 1.0;
                        var interop = new WindowInteropHelper(machineGUIWindow);
                        interop.Owner = Graph.Buzz.MachineViewHWND;

                        machineGUIWindow.ResizeMode = DLL.GUIFactoryDecl.IsGUIResizable ? ResizeMode.CanResize : ResizeMode.NoResize;

                        guiUIElement.SizeChanged += (sender, e) =>
                        {
                            vb.Width = guiUIElement.ActualWidth * scale;
                            vb.Height = guiUIElement.ActualHeight * scale;

                            machineGUIWindow.Width = vb.Width;
                            machineGUIWindow.Height = vb.Height;
                        };

                        machineGUIWindow.Loaded += (sender, e) =>
                        {
                            if (DLL.GUIFactoryDecl.UseThemeStyles)
                            {
                                var r = Utils.GetBuzzThemeResources("ParameterWindow.xaml");
                                machineGUIWindow.Resources.MergedDictionaries.Add(r);
                            }
                        };

                        machineGUIWindow.SizeChanged += (sender, e) =>
                        {
                            machineGUIWindow.Width = vb.Width + SystemParameters.VerticalScrollBarWidth;
                            machineGUIWindow.Height = vb.Height + SystemParameters.HorizontalScrollBarHeight + SystemParameters.ResizeFrameHorizontalBorderHeight;
                        };

                        machineGUIWindow.PreviewMouseWheel += (sender, e) =>
                        {
                            if (Keyboard.Modifiers == ModifierKeys.Control)
                            {
                                scale += e.Delta / 600.0;
                                if (scale < 0.5)
                                    scale = 0.5;

                                vb.Width = guiUIElement.ActualWidth * scale;
                                vb.Height = guiUIElement.ActualHeight * scale;

                                machineGUIWindow.Width = vb.Width;
                                machineGUIWindow.Height = vb.Height;

                                e.Handled = true;
                            }
                        };

                        machineGUIWindow.Closing += (s, e) =>
                        {
                            machineGUIWindow.Hide();
                            e.Cancel = true;
                        };

                        gui.Machine = this;
                    }
                }
                catch (Exception ex)
                {
                    Utils.MessageBox(ex.ToString());
                }
            }
            else
            {
                try
                {
                    if (machineGUIWindow == null)
                    {
                        machineGUIWindow = new MachineGUIHostWindow();

                        machineGUIWindow.SizeToContent = SizeToContent.WidthAndHeight;
                        machineGUIWindow.Title = Name;
                        var gui = DLL.GUIFactory.CreateGUI(machineGUIWindow);
                        var guiUIElement = gui as UserControl;
                        machineGUIWindow.Content = guiUIElement;
                        machineGUIWindow.ResizeMode = DLL.GUIFactoryDecl.IsGUIResizable ? ResizeMode.CanResize : ResizeMode.NoResize;

                        var interop = new WindowInteropHelper(machineGUIWindow);
                        interop.Owner = Graph.Buzz.MachineViewHWND;

                        gui.Machine = this;

                        machineGUIWindow.Closing += (s, e) =>
                        {
                            machineGUIWindow.Hide();
                            e.Cancel = true;
                        };

                        machineGUIWindow.Loaded += (s, e) =>
                        {
                            if (DLL.GUIFactoryDecl.UseThemeStyles)
                            {
                                var r = Utils.GetUserControlXAML<Window>("ParameterWindow.xaml");
                                machineGUIWindow.Resources.MergedDictionaries.Add(r.Resources);
                            }
                        };
                    }
                }
                catch (Exception ex)
                {
                    Utils.MessageBox(ex.ToString());
                }
            }

            if (rect.Width != 0)
            {
                machineGUIWindow.Left = rect.Left;
                machineGUIWindow.Top = rect.Top;
                machineGUIWindow.Width = rect.Width;
                machineGUIWindow.Height = rect.Height;
            }

            try
            {
                if (machineGUIWindow != null)
                    machineGUIWindow.Show();
            }
            catch (Exception ex)
            {
                Utils.MessageBox(ex.ToString());
            }
        }

        internal void CloseWindows()
        {
            if (parameterWindow != null)
            {
                parameterWindow.Close();
                parameterWindow = null;
            }

            if (machineGUIWindow != null)
            {
                machineGUIWindow.Close();
                machineGUIWindow = null;
            }
        }

        public void ExecuteCommand(int id)
        {

        }

        // MULTI IO
        public string GetChannelName(bool input, int index)
        {
            string ret = "";
            if (input)
            {
                if (inputChannelNames.ContainsKey(index))
                {
                    ret = inputChannelNames[index];
                }
            }
            else if (outputChannelNames.ContainsKey(index))
            {
                ret = outputChannelNames[index];
            }
            return ret;
        }

        internal bool sendControlChangesFlag;

        public bool IsSeqThru { get; internal set; }
        public bool IsSeqMute { get; internal set; }


        internal bool workDone;

        public void SendControlChanges()
        {
            //var machineManager = (graph.Buzz as ReBuzzCore).MachineManager;

            // Control machines !MUST! send the changes immediately before Work() so that events get triggered correctly
            //machineManager.Tick(this);
            sendControlChangesFlag = true;
        }

        public byte[] SendGUIMessage(byte[] message)
        {
            var buzz = graph.Buzz as ReBuzzCore;
            return buzz.MachineManager.SendGUIMessage(this, message);
        }

        public void SendMIDIControlChange(int ctrl, int channel, int value)
        {
            if (Ready)
            {
                var buzz = graph.Buzz as ReBuzzCore;
                buzz.MachineManager.SendMIDIControlChange(this, ctrl, channel, value);
            }
        }

        public void SendMIDINote(int channel, int value, int velocity)
        {
            if (Ready)
            {
                var buzz = graph.Buzz as ReBuzzCore;
                buzz.MachineManager.SendMIDINote(this, channel, value, velocity);
            }
        }

        public void ShowContextMenu(int x, int y)
        {
        }

        public void ShowDialog(MachineDialog d, int x, int y)
        {
            switch (d)
            {
                case MachineDialog.Parameters:
                    OpenParameterWindow();
                    break;
                case MachineDialog.Patterns:
                    if (PatternsList.Count > 0)
                    {
                        var buzz = graph.Buzz as ReBuzzCore;
                        buzz.SetPatternEditorPattern(PatternsList[0]);
                        buzz.ActiveView = BuzzView.PatternView;
                    }
                    break;
                case MachineDialog.Attributes:
                    OpenParameterWindow();
                    break;
                case MachineDialog.Rename:

                    var renameWindow = new RenameMachineWindow(name);
                    var rd = Utils.GetUserControlXAML<ResourceDictionary>("MachineView\\MVResources.xaml");
                    renameWindow.Resources.MergedDictionaries.Add(rd);
                    if (renameWindow.ShowDialog() == true)
                    {
                        var newName = renameWindow.tbName.Text.Trim();
                        (graph.Buzz as ReBuzzCore).RenameMachine(this, newName);
                    }
                    break;
                case MachineDialog.Delay:
                    break;
            }
        }

        public void ShowHelp()
        {
            string[] extensions = new string[] { ".html", ".htm", ".pdf", ".txt" };

            foreach (var ext in extensions)
            {
                string helpFile = DLL.Path.Substring(0, DLL.Path.Length - 4) + ext;
                if (File.Exists(helpFile))
                {
                    var ps = new ProcessStartInfo(helpFile)
                    {
                        UseShellExecute = true,
                        Verb = "open"
                    };
                    Process.Start(ps);
                    return;
                }
                if (DLL.Path.ToLower().EndsWith(".x64.dll"))
                {
                    helpFile = DLL.Path.Substring(0, DLL.Path.Length - 8) + ext;
                    if (File.Exists(helpFile))
                    {

                        var ps = new ProcessStartInfo(helpFile)
                        {
                            UseShellExecute = true,
                            Verb = "open"
                        };
                        Process.Start(ps);
                        return;
                    }
                }
            }
        }

        public void ShowPresetEditor()
        {
            OpenParameterWindow();
        }

        public void UnbindAllMIDIControllers()
        {
            var buzz = graph.Buzz as ReBuzzCore;
            buzz.MidiControllerAssignments.UnbindAllMIDIControllers(this);
        }

        internal void AddParameterGroup(ParameterGroup group)
        {
            parameterGroups.Add(group);
        }

        internal void AddOutput(MachineConnectionCore mcc)
        {
            this.outputs.Add(mcc);

        }

        internal void AddInput(MachineConnectionCore mcc)
        {
            this.inputs.Add(mcc);
            var buzz = graph.Buzz as ReBuzzCore;
            //if (mcc.Source.DLL.Info.Flags.HasFlag(MachineInfoFlags.DOES_INPUT_MIXING))
            buzz.MachineManager.AddInput(this, mcc.Source, true);
        }

        internal void ResetParametersToNoValue()
        {
            foreach (ParameterCore p in ParameterGroupsList[1].Parameters)
            {
                p.SetValue(0, p.NoValue);
            }

            // Tracks
            for (int i = 0; i < TrackCount; i++)
            {
                foreach (ParameterCore p in ParameterGroupsList[2].Parameters)
                {
                    p.SetValue(i, p.NoValue);
                }
            }
        }

        internal void SetParametersToDefaulValue()
        {
            foreach (ParameterCore p in ParameterGroupsList[0].Parameters)
            {
                if (p.Flags.HasFlag(ParameterFlags.State))
                {
                    p.SetValue(0, p.DefValue);
                }
                else
                {
                    p.SetValue(0, p.NoValue);
                }
            }

            foreach (ParameterCore p in ParameterGroupsList[1].Parameters)
            {
                if (p.Flags.HasFlag(ParameterFlags.State))
                {
                    p.SetValue(0, p.DefValue);
                }
                else
                {
                    p.SetValue(0, p.NoValue);
                }
            }

            // Tracks
            for (int i = 0; i < TrackCount; i++)
            {
                foreach (ParameterCore p in ParameterGroupsList[2].Parameters)
                {
                    if (p.Flags.HasFlag(ParameterFlags.State))
                    {
                        p.SetValue(i, p.DefValue);
                    }
                    else
                    {
                        p.SetValue(i, p.NoValue);
                    }
                }
            }
        }

        internal void RemoveOutput(IMachineConnection mc)
        {
            outputs.Remove(mc);
        }
        internal void RemoveInput(IMachineConnection mc)
        {
            if (inputs.Remove(mc) == true && parameterGroups[0].TrackCount > 0)
            {
                parameterGroups[0].TrackCount--;
            }

            var buzz = graph.Buzz as ReBuzzCore;
            buzz.MachineManager.DeleteInput(this, mc.Source);
        }

        // Managed
        internal void SetCommands()
        {
            var buzz = graph.Buzz as ReBuzzCore;
            var cmds = buzz.MachineManager.GetCommands(this);
            if (cmds != null)
                commands = cmds.ToList();
        }

        // Native
        internal void SetCommands(string commandsStr)
        {
            if (commandsStr == "")
                return;

            // Clear menu tree
            this.commands.Clear();
            var comnds = commandsStr.Split('\n');
            for (int i = 0; i < comnds.Length; i++)
            {
                var cmd = comnds[i].Trim();
                int c_index = cmd.IndexOf("&");
                if (c_index >= 0)
                {
                    cmd = cmd.Remove(c_index, 1).Insert(c_index, "_");
                }
                /*
                else if (cmd.StartsWith("/"))
                {
                    //string subMenu = buzz.MachineManager.GetSubMenu(this, i);
                }
                */

                this.commands.Add(new MenuItemCore() { Command = MachineMenuCommand, CommandParameter = i, Text = cmd.Trim(), IsEnabled = true });
            }
            CreateSubMenus();
        }

        internal void CreateSubMenus()
        {
            // Submenu 0: CommandParameter == 256..511
            // Submenu 1: CommandParameter == 512..767
            // ...
            int index = 256;
            foreach (var cmd in commands)
            {
                var command = cmd as MenuItemCore;
                if (command.Text.StartsWith("/"))
                {
                    command.Text = command.Text.Remove(0, 1);
                    PopulateSubMenu(command, index);
                    index += 256;
                }
            }
        }

        private void PopulateSubMenu(MenuItemCore rootSubmenuItem, int index)
        {
            var buzz = graph.Buzz as ReBuzzCore;

            // Get submenu commands.
            string[] comnds = buzz.MachineManager.GetSubMenu(this, (int)rootSubmenuItem.CommandParameter);
            for (int i = 0; i < comnds.Length; i++)
            {
                var stringMenuCommandPath = comnds[i].Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                var subMenuItem = rootSubmenuItem;

                // Create sub sub menus if needed and find the target menu item. subMenuItem will be the menu item for command.
                for (int j = 0; j < stringMenuCommandPath.Length - 1; j++)
                {
                    string text = stringMenuCommandPath[j].Trim();

                    var foundItem = subMenuItem.ChildrenList.FirstOrDefault(item => item.Text == text);
                    if (foundItem != null)
                    {
                        subMenuItem = foundItem;
                    }
                    else
                    {
                        var menuCommand = new MenuItemCore() { Command = MachineMenuCommand, Text = stringMenuCommandPath[j].Trim(), IsEnabled = true };
                        subMenuItem.ChildrenList.Add(menuCommand);
                        subMenuItem = menuCommand;
                    }
                }
                // The actual command
                string name = stringMenuCommandPath.Last().Trim();

                MenuItemCore finalMenuCommand = null;
                /*
                var di = buzz.MachineDB.DictLibRef.FirstOrDefault(x => x.Value.InstrumentFullName == comnds[i]);
                if (di.Value.InstrumentFullName != null)
                {
                    int id = di.Key;
                    finalMenuCommand = new MenuItemCore() { Command = MachineMenuPluginCommand, CommandParameter = id, Text = name, IsEnabled = true };
                }
                else
                */
                {
                    // Is the CommandParameter correct?
                    finalMenuCommand = new MenuItemCore() { Command = MachineMenuCommand, CommandParameter = index, Text = name, IsEnabled = true };
                }
                index++;
                subMenuItem.ChildrenList.Add(finalMenuCommand);
            }
        }

        readonly Sample[] stereoSamples = new Sample[256];
        internal Sample[] GetStereoSamples(int nSamples)
        {
            for (int i = 0; i < stereoSamples.Length; i++)
                stereoSamples[i].L = stereoSamples[i].R = 0;

            foreach (var input in Inputs)
            {
                var inputCore = input as MachineConnectionCore;

                for (int i = 0; i < nSamples; i++)
                {
                    stereoSamples[i].L += inputCore.Buffer[i].L;
                    stereoSamples[i].R += inputCore.Buffer[i].R;
                }
            }

            //Utils.FlushDenormalToZero(samples);
            return stereoSamples;
        }

        internal void UpdateOutputs(Sample[] samples, bool denormal = true)
        {
            foreach (var output in Outputs)
            {
                var outputCore = output as MachineConnectionCore;
                if (denormal)
                {
                    Utils.FlushDenormalToZero(samples);
                }
                outputCore.UpdateBuffer(samples);
            }
        }

        readonly List<Sample[]> channels = new List<Sample[]>();
        internal List<Sample[]> GetMultiIOSamples(int nSamples)
        {
            // Check if enough channels to mix input destinations
            if (channels.Count < InputChannelCount)
            {
                channels.Clear();
                for (int i = 0; i < InputChannelCount; i++)
                {
                    channels.Add(null);
                }
            }
            else
            {
                // Clear
                for (int i = 0; i < InputChannelCount; i++)
                {
                    var ci = channels[i];
                    if (ci != null)
                    {
                        for (int j = 0; j < ci.Length; j++)
                        {
                            ci[j] = 0;
                        }
                    }
                }
            }

            foreach (var input in Inputs)
            {
                var inputCore = input as MachineConnectionCore;

                //if (inputCore.Source.IsActive)
                if (inputCore.DestinationChannel < channels.Count)
                {
                    if (channels[inputCore.DestinationChannel] == null)
                    {
                        channels[inputCore.DestinationChannel] = new Sample[256];
                    }

                    Sample[] samples = channels[inputCore.DestinationChannel];

                    for (int i = 0; i < samples.Length; i++)
                    {
                        samples[i].L += inputCore.Buffer[i].L;
                        samples[i].R += inputCore.Buffer[i].R;
                    }
                }
            }

            return channels;
        }

        internal void UpdateOutputs(List<Sample[]> multiSamplesOut)
        {
            foreach (var output in Outputs)
            {
                var outputCore = output as MachineConnectionCore;
                if (outputCore.SourceChannel < multiSamplesOut.Count &&
                    multiSamplesOut[outputCore.SourceChannel] != null)
                {
                    Sample[] samples = multiSamplesOut[outputCore.SourceChannel];
                    Utils.FlushDenormalToZero(samples);
                    outputCore.UpdateBuffer(samples);
                }
            }
        }

        private readonly float AMP_EPS = 1.0f;
        public bool GetActivity()
        {
            bool isActive = false;

            float maxSample = 0.0f;

            var connections = Name == "Master" ? Inputs : Outputs;

            foreach (var output in connections)
            {
                var outputCore = output as MachineConnectionCore;
                Sample[] samples = outputCore.Buffer;
                for (int i = 0; i < samples.Length; i++)
                {
                    maxSample = Math.Max(Math.Abs(samples[i].L), maxSample);
                    maxSample = Math.Max(Math.Abs(samples[i].R), maxSample);
                }
            }

            if (maxSample > AMP_EPS)
                isActive = true;

            return isActive;
        }

        internal List<Task> workTasks = new List<Task>(50);

        internal List<int> setMachineTrackCountList = new List<int>();

        internal void SetMachineTrackCount(int trackCount)
        {
            setMachineTrackCountList.Add(trackCount);
        }

        internal void RaiseTrackCount()
        {
            PropertyChanged?.Raise(this, "TrackCount");
        }

        internal void ClearEvents()
        {
            PropertyChanged = null;
            PatternAdded = null;
            PatternRemoved = null;
            foreach (var pg in parameterGroups)
            {
                foreach (ParameterCore p in pg.ParametersList)
                {
                    p.ClearEvents();
                }
            }
        }
    }
}