using AtmaFileSystem;
using AtmaFileSystem.IO;
using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using SimpleCommand = AttachedCommandBehavior.SimpleCommand;

namespace ReBuzzTests.Automation.TestMachines
{

    [MachineDecl(Name = "Synth", ShortName = "Synth", Author = "WDE", MaxTracks = 1, InputCount = 0,
        OutputCount = 1)]
    public class Synth(IBuzzMachineHost host) : IBuzzMachine, INotifyPropertyChanged
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
            return typeof(Synth).GetCustomAttributes(false)
                .OfType<MachineDecl>().Single();
        }

        [ParameterDecl(ValueDescriptions = ["no", "yes"])]
        public bool Bypass { get; set; }

        public Sample Work()
        {
            Debugger.Break();
            Console.WriteLine($"=== Sample: {leftSample}, {rightSample}"); //bug
            return new Sample(leftSample, rightSample);
        }

        // actual machine ends here. the stuff below demonstrates some other features of the api.

        public class State : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
        }

        private State machineState = new();
        private float leftSample = 0;
        private float rightSample = 0;

        public State MachineState // a property called 'MachineState' gets automatically saved in songs and presets
        {
            get => machineState;
            set
            {
                machineState = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MachineState)));
            }
        }

        public IEnumerable<IMenuItem> Commands =>
        [
            CommandItem<float>("SetLeftSample", o => leftSample = o),
            CommandItem<float>("SetRightSample", o => rightSample = o),
        ];

        private MenuItemVM CommandItem<T>(string text, Action<T> executeDelegate)
        {
            return new MenuItemVM
            {
                Text = text,
                Command = new BuzzGUI.Common.SimpleCommand
                {
                    CanExecuteDelegate = _ => true, ExecuteDelegate = o => executeDelegate((T)o)
                }
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}