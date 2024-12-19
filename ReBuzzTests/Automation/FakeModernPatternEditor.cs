using AtmaFileSystem;
using AtmaFileSystem.IO;
using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace ReBuzzTests.Automation
{
    [MachineDecl(Name = "Modern Pattern Editor", ShortName = "MPE", Author = "WDE", MaxTracks = 1, InputCount = 0,
        OutputCount = 0)]
    public class FakeModernPatternEditor : IBuzzMachine, INotifyPropertyChanged
    {
        private IBuzzMachineHost host;

        public static string GetSourceCode()
        {
            return AbsoluteFilePath.OfThisFile().ReadAllText();
        }

        public static MachineDecl GetMachineDecl()
        {
            return typeof(FakeModernPatternEditor).GetCustomAttributes(false)
                .OfType<MachineDecl>().Single();
        }

        public FakeModernPatternEditor(IBuzzMachineHost host)
        {
            this.host = host;
            Gain = new Interpolator();
        }

        [ParameterDecl(ResponseTime = 5, MaxValue = 127, DefValue = 80, Transformation = Transformations.Cubic,
            TransformUnityValue = 80, ValueDescriptor = Descriptors.Decibel)]
        public Interpolator Gain { get; private set; }

        [ParameterDecl(ValueDescriptions = ["no", "yes"])]
        public bool Bypass { get; set; }


        [ParameterDecl(MaxValue = 127, DefValue = 0)]
        public void ATrackParam(int v, int track)
        {
            // track parameter example
        }

        public Sample Work(Sample s)
        {
            return Bypass ? s : s * Gain.Tick();
        }

        // actual machine ends here. the stuff below demonstrates some other features of the api.

        public class State : INotifyPropertyChanged
        {
            public State()
            {
                text = "here is state";
            } // NOTE: parameterless constructor is required by the xml serializer

            private string text;

            public string Text
            {
                get => text;
                set
                {
                    text = value;
                    if (PropertyChanged != null)
                    {
                        PropertyChanged(this, new PropertyChangedEventArgs("Text"));
                    }
                    // NOTE: the INotifyPropertyChanged stuff is only used for data binding in the GUI in this demo. it is not required by the serializer.
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        private State machineState = new();

        public State MachineState // a property called 'MachineState' gets automatically saved in songs and presets
        {
            get => machineState;
            set
            {
                machineState = value;
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("MachineState"));
                }
            }
        }

        private int checkedItem = 1;

        public IEnumerable<IMenuItem> Commands
        {
            get
            {
                yield return new MenuItemVM() { Text = "Hello" };
                yield return new MenuItemVM() { IsSeparator = true };
                yield return new MenuItemVM()
                {
                    Text = "Submenu",
                    Children =
                    [
                        new MenuItemVM() { Text = "Child 1" },
                        new MenuItemVM() { Text = "Child 2" }
                    ]
                };
                yield return new MenuItemVM() { Text = "Label", IsLabel = true };
                yield return new MenuItemVM()
                {
                    Text = "Checkable",
                    Children =
                    [
                        new MenuItemVM() { Text = "Child 1", IsCheckable = true, StaysOpenOnClick = true },
                        new MenuItemVM() { Text = "Child 2", IsCheckable = true, StaysOpenOnClick = true },
                        new MenuItemVM() { Text = "Child 3", IsCheckable = true, StaysOpenOnClick = true }
                    ]
                };

                var g = new MenuItemVM.Group();

                yield return new MenuItemVM()
                {
                    Text = "CheckGroup",
                    Children = Enumerable.Range(1, 5).Select(i => new MenuItemVM()
                    {
                        Text = "Child " + i,
                        IsCheckable = true,
                        CheckGroup = g,
                        StaysOpenOnClick = true,
                        IsChecked = i == checkedItem,
                        CommandParameter = i,
                        Command = new SimpleCommand()
                        {
                            CanExecuteDelegate = p => true, ExecuteDelegate = p => checkedItem = (int)p
                        }
                    })
                };
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}