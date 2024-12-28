using BuzzGUI.Common;
using BuzzGUI.Common.Templates;
using BuzzGUI.Interfaces;
using BuzzGUI.SequenceEditor;
using ReBuzz.Core;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;

namespace ReBuzz
{
    /// <summary>
    /// Interaction logic for EditorView.xaml
    /// </summary>
    public partial class EditorView : UserControl, INotifyPropertyChanged
    {
        public class MachineVM : INotifyPropertyChanged
        {
            IMachine machine;
            IPattern selectedPattern;
            private IBuzz reBuzz;

            public event PropertyChangedEventHandler PropertyChanged;

            public IMachine Machine
            {
                get => machine;
                set
                {
                    if (machine != value)
                    {
                        machine = value;
                        PropertyChanged.Raise(this, "Machine");
                    }
                }
            }
            public IPattern SelectedPattern
            {
                get => selectedPattern;
                set
                {
                    if (selectedPattern != value)
                    {
                        selectedPattern = value;
                        PropertyChanged.Raise(this, "SelectedPattern");
                    }
                }
            }

            public MachineVM(IBuzz rb)
            {
                this.reBuzz = rb;
            }
        }

        public ObservableCollection<IMachineDLL> EditorMachines { get; set; }


        List<MachineVM> machines;
        public List<MachineVM> Machines
        {
            get { return machines; }
            set { machines = value; PropertyChanged.Raise(this, "Machines"); }
        }

        MachineVM selectedMachine;
        public MachineVM SelectedMachine
        {
            get { return selectedMachine; }
            set
            {
                if (selectedMachine != value)
                {
                    selectedMachine = value;
                    PropertyChanged.Raise(this, "SelectedMachine");

                    if (selectedMachine != null && selectedMachine.Machine.Patterns.Count == 0)
                        machineBox.Focus();
                }
            }
        }

        IMachineDLL editorMachine;
        public IMachineDLL EditorMachine
        {
            get => editorMachine; set
            {
                var previous = editorMachine;
                if (editorMachine != value)
                {
                    editorMachine = value;
                    if (ReBuzz.SetEditorMachineForCurrent(value))
                    {
                        editorMachine = previous;
                    }
                    /*
                    if (editorMachine.IsManaged)
                    {
                        machineBoxLabel.Visibility = Visibility.Collapsed;
                        machineBox.Visibility = Visibility.Collapsed;
                        patternBoxLabel.Visibility = Visibility.Collapsed;
                        patternBox.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        machineBoxLabel.Visibility = Visibility.Visible;
                        machineBox.Visibility = Visibility.Visible;
                        patternBoxLabel.Visibility = Visibility.Visible;
                        patternBox.Visibility = Visibility.Visible;
                    }
                    */

                    Dispatcher.InvokeAsync(() =>
                    {
                        PropertyChanged.Raise(this, "EditorMachine");
                    });
                }
            }
        }
        SequenceEditor sequenceEditor;
        private readonly IRegistryEx registryEx;

        public SequenceEditor SequenceEditor
        {
            get => sequenceEditor;
            set
            {
                if (sequenceEditor != null)
                {
                    gridEditorView.Children.Remove(sequenceEditor);
                }
                sequenceEditor = value;
                Grid.SetRow(sequenceEditor, 0);
                gridEditorView.Children.Add(sequenceEditor);
            }
        }

        public ReBuzzCore ReBuzz { get; }

        public EditorView(ReBuzzCore reBuzz, IRegistryEx registryEx)
        {
            this.registryEx = registryEx;
            ReBuzz = reBuzz;
            DataContext = this;
            InitializeComponent();

            EditorMachines = new ObservableCollection<IMachineDLL>();
            machines = new List<MachineVM>();

            foreach (var machine in Global.Buzz.MachineDLLs.Values)
            {
                if (machine.Info != null && (machine.Info.Flags & MachineInfoFlags.PATTERN_EDITOR) == MachineInfoFlags.PATTERN_EDITOR)
                    EditorMachines.Add(machine);
            }
            EditorMachine = EditorMachines.FirstOrDefault();
            PropertyChanged.Raise(this, "EditorMachine");
            PropertyChanged.Raise(this, "EditorMachines");

            btDefThis.Click += (sender, e) =>
            {
                if (ReBuzz.PatternEditorMachine != null)
                    registryEx.Write(ReBuzz.PatternEditorMachine.DLL.Name, editorMachine.Name, "Settings");
            };

            btDefAll.Click += (sender, e) =>
            {
                registryEx.Write("DefaultPE", editorMachine.Name, "Settings");
            };

            ReBuzz.SetPatternEditorPatternChanged += (pattern) =>
            {
                if (pattern != null)
                {
                    var em = (pattern.Machine as MachineCore).EditorMachine;
                    if (em != null)
                    {
                        EditorMachine = em.DLL;
                    }

                    SelectedMachine = machines.FirstOrDefault(m => m.Machine == pattern.Machine);
                    if (SelectedMachine != null)
                        selectedMachine.SelectedPattern = pattern;
                }
            };

            patternBox.SelectionChanged += (sender, e) =>
            {
                ReBuzz.SetPatternEditorPattern(patternBox.SelectedItem as IPattern);
                if (ReBuzz.ActiveView == BuzzView.PatternView)
                    reBuzz.ActivatePatternEditor();
            };

            ReBuzz.SelectedMachineChanged += (machine) =>
            {
                SelectedMachine = machines.FirstOrDefault(m => m.Machine == machine);
            };

            ReBuzz.Song.MachineAdded += (machine) =>
            {
                machines.Add(new MachineVM(ReBuzz) { Machine = machine });
                Machines = machines.OrderBy(m => machine.Name).ToList();
            };

            ReBuzz.Song.MachineRemoved += (machine) =>
            {
                machines.RemoveAll(m => m.Machine == machine);
                PropertyChanged.Raise(this, "Machines");
            };

            foreach (var m in ReBuzz.Song.Machines)
                machines.Add(new MachineVM(ReBuzz) { Machine = m });
            Machines = machines.OrderBy(m => m.Machine.Name).ToList();

            this.KeyDown += (sender, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Alt)
                {
                    if (e.Key == Key.System)
                    {
                        switch (e.SystemKey)
                        {
                            case Key.M: machineBox.IsDropDownOpen = true; e.Handled = true; break;
                            case Key.P: patternBox.IsDropDownOpen = true; e.Handled = true; break;
                        }
                    }
                }
                else if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (e.Key == Key.Down)
                    {
                        machineBox.SelectedIndex++;
                        e.Handled = true;
                        reBuzz.ActivatePatternEditor();
                    }
                    else if (e.Key == Key.Up)
                    {
                        if (machineBox.SelectedIndex > 0) machineBox.SelectedIndex--;
                        e.Handled = true;
                        reBuzz.ActivatePatternEditor();
                    }
                    else if (e.Key == Key.Enter)
                    {
                        // Create new pattern
                        if (SelectedMachine != null)
                        {
                            if (selectedMachine.Machine.Patterns.Count == 0)
                            {
                                selectedMachine.Machine.CreatePattern("00", 16);
                            }
                        }
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Delete)
                    {
                        // Delete pattern
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Back)
                    {
                        // Show pattern properties
                        e.Handled = true;
                    }
                }
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
