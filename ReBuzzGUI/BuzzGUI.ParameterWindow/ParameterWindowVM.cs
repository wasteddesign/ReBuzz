using BuzzGUI.Common;
using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.Common.Presets;
using BuzzGUI.Interfaces;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml;

namespace BuzzGUI.ParameterWindow
{
    public class ParameterWindowVM : INotifyPropertyChanged, IMachineGUIHost
    {
        public static ParameterWindowSettings Settings = new ParameterWindowSettings();

        public static void Init()
        {
            BuzzGUI.Common.SettingsWindow.AddSettings("Parameter Window", Settings);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public IMachineGUI MachineGUI { get; private set; }

        IMachine machine;
        public IMachine Machine
        {
            get { return machine; }
            set
            {
                if (machine != null)
                {
                    if (MachineGUI != null)
                    {
                        MachineGUI.Machine = null;
                        MachineGUI = null;
                        PropertyChanged.Raise(this, "EmbeddedGUI");
                    }

                    machine.PropertyChanged -= new PropertyChangedEventHandler(MachinePropertyChanged);

                    foreach (var p in Parameters)
                        p.Release();

                    Parameters = null;
                    PropertyChanged.Raise(this, "Parameters");
                }

                machine = value;

                if (machine != null)
                {
                    machine.PropertyChanged += new PropertyChangedEventHandler(MachinePropertyChanged);

                    if (machine.DLL.GUIFactory != null && (machine.DLL.GUIFactoryDecl == null || !machine.DLL.GUIFactoryDecl.PreferWindowedGUI))
                    {
                        MachineGUI = machine.DLL.GUIFactory.CreateGUI(this);
                        MachineGUI.Machine = machine;
                        PropertyChanged.Raise(this, "EmbeddedGUI");
                    }

                    CreateParameters();
                }
            }
        }

        void MachinePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "TrackCount")
                CreateParameters();
            else if (e.PropertyName == "Presets")
                PropertyChanged.Raise(this, e.PropertyName);
            else if (e.PropertyName == "Attributes")
            {
                foreach (ParameterVM pvm in Parameters) PropertyChanged.Raise(pvm, "Attributes");
            }
            else if (e.PropertyName == "Name")
            {
                PropertyChanged.Raise(this, "MachineName");
            }

        }

        bool IsParameterVisible(IParameter p)
        {
            var md = MachineGUI as IMachineMetadata;
            if (md == null) return true;
            IParameterMetadata pmd;
            return md.ParameterMetadata.TryGetValue(p, out pmd) ? pmd.IsVisible : true;
        }

        IParameterMetadata GetParameterMetadata(IParameter p)
        {
            var md = MachineGUI as IMachineMetadata;
            if (md == null) return null;
            IParameterMetadata pmd = null;
            md.ParameterMetadata.TryGetValue(p, out pmd);
            return pmd;
        }

        void CreateParameters()
        {
            if (Parameters != null)
            {
                foreach (var p in Parameters)
                    p.Release();
            }

            // For ParamEditors
            if (InputParameters != null)
            {
                foreach (var p in InputParameters)
                    p.Release();
            }

            if (GlobalParameters != null)
            {
                foreach (var p in GlobalParameters)
                    p.Release();
            }

            var md = MachineGUI as IMachineMetadata;

            Parameters = Machine.AllParametersAndTracks()
                .Where(pt => pt.Item1.Flags.HasFlag(ParameterFlags.State) && IsParameterVisible(pt.Item1))
                .Select(pt => new ParameterVM(this, pt.Item1, pt.Item2, GetParameterMetadata(pt.Item1))).ToReadOnlyCollection();

            InputParameters = Machine.ParameterGroups[0].Parameters.Select(pt => new ParameterVM(this, pt, 0, GetParameterMetadata(pt))).ToReadOnlyCollection();
            GlobalParameters = Machine.ParameterGroups[1].Parameters.Select(pt => new ParameterVM(this, pt, 0, GetParameterMetadata(pt))).ToReadOnlyCollection();

            PropertyChanged.Raise(this, "Parameters");
        }

        public SimpleCommand PresetsCommand { get; private set; }
        public SimpleCommand UndoCommand { get; private set; }
        public SimpleCommand RedoCommand { get; private set; }
        public SimpleCommand EditCommand { get; private set; }
        public SimpleCommand HelpCommand { get; private set; }
        public SimpleCommand SelectPresetCommand { get; private set; }

        public SimpleCommand CopyPresetCommand { get; private set; }
        public SimpleCommand PastePresetCommand { get; private set; }
        public SimpleCommand UnlockAllCommand { get; private set; }
        public SimpleCommand InvertLocksCommand { get; private set; }
        public SimpleCommand PresetEditorAddCommand { get; private set; }
        public SimpleCommand PresetEditorDeleteCommand { get; private set; }
        public SimpleCommand PresetEditorImportCommand { get; private set; }
        public SimpleCommand ApplyCommand { get; private set; }
        public SimpleCommand ActivatedCommand { get; private set; }
        public SimpleCommand DeactivatedCommand { get; private set; }
        public SimpleCommand PreviewKeyDownCommand { get; private set; }
        public SimpleCommand PreviewKeyUpCommand { get; private set; }
        public SimpleCommand PreviewTextInputCommand { get; private set; }
        public SimpleCommand SettingsCommand { get; private set; }
        public SimpleCommand AddTrackCommand { get; private set; }
        public SimpleCommand DeleteLastTrackCommand { get; private set; }

        readonly EditContext editContext;
        PresetsWindow presetsWindow;

        string clipboardText;
        Preset clipboardPreset;

        readonly Dictionary<int, int> keysDown = new Dictionary<int, int>();
        readonly int velocity = 100;


        public ParameterWindowVM()
        {
            editContext = new EditContext(this);

            PresetsCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    presetsWindow = new PresetsWindow()
                    {
                        DataContext = this,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };
                    presetsWindow.ShowDialog();
                }
            };

            UndoCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => editContext.ManagedActionStack.CanUndo,
                ExecuteDelegate = x => { editContext.ManagedActionStack.Undo(); }
            };

            RedoCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => editContext.ManagedActionStack.CanRedo,
                ExecuteDelegate = x => { editContext.ManagedActionStack.Redo(); }
            };

            HelpCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x => { machine.ShowHelp(); }
            };

            EditCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    if (EditGridVisibility == Visibility.Collapsed)
                    {
                        EditGridVisibility = Visibility.Visible;
                        EditButtonText = "Edit <<";
                    }
                    else
                    {
                        EditGridVisibility = Visibility.Collapsed;
                        EditButtonText = "Edit >>";
                    }

                    foreach (var p in Parameters)
                        PropertyChanged.Raise(p, "LockButtonVisibility");
                }
            };

            SelectPresetCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = name =>
                {
                    // FIXME: this is called with null preset when the window is closed
                    if (name != null)
                    {
                        var preset = machine.GetPreset(name as string);
                        DoAction(new Actions.SelectPresetAction(this, preset));

                    }
                }
            };

            CopyPresetCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = format =>
                {
                    var preset = new Preset(machine, false, true);
                    if ((string)format == "XML")
                        ClipboardEx.SetText(preset.ToXml());
                    else
                        ClipboardEx.SetText(preset.ToString());
                }
            };

            PastePresetCommand = new SimpleCommand
            {
                CanExecuteDelegate = x =>
                {
                    string text = ClipboardEx.GetText();
                    if (text == null) return false;
                    if (text != clipboardText)
                    {
                        clipboardText = text;

                        try
                        {
                            clipboardPreset = null;
                            clipboardPreset = Preset.FromXml(text);
                            if (clipboardPreset == null) clipboardPreset = new Preset(machine, text);
                            if (clipboardPreset != null && clipboardPreset.Machine != machine.DLL.Name) clipboardPreset = null;
                        }
                        catch (Exception) { return false; }
                    }
                    return clipboardPreset != null;
                },
                ExecuteDelegate = _x =>
                {
                    try
                    {
                        if (clipboardPreset != null)
                            clipboardPreset.Apply(machine, true);
                    }
                    catch (Exception) { }
                }
            };

            UnlockAllCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => EditGridVisibility == Visibility.Visible,
                ExecuteDelegate = x => { foreach (var p in Parameters) if (p.Parameter.Group.Type != ParameterGroupType.Input) p.IsLocked = false; }
            };

            InvertLocksCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => EditGridVisibility == Visibility.Visible,
                ExecuteDelegate = x => { foreach (var p in Parameters) if (p.Parameter.Group.Type != ParameterGroupType.Input) p.IsLocked ^= true; }
            };

            ApplyCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x => { DoAction(new Actions.ApplyEditFunctionAction(this, false)); }
            };

            PresetEditorAddCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => PresetEditorPresetName != null && PresetEditorPresetName.Length > 0,
                ExecuteDelegate = x =>
                {
                    var preset = new Preset(Machine, false, true) { Comment = presetEditorComment };
                    Machine.SetPreset(presetEditorPresetName, preset);
                    PropertyChanged.Raise(this, "Presets");
                    PropertyChanged.Raise(this, "PresetsWithDefault");

                    presetsWindow.Close();
                }
            };

            PresetEditorDeleteCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => PresetEditorSelectedValue != null && PresetEditorSelectedValue.Length > 0,
                ExecuteDelegate = x =>
                {
                    Machine.SetPreset(presetEditorPresetName, null);
                    PropertyChanged.Raise(this, "Presets");
                    PropertyChanged.Raise(this, "PresetsWithDefault");
                }
            };

            PresetEditorImportCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    var ofd = new OpenFileDialog()
                    {
                        Filter = "Preset Files|*.prs.xml|Old Preset Files|*.prs",
                        Multiselect = true
                    };

                    if ((bool)ofd.ShowDialog())
                    {
                        foreach (var fn in ofd.FileNames) Machine.ImportPresets(fn);

                        PropertyChanged.Raise(this, "Presets");
                        PropertyChanged.Raise(this, "PresetsWithDefault");

                    }

                }
            };

            ActivatedCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    if (Machine != null && (Machine.DLL.Info.Type == MachineType.Generator || Machine.IsControlMachine))
                        Global.Buzz.MIDIFocusMachine = Machine;

                    //Global.Buzz.EditContext = editContext;
                }
            };

            DeactivatedCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {

                    //if (Global.Buzz.EditContext == editContext)	Global.Buzz.EditContext = null;

                    foreach (var k in keysDown)
                        machine.Graph.Buzz.SendMIDIInput(MIDI.Encode(MIDI.NoteOff, k.Value, 0));

                    keysDown.Clear();

                }
            };

            PreviewKeyDownCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    if (!Settings.KeyboardMIDI || Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase) return;

                    var e = x as KeyEventArgs;

                    if (e.KeyboardDevice.Modifiers == ModifierKeys.None && Machine.DLL.Info.Type == MachineType.Generator)
                    {
                        int i = PianoKeys.GetPianoKeyIndex(e);

                        if (i != -1 && !e.IsRepeat && !keysDown.ContainsKey(i))
                        {
                            int k = 12 * machine.BaseOctave + i;

                            machine.Graph.Buzz.SendMIDIInput(MIDI.Encode(MIDI.NoteOn, k, velocity));

                            keysDown[i] = k;
                        }

                        if (i != -1) e.Handled = true;

                    }

                }
            };

            PreviewKeyUpCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    var e = x as KeyEventArgs;

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
                                k = 12 * machine.BaseOctave + i;
                            }

                            machine.Graph.Buzz.SendMIDIInput(MIDI.Encode(MIDI.NoteOff, k, 0));

                            e.Handled = true;
                        }
                    }

                }
            };

            PreviewTextInputCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    var e = x as TextCompositionEventArgs;

                    switch (e.Text)
                    {
                        case "*":
                            if (machine.BaseOctave < 9) machine.BaseOctave++;
                            e.Handled = true;
                            break;

                        case "/":
                            if (machine.BaseOctave > 0) machine.BaseOctave--;
                            e.Handled = true;
                            break;
                    }

                }
            };

            SettingsCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x => { BuzzGUI.Common.SettingsWindow.Show(null, "Parameter Window"); }
            };

            AddTrackCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => machine != null && machine.ParameterGroups[2].TrackCount < machine.DLL.Info.MaxTracks,
                ExecuteDelegate = x => { machine.ParameterGroups[2].TrackCount++; }
            };

            DeleteLastTrackCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => machine != null && machine.ParameterGroups[2].TrackCount > machine.DLL.Info.MinTracks,
                ExecuteDelegate = x => { machine.ParameterGroups[2].TrackCount--; }
            };

        }

        public void Release()
        {
        }


        public string MachineInfoName { get { return machine.DLL.Info.Name; } }
        public string MachineName { get { return machine.Name; } }

        public ReadOnlyCollection<ParameterVM> Parameters { get; private set; }
        public ReadOnlyCollection<ParameterVM> InputParameters { get; private set; }
        public ReadOnlyCollection<ParameterVM> GlobalParameters { get; private set; }

        public IEnumerable<string> Presets { get { return Machine.GetPresetNames(); } }
        public IEnumerable<string> PresetsWithDefault { get { return Enumerable.Concat(Enumerable.Repeat("<default>", 1), Presets); } }

        public IIndex<string, Color> ThemeColors { get { return machine.DLL.Buzz.ThemeColors; } }

        public FrameworkElement EmbeddedGUI { get { return MachineGUI as FrameworkElement; } }

        Visibility editGridVisibility = Visibility.Collapsed;
        public Visibility EditGridVisibility
        {
            get { return editGridVisibility; }
            set
            {
                editGridVisibility = value;
                PropertyChanged.Raise(this, "EditGridVisibility");
            }
        }

        string editButtonText = "Edit >>";
        public string EditButtonText
        {
            get { return editButtonText; }
            set
            {
                editButtonText = value;
                PropertyChanged.Raise(this, "EditButtonText");
            }
        }

        #region PresetEditor
        string presetEditorSelectedValue;
        public string PresetEditorSelectedValue
        {
            get { return presetEditorSelectedValue; }
            set
            {
                presetEditorSelectedValue = value;
                PropertyChanged.Raise(this, "PresetEditorSelectedValue");

                PresetEditorPresetName = value;
                PresetEditorComment = PresetEditorSelectedValue != null ? machine.GetPreset(PresetEditorSelectedValue).Comment : null;
            }
        }

        string presetEditorPresetName;
        public string PresetEditorPresetName
        {
            get { return presetEditorPresetName; }
            set
            {
                presetEditorPresetName = value;
                PropertyChanged.Raise(this, "PresetEditorPresetName");
                PropertyChanged.Raise(this, "PresetEditorButtonText");
                PropertyChanged.Raise(this, "PresetEditorAddCommand");
            }
        }


        public string PresetEditorButtonText { get { return machine.GetPresetNames().Contains(PresetEditorPresetName) ? "Update" : "Add"; } }

        string presetEditorComment;
        public string PresetEditorComment
        {
            get { return presetEditorComment; }
            set
            {
                presetEditorComment = value;
                PropertyChanged.Raise(this, "PresetEditorComment");
            }
        }


        #endregion

        public bool HasAttributes { get { return machine.Attributes.Count > 0; } }

        public enum EditFunctions { RandomizeAbsolute, RandomizeRelative, RandomizeMaybe, RandomizeAB, RandomizeAorB, MorphAB };

        EditFunctions editFunction = EditFunctions.RandomizeAbsolute;
        public EditFunctions EditFunction { get { return editFunction; } }

        public int EditFunctionIndex
        {
            get { return (int)editFunction; }
            set
            {
                editFunction = (EditFunctions)value;
                PropertyChanged.Raise(this, "EditFunctionIndex");
                PropertyChanged.Raise(this, "IsEditFunctionParameterEnabled");
                PropertyChanged.Raise(this, "IsPresetBEnabled");
            }
        }

        double editFunctionParameter = 0.32;
        public double EditFunctionParameter
        {
            get { return editFunctionParameter; }
            set
            {
                editFunctionParameter = value;
                PropertyChanged.Raise(this, "EditFunctionParameter");

                if (EditGridVisibility == Visibility.Visible)
                {
                    if (EditFunction == EditFunctions.MorphAB)
                    {
                        Actions.ApplyEditFunctionAction a = null;

                        if (editContext.ManagedActionStack.CanUndo && !editContext.ManagedActionStack.CanRedo)
                        {
                            a = editContext.ManagedActionStack.Actions.Last() as Actions.ApplyEditFunctionAction;
                            if (a != null && a.IsUpdateable) a.Update();
                        }

                        if (a == null)
                            DoAction(new Actions.ApplyEditFunctionAction(this, updateable: true));
                    }
                }
            }
        }

        public bool IsEditFunctionParameterEnabled
        {
            get
            {
                switch (editFunction)
                {
                    case EditFunctions.RandomizeAbsolute: return false;
                    case EditFunctions.RandomizeRelative: return true;
                    case EditFunctions.RandomizeAB: return false;
                    case EditFunctions.RandomizeAorB: return true;
                    case EditFunctions.RandomizeMaybe: return true;
                    case EditFunctions.MorphAB: return true;
                    default: return false;
                }
            }
        }

        public bool IsPresetBEnabled
        {
            get
            {
                switch (editFunction)
                {
                    case EditFunctions.RandomizeAbsolute: return false;
                    case EditFunctions.RandomizeRelative: return false;
                    case EditFunctions.RandomizeAB: return true;
                    case EditFunctions.RandomizeAorB: return true;
                    case EditFunctions.RandomizeMaybe: return false;
                    case EditFunctions.MorphAB: return true;
                    default: return false;
                }

            }
        }

        string presetAName = "<default>";
        public string PresetAName
        {
            get { return presetAName; }
            set
            {
                presetAName = value;
                PropertyChanged.Raise(this, "PresetAName");
            }
        }

        string presetBName = "<default>";
        public string PresetBName
        {
            get { return presetBName; }
            set
            {
                presetBName = value;
                PropertyChanged.Raise(this, "PresetBName");
            }
        }

        public Preset PresetA { get { return machine.GetPreset(PresetAName); } }
        public Preset PresetB { get { return machine.GetPreset(PresetBName); } }

        public void DoAction(IAction a)
        {
            editContext.ManagedActionStack.Do(a);
        }

        public TextFormattingMode TextFormattingMode { get { return Global.GeneralSettings.WPFIdealFontMetrics ? TextFormattingMode.Ideal : TextFormattingMode.Display; } }

        #region Editor template

        private ControlTemplate editorTemplate = null;
        private bool searchedForEditorTemplate = false;

        private ControlTemplate GetEditorTemplate()
        {
            string path = Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
            path += "\\ParameterEditors\\" + machine.DLL.Name + ".xaml";

            System.Diagnostics.Trace.WriteLine(path);

            XmlReader reader;

            try
            {
                reader = XmlReader.Create(path);
            }
            catch (FileNotFoundException)
            {
                return null;
            }

            try
            {
                ControlTemplate t = XamlReader.Load(reader) as ControlTemplate;
                if (t == null)
                    MessageBox.Show(string.Format("Root element in {0} is not a ControlTemplate", path));

                return t;
            }
            catch (Exception e)
            {
                MessageBox.Show(string.Format("Error in {0}:\n\n{1}", path, e.Message));
                return null;
            }
        }

        public ControlTemplate EditorTemplate
        {
            get
            {
                if (!searchedForEditorTemplate)
                {
                    searchedForEditorTemplate = true;
                    editorTemplate = GetEditorTemplate();
                }
                return editorTemplate;
            }
        }

        public bool HasEditorTemplate { get { return EditorTemplate != null; } }

        #endregion
    }
}
