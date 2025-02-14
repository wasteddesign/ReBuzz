using AtmaFileSystem;
using AtmaFileSystem.IO;
using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace ReBuzzTests.Automation.TestMachines
{
    /// <summary>
    /// Without a Modern Pattern Editor machine, ReBuzz will not start.
    /// This file contains a fake machine. Most of the code is copied from some hello world
    /// example without giving it too much thought. The structure of this fake dll may be improved
    /// in the future as needed. For now, this rough fake serves its purpose.
    ///
    /// This file is also compiled into a dll and placed in the ReBuzz gear folder.
    /// This is why it has a method for getting its source code. Also, the fact that this file
    /// is compilable is why it cannot depend on any other files in this project. This is why
    /// the <see cref="FakeModernPatternEditorInfo"/> class exists.
    /// </summary>
    [MachineDecl(Name = "Modern Pattern Editor", ShortName = "MPE", Author = "WDE", MaxTracks = 1, InputCount = 0,
        OutputCount = 0)]
    public class FakeModernPatternEditor(IBuzzMachineHost host) : IBuzzMachine, INotifyPropertyChanged
    {
        private readonly IBuzzMachineHost host = host;

        /// <summary>
        /// Gets the source code of this file for compilation into a dll.
        /// </summary>
        public static string GetSourceCode()
        {
            return AbsoluteFilePath.OfThisFile().ReadAllText();
        }

        public static MachineDecl GetMachineDecl()
        {
            return typeof(FakeModernPatternEditor).GetCustomAttributes(false)
                .OfType<MachineDecl>().Single();
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
            return s;
        }

        // actual machine ends here. the stuff below demonstrates some other features of the api.

        public class State : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
        }

        private State machineState = new();

        public State MachineState // a property called 'MachineState' gets automatically saved in songs and presets
        {
            get => machineState;
            set
            {
                machineState = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MachineState)));
            }
        }

        private int checkedItem = 1;

        public IEnumerable<IMenuItem> Commands
        {
            get
            {
                yield return new MenuItemVM { Text = "Hello" };
                yield return new MenuItemVM { IsSeparator = true };
                yield return new MenuItemVM
                {
                    Text = "Submenu",
                    Children =
                    [
                        new MenuItemVM { Text = "Child 1" },
                        new MenuItemVM { Text = "Child 2" }
                    ]
                };
                yield return new MenuItemVM { Text = "Label", IsLabel = true };
                yield return new MenuItemVM
                {
                    Text = "Checkable",
                    Children =
                    [
                        new MenuItemVM { Text = "Child 1", IsCheckable = true, StaysOpenOnClick = true },
                        new MenuItemVM { Text = "Child 2", IsCheckable = true, StaysOpenOnClick = true },
                        new MenuItemVM { Text = "Child 3", IsCheckable = true, StaysOpenOnClick = true }
                    ]
                };

                var g = new MenuItemVM.Group();

                yield return new MenuItemVM
                {
                    Text = "CheckGroup",
                    Children = Enumerable.Range(1, 5).Select(i => new MenuItemVM
                    {
                        Text = "Child " + i,
                        IsCheckable = true,
                        CheckGroup = g,
                        StaysOpenOnClick = true,
                        IsChecked = i == checkedItem,
                        CommandParameter = i,
                        Command = new SimpleCommand
                        {
                            CanExecuteDelegate = p => true,
                            ExecuteDelegate = p => checkedItem = (int)p
                        }
                    })
                };
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}