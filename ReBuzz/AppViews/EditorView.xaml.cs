using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using BuzzGUI.SequenceEditor;
using ReBuzz.Core;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;

namespace ReBuzz
{
    /// <summary>
    /// Interaction logic for EditorView.xaml
    /// </summary>
    public partial class EditorView : UserControl, INotifyPropertyChanged
    {
        public ObservableCollection<IMachineDLL> EditorMachines { get; set; }

        IMachineDLL editorMachine;
        public IMachineDLL EditorMachine
        {
            get => editorMachine; set
            {
                return;
                var previous = editorMachine;
                if (editorMachine != value)
                {
                    editorMachine = value;
                    if (ReBuzz.SetEditorMachineForCurrent(value))
                    {
                        editorMachine = previous;
                    }
                    Dispatcher.InvokeAsync(() =>
                    {
                        PropertyChanged.Raise(this, "EditorMachine");
                    });
                }
            }
        }
        public SequenceEditor SequenceEditor { get; }
        public ReBuzzCore ReBuzz { get; }

        public EditorView(SequenceEditor sequenceEditor, ReBuzzCore reBuzz)
        {
            ReBuzz = reBuzz;
            DataContext = this;
            InitializeComponent();
            this.SequenceEditor = sequenceEditor;
            Grid.SetRow(sequenceEditor, 0);
            gridEditorView.Children.Add(sequenceEditor);

            EditorMachines = new ObservableCollection<IMachineDLL>();

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
                    RegistryEx.Write(ReBuzz.PatternEditorMachine.DLL.Name, editorMachine.Name, "Settings");
            };

            btDefAll.Click += (sender, e) =>
            {
                RegistryEx.Write("DefaultPE", editorMachine.Name, "Settings");
            };

        }




        public event PropertyChangedEventHandler PropertyChanged;
    }
}
